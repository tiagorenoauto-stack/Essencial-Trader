using System;
using System.Globalization;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge
{
    // Read-only event bridge: subscribes to a real NinjaTrader Account's OrderUpdate and
    // ExecutionUpdate events, converts them into neutral records, and forwards to the
    // Safe Core's OrderEventRouter. This class never sends, modifies, or cancels orders.
    //
    // Filtering: events for any account other than the configured AccountName are ignored.
    // If InstrumentFullName is provided, events for any other instrument are also ignored.
    //
    // Lifetime: Start/Stop/Dispose are all idempotent. After Dispose, no NinjaTrader handler
    // remains attached. This is the only correct shape under NinjaScript recompiles, where
    // old runtime instances may briefly coexist with new ones.
    public sealed class NinjaTraderAccountEventBridge : INinjaTraderEventSource
    {
        private readonly ISafeCoreLogger logger;
        private readonly Account account;
        private readonly OrderEventRouter router;
        private readonly string accountName;
        private readonly string instrumentFullName; // may be null/empty -> no instrument filter
        private readonly object gate;

        private bool running;
        private bool disposed;

        public NinjaTraderAccountEventBridge(
            ISafeCoreLogger logger,
            Account account,
            OrderEventRouter router,
            string instrumentFullName)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            if (account == null) throw new ArgumentNullException("account");
            if (router == null) throw new ArgumentNullException("router");

            this.logger = logger;
            this.account = account;
            this.router = router;
            this.accountName = account.Name ?? string.Empty;
            this.instrumentFullName = instrumentFullName ?? string.Empty;
            this.gate = new object();
        }

        public bool IsRunning
        {
            get { lock (gate) { return running && !disposed; } }
        }

        public void Start()
        {
            lock (gate)
            {
                if (disposed)
                {
                    logger.Order("EventBridge Start ignored: already disposed");
                    return;
                }
                if (running)
                {
                    logger.Order("EventBridge Start ignored: already running account=" + accountName);
                    return;
                }

                account.OrderUpdate += OnOrderUpdate;
                account.ExecutionUpdate += OnExecutionUpdate;
                running = true;
            }

            logger.Order(
                "EventBridge subscribed account=" + accountName +
                " instrumentFilter=" + (string.IsNullOrEmpty(instrumentFullName) ? "*" : instrumentFullName));
        }

        public void Stop()
        {
            bool wasRunning;
            lock (gate)
            {
                wasRunning = running;
                if (running)
                {
                    try
                    {
                        account.OrderUpdate -= OnOrderUpdate;
                        account.ExecutionUpdate -= OnExecutionUpdate;
                    }
                    catch (Exception ex)
                    {
                        // Detach must never throw upward; log and continue. Even on exception,
                        // we mark not-running so a follow-up Stop is a no-op.
                        logger.Order("EventBridge Stop detach error ex=" + ex.GetType().Name + ":" + ex.Message);
                    }
                    running = false;
                }
            }

            if (wasRunning)
                logger.Order("EventBridge unsubscribed account=" + accountName);
            else
                logger.Order("EventBridge Stop ignored: not running account=" + accountName);
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
            }
            Stop();
        }

        // Thread-safe gate consulted at the very top of every event handler. Detach is
        // not always synchronous in NinjaTrader -- an event already in flight may still be
        // delivered after Stop()/Dispose(). Anything observed in that window must be
        // dropped before it reaches OrderEventRouter.
        private bool CanRouteEvents()
        {
            lock (gate)
            {
                return running && !disposed;
            }
        }

        private void OnOrderUpdate(object sender, OrderEventArgs args)
        {
            if (!CanRouteEvents())
            {
                logger.Order("EventBridge OrderUpdate ignored: bridge not running");
                return;
            }

            try
            {
                if (args == null)
                {
                    logger.Order("EventBridge OrderUpdate ignored: null args");
                    return;
                }

                Order order = args.Order;
                if (order == null)
                {
                    logger.Order("EventBridge OrderUpdate ignored: null order");
                    return;
                }

                string evtAccount = order.Account != null ? (order.Account.Name ?? string.Empty) : string.Empty;
                string evtInstrument = order.Instrument != null ? (order.Instrument.FullName ?? string.Empty) : string.Empty;
                string state = ResolveOrderState(order, args);
                string orderId = ResolveOrderId(order);
                string actionText = ResolveOrderActionText(order);

                logger.Order(
                    "EventBridge OrderUpdate received account=" + evtAccount +
                    " instrument=" + evtInstrument +
                    " orderId=" + (string.IsNullOrEmpty(orderId) ? "-" : orderId) +
                    " action=" + (string.IsNullOrEmpty(actionText) ? "-" : actionText) +
                    " state=" + state);

                if (!ScopeMatches(evtAccount, evtInstrument, "OrderUpdate", orderId))
                    return;

                OrderEventRecord record;
                record.OrderId = orderId ?? string.Empty;
                record.AccountName = evtAccount;
                record.InstrumentFullName = evtInstrument;
                record.State = state;
                record.ActionText = actionText;
                record.TimeUtc = ResolveOrderTimeUtc(order);

                bool routed = router.TryHandleOrderUpdate(record);
                logger.Order(
                    "EventBridge OrderUpdate " + (routed ? "routed" : "duplicate-or-rejected") +
                    " orderId=" + (string.IsNullOrEmpty(orderId) ? "-" : orderId) +
                    " state=" + state);
            }
            catch (Exception ex)
            {
                // Never let a handler exception escape into NinjaTrader's event pipeline.
                logger.Order("EventBridge OrderUpdate handler error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private void OnExecutionUpdate(object sender, ExecutionEventArgs args)
        {
            if (!CanRouteEvents())
            {
                logger.Order("EventBridge ExecutionUpdate ignored: bridge not running");
                return;
            }

            try
            {
                if (args == null)
                {
                    logger.Order("EventBridge ExecutionUpdate ignored: null args");
                    return;
                }

                Execution execution = args.Execution;
                if (execution == null)
                {
                    logger.Order("EventBridge ExecutionUpdate ignored: null execution");
                    return;
                }

                string evtAccount = execution.Account != null ? (execution.Account.Name ?? string.Empty) : string.Empty;
                string evtInstrument = execution.Instrument != null ? (execution.Instrument.FullName ?? string.Empty) : string.Empty;
                string executionId = execution.ExecutionId ?? string.Empty;
                string orderId = execution.Order != null ? ResolveOrderId(execution.Order) : string.Empty;
                int quantity = execution.Quantity;
                double price = execution.Price;
                string actionText = ResolveExecutionActionText(execution);

                logger.Order(
                    "EventBridge ExecutionUpdate received account=" + evtAccount +
                    " instrument=" + evtInstrument +
                    " executionId=" + (string.IsNullOrEmpty(executionId) ? "-" : executionId) +
                    " orderId=" + (string.IsNullOrEmpty(orderId) ? "-" : orderId) +
                    " action=" + (string.IsNullOrEmpty(actionText) ? "-" : actionText) +
                    " qty=" + quantity.ToString(CultureInfo.InvariantCulture) +
                    " price=" + price.ToString("0.#####", CultureInfo.InvariantCulture));

                if (!ScopeMatches(evtAccount, evtInstrument, "ExecutionUpdate", executionId))
                    return;

                ExecutionEventRecord record;
                record.ExecutionId = executionId;
                record.OrderId = orderId;
                record.AccountName = evtAccount;
                record.InstrumentFullName = evtInstrument;
                record.Quantity = quantity;
                record.Price = price;
                record.ActionText = actionText;
                record.TimeUtc = ResolveExecutionTimeUtc(execution);

                bool routed = router.TryHandleExecutionUpdate(record);
                logger.Order(
                    "EventBridge ExecutionUpdate " + (routed ? "routed" : "duplicate-or-rejected") +
                    " executionId=" + (string.IsNullOrEmpty(executionId) ? "-" : executionId) +
                    " orderId=" + (string.IsNullOrEmpty(orderId) ? "-" : orderId));
            }
            catch (Exception ex)
            {
                logger.Order("EventBridge ExecutionUpdate handler error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private bool ScopeMatches(string evtAccount, string evtInstrument, string kind, string id)
        {
            if (!string.Equals(evtAccount, accountName, StringComparison.Ordinal))
            {
                logger.Order(
                    "EventBridge " + kind + " ignored: account mismatch event=" + evtAccount +
                    " bridge=" + accountName +
                    " id=" + (string.IsNullOrEmpty(id) ? "-" : id));
                return false;
            }
            if (!string.IsNullOrEmpty(instrumentFullName)
                && !string.Equals(evtInstrument, instrumentFullName, StringComparison.Ordinal))
            {
                logger.Order(
                    "EventBridge " + kind + " ignored: instrument mismatch event=" + evtInstrument +
                    " bridge=" + instrumentFullName +
                    " id=" + (string.IsNullOrEmpty(id) ? "-" : id));
                return false;
            }
            return true;
        }

        private static string ResolveOrderState(Order order, OrderEventArgs args)
        {
            // Prefer the Order's current state; fall back to args.OrderState if needed.
            try
            {
                return order.OrderState.ToString();
            }
            catch
            {
                try { return args.OrderState.ToString(); }
                catch { return string.Empty; }
            }
        }

        private static string ResolveOrderId(Order order)
        {
            string id = order.OrderId;
            if (string.IsNullOrEmpty(id))
            {
                try { id = order.Id.ToString(CultureInfo.InvariantCulture); }
                catch { id = string.Empty; }
            }
            return id ?? string.Empty;
        }

        // Convert NinjaTrader OrderAction (Buy / Sell / SellShort / BuyToCover) to a neutral
        // string. Strings only -- the Safe Core never imports the OrderAction enum.
        private static string ResolveOrderActionText(Order order)
        {
            try { return order.OrderAction.ToString(); }
            catch { return string.Empty; }
        }

        // Execution side comes from MarketPosition (Long/Short/Flat). Falls back to the
        // owning Order's OrderAction when MarketPosition is unavailable.
        private static string ResolveExecutionActionText(Execution execution)
        {
            try
            {
                string mp = execution.MarketPosition.ToString();
                if (!string.IsNullOrEmpty(mp)) return mp;
            }
            catch
            {
                // ignore and fall through
            }
            try
            {
                if (execution.Order != null) return execution.Order.OrderAction.ToString();
            }
            catch
            {
                // ignore
            }
            return string.Empty;
        }

        private static DateTime ResolveOrderTimeUtc(Order order)
        {
            try
            {
                DateTime t = order.Time;
                return t == DateTime.MinValue ? DateTime.UtcNow : t.ToUniversalTime();
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static DateTime ResolveExecutionTimeUtc(Execution execution)
        {
            try
            {
                DateTime t = execution.Time;
                return t == DateTime.MinValue ? DateTime.UtcNow : t.ToUniversalTime();
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }
}

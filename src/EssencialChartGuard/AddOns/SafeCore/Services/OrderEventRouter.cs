using System;
using System.Collections.Generic;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    // Neutral records the router consumes. A future adapter must map NinjaTrader
    // OrderEventArgs/ExecutionEventArgs into these, never call the router with raw NT types.
    //
    // ActionText is a neutral string the bridge fills from OrderAction / MarketPosition
    // ("Buy", "Sell", "SellShort", "BuyToCover", "Long", "Short", "Flat", ...). Kept as a
    // string so the Safe Core stays free of NinjaTrader.Cbi enums.
    public struct OrderEventRecord
    {
        public string OrderId;
        public string AccountName;
        public string InstrumentFullName;
        public string State;
        public string ActionText;
        public DateTime TimeUtc;
    }

    public struct ExecutionEventRecord
    {
        public string ExecutionId;
        public string OrderId;
        public string AccountName;
        public string InstrumentFullName;
        public int Quantity;
        public double Price;
        public string ActionText;
        public DateTime TimeUtc;
    }

    public sealed class OrderEventRouter
    {
        private readonly ISafeCoreLogger logger;
        private readonly ObservedAccountState observedState; // optional
        private readonly HashSet<string> seenOrderEventKeys;
        private readonly HashSet<string> seenExecutionKeys;
        private readonly object gate;

        public OrderEventRouter(ISafeCoreLogger logger)
            : this(logger, null)
        {
        }

        public OrderEventRouter(ISafeCoreLogger logger, ObservedAccountState observedState)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            this.logger = logger;
            this.observedState = observedState;
            this.seenOrderEventKeys = new HashSet<string>(StringComparer.Ordinal);
            this.seenExecutionKeys = new HashSet<string>(StringComparer.Ordinal);
            this.gate = new object();
        }

        public ObservedAccountState ObservedState
        {
            get { return observedState; }
        }

        public bool TryHandleOrderUpdate(OrderEventRecord record)
        {
            string key = BuildOrderKey(record);
            if (string.IsNullOrEmpty(key))
            {
                logger.Order("Order update ignored: missing stable key");
                return false;
            }

            lock (gate)
            {
                if (!seenOrderEventKeys.Add(key))
                {
                    logger.Order("Order update duplicate ignored key=" + key);
                    return false;
                }
            }

            logger.Order(
                "OrderUpdate accepted key=" + key +
                " account=" + (record.AccountName ?? "?") +
                " instrument=" + (record.InstrumentFullName ?? "?") +
                " state=" + (record.State ?? "?"));

            if (observedState != null)
            {
                observedState.ApplyOrder(record);
                observedState.TryLogSummaryIfChanged(logger);
            }
            return true;
        }

        public bool TryHandleExecutionUpdate(ExecutionEventRecord record)
        {
            string key = BuildExecutionKey(record);
            if (string.IsNullOrEmpty(key))
            {
                logger.Order("Execution update ignored: missing stable key");
                return false;
            }

            lock (gate)
            {
                if (!seenExecutionKeys.Add(key))
                {
                    logger.Order("Execution update duplicate ignored key=" + key);
                    return false;
                }
            }

            logger.Order(
                "ExecutionUpdate accepted key=" + key +
                " account=" + (record.AccountName ?? "?") +
                " instrument=" + (record.InstrumentFullName ?? "?") +
                " action=" + (string.IsNullOrEmpty(record.ActionText) ? "?" : record.ActionText) +
                " qty=" + record.Quantity +
                " price=" + record.Price.ToString("0.#####"));

            if (observedState != null)
            {
                observedState.ApplyExecution(record);
                observedState.TryLogSummaryIfChanged(logger);
            }
            return true;
        }

        public void Reset()
        {
            lock (gate)
            {
                seenOrderEventKeys.Clear();
                seenExecutionKeys.Clear();
            }
            if (observedState != null) observedState.Reset();
            logger.Order("OrderEventRouter state reset");
        }

        // Order key: same OrderId in the same state must be treated as the same event,
        // because NinjaTrader can re-raise OrderUpdate for the same transition (e.g. on reconnect).
        private static string BuildOrderKey(OrderEventRecord record)
        {
            if (string.IsNullOrEmpty(record.OrderId)) return null;
            string state = record.State ?? string.Empty;
            return record.OrderId + "|" + state;
        }

        // Execution key: prefer ExecutionId. Fall back to OrderId+qty+price only when
        // ExecutionId is missing, since duplicate ExecutionUpdate is the main reason
        // protection orders end up duplicated for the same fill.
        private static string BuildExecutionKey(ExecutionEventRecord record)
        {
            if (!string.IsNullOrEmpty(record.ExecutionId)) return record.ExecutionId;
            if (!string.IsNullOrEmpty(record.OrderId))
                return record.OrderId + "|" + record.Quantity + "|" + record.Price.ToString("0.#####");
            return null;
        }
    }
}

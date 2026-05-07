using System;
using System.Collections.Generic;
using System.Globalization;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    public enum ObservedPosition
    {
        Unknown,
        Flat,
        Long,
        Short
    }

    // Approximate, event-driven mirror of an account's order/position state. Built from the
    // routed OrderEventRouter records only; not authoritative. The truth source remains
    // NinjaTrader's Account.Orders / Account.Positions, which a later iteration must
    // reconcile against this view. Until then, any discrepancy must be assumed to favour
    // the NinjaTrader side, not this state.
    //
    // Thread-safety: a single internal lock guards all mutating reads/writes. Callers
    // (OrderEventRouter) already hold their own gate, but we lock here too because UI/log
    // readers may snapshot the state from a different thread.
    public sealed class ObservedAccountState
    {
        private readonly object gate;
        private readonly Dictionary<string, ObservedOrderState> ordersByOrderId;

        private ObservedExecutionState lastExecution;
        private ObservedPosition position;
        private int positionQuantity;
        private double? lastExecutionPrice;
        private string lastSummaryHash;
        private DateTime lastUpdateUtc;
        // Account/instrument identity observed from the initial snapshot or the most recent
        // routed event. Lets BuildSummary report a meaningful account/instrument even when
        // there is no execution and no working order yet (e.g. attach-time Flat snapshot).
        private string observedAccountName;
        private string observedInstrumentFullName;

        public ObservedAccountState()
        {
            gate = new object();
            ordersByOrderId = new Dictionary<string, ObservedOrderState>(StringComparer.Ordinal);
            position = ObservedPosition.Unknown;
            positionQuantity = 0;
            lastUpdateUtc = DateTime.UtcNow;
        }

        public ObservedPosition Position
        {
            get { lock (gate) return position; }
        }

        public int PositionQuantity
        {
            get { lock (gate) return positionQuantity; }
        }

        public double? LastExecutionPrice
        {
            get { lock (gate) return lastExecutionPrice; }
        }

        public DateTime LastUpdateUtc
        {
            get { lock (gate) return lastUpdateUtc; }
        }

        public int WorkingOrdersCount
        {
            get
            {
                lock (gate)
                {
                    int count = 0;
                    foreach (KeyValuePair<string, ObservedOrderState> kvp in ordersByOrderId)
                    {
                        if (kvp.Value != null && kvp.Value.IsWorking) count++;
                    }
                    return count;
                }
            }
        }

        // Read-only initial reconciliation: seed the observed state from a position snapshot
        // captured directly from NinjaTrader's Account.Positions (via the bridge's reader).
        // This is meant for attach-time only; after this, OrderEventRouter keeps the state
        // in sync via routed Order/Execution events. The truth source for positions remains
        // NinjaTrader's Account.Positions -- this snapshot is an approximate seed, not an
        // ongoing mirror.
        public void ApplyPositionSnapshot(ObservedPositionSnapshot snapshot)
        {
            if (snapshot == null) return;

            int qty = snapshot.Quantity;
            if (qty < 0) qty = -qty;

            ObservedPosition newPosition;
            int newSignedQuantity;

            if (qty == 0 || IsFlatText(snapshot.PositionText))
            {
                newPosition = ObservedPosition.Flat;
                newSignedQuantity = 0;
            }
            else if (IsLongText(snapshot.PositionText))
            {
                newPosition = ObservedPosition.Long;
                newSignedQuantity = +qty;
            }
            else if (IsShortText(snapshot.PositionText))
            {
                newPosition = ObservedPosition.Short;
                newSignedQuantity = -qty;
            }
            else
            {
                // Unrecognised PositionText with non-zero qty: refuse to guess the sign.
                newPosition = ObservedPosition.Unknown;
                newSignedQuantity = 0;
            }

            lock (gate)
            {
                position = newPosition;
                positionQuantity = newSignedQuantity;
                if (snapshot.AveragePrice.HasValue)
                    lastExecutionPrice = snapshot.AveragePrice;
                if (!string.IsNullOrEmpty(snapshot.AccountName))
                    observedAccountName = snapshot.AccountName;
                if (!string.IsNullOrEmpty(snapshot.InstrumentFullName))
                    observedInstrumentFullName = snapshot.InstrumentFullName;
                lastUpdateUtc = snapshot.TimeUtc;
            }
        }

        // Apply an order event already accepted by OrderEventRouter (i.e. non-duplicate).
        public void ApplyOrder(OrderEventRecord record)
        {
            string orderId = record.OrderId ?? string.Empty;
            if (orderId.Length == 0) return;

            lock (gate)
            {
                ObservedOrderState state;
                if (!ordersByOrderId.TryGetValue(orderId, out state) || state == null)
                {
                    state = new ObservedOrderState(orderId);
                    ordersByOrderId[orderId] = state;
                }

                state.AccountName = record.AccountName ?? state.AccountName;
                state.InstrumentFullName = record.InstrumentFullName ?? state.InstrumentFullName;
                state.LastState = record.State ?? state.LastState;
                if (!string.IsNullOrEmpty(record.ActionText))
                    state.ActionText = record.ActionText;
                state.LastUpdateUtc = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(record.AccountName))
                    observedAccountName = record.AccountName;
                if (!string.IsNullOrEmpty(record.InstrumentFullName))
                    observedInstrumentFullName = record.InstrumentFullName;

                lastUpdateUtc = state.LastUpdateUtc;
            }
        }

        // Apply an execution event already accepted by OrderEventRouter (i.e. non-duplicate).
        // Net position is computed by signed quantity using ActionText. If ActionText is
        // missing or unrecognised, position transitions to Unknown -- never silently guess.
        public void ApplyExecution(ExecutionEventRecord record)
        {
            ObservedExecutionState exec = new ObservedExecutionState(record.ExecutionId);
            exec.OrderId = record.OrderId ?? string.Empty;
            exec.AccountName = record.AccountName ?? string.Empty;
            exec.InstrumentFullName = record.InstrumentFullName ?? string.Empty;
            exec.Quantity = record.Quantity;
            exec.Price = record.Price;
            exec.ActionText = record.ActionText ?? string.Empty;
            exec.TimeUtc = record.TimeUtc == DateTime.MinValue ? DateTime.UtcNow : record.TimeUtc;

            lock (gate)
            {
                lastExecution = exec;
                lastExecutionPrice = record.Price;

                if (!string.IsNullOrEmpty(record.AccountName))
                    observedAccountName = record.AccountName;
                if (!string.IsNullOrEmpty(record.InstrumentFullName))
                    observedInstrumentFullName = record.InstrumentFullName;

                int signedDelta = SignedDelta(exec.ActionText, record.Quantity);
                if (signedDelta == int.MinValue)
                {
                    // Unrecognised action; we cannot tell what the position became.
                    position = ObservedPosition.Unknown;
                }
                else
                {
                    positionQuantity += signedDelta;
                    if (positionQuantity > 0) position = ObservedPosition.Long;
                    else if (positionQuantity < 0) position = ObservedPosition.Short;
                    else position = ObservedPosition.Flat;
                }

                lastUpdateUtc = exec.TimeUtc;
            }
        }

        // Returns a stable, single-line summary suitable for logging. Reading it does not
        // mutate state. Use TryLogSummaryIfChanged to avoid log spam when nothing changed.
        public string BuildSummary()
        {
            lock (gate)
            {
                string account = LookupAccountForSummary();
                string instrument = LookupInstrumentForSummary();
                string pos = position.ToString();
                string qty = AbsQuantityForSummary().ToString(CultureInfo.InvariantCulture);
                string lastPrice = lastExecutionPrice.HasValue
                    ? lastExecutionPrice.Value.ToString("0.#####", CultureInfo.InvariantCulture)
                    : "-";

                int working = 0;
                foreach (KeyValuePair<string, ObservedOrderState> kvp in ordersByOrderId)
                    if (kvp.Value != null && kvp.Value.IsWorking) working++;

                return "ObservedState account=" + account +
                    " instrument=" + instrument +
                    " position=" + pos +
                    " qty=" + qty +
                    " lastPrice=" + lastPrice +
                    " workingOrders=" + working.ToString(CultureInfo.InvariantCulture);
            }
        }

        // Returns a structured, value-typed snapshot suitable for read-only consumers like
        // the side panel. Built under the same lock as BuildSummary so the consumer never
        // sees a half-updated view. The DTO carries no NinjaTrader.Cbi types; the panel can
        // render directly from these fields.
        public ObservedAccountSnapshotDto BuildSnapshotDto()
        {
            ObservedAccountSnapshotDto dto;
            lock (gate)
            {
                dto.AccountName = LookupAccountForSummary();
                dto.InstrumentFullName = LookupInstrumentForSummary();
                dto.Position = position;
                dto.AbsoluteQuantity = AbsQuantityForSummary();
                dto.LastPrice = lastExecutionPrice;
                int working = 0;
                foreach (KeyValuePair<string, ObservedOrderState> kvp in ordersByOrderId)
                    if (kvp.Value != null && kvp.Value.IsWorking) working++;
                dto.WorkingOrdersCount = working;
                dto.LastUpdateUtc = lastUpdateUtc;
            }
            return dto;
        }

        // Logs the summary only when it changed since last log. Returns true when a log
        // line was emitted.
        public bool TryLogSummaryIfChanged(ISafeCoreLogger logger)
        {
            if (logger == null) return false;
            string summary = BuildSummary();

            lock (gate)
            {
                if (string.Equals(summary, lastSummaryHash, StringComparison.Ordinal))
                    return false;
                lastSummaryHash = summary;
            }

            logger.Order(summary);
            return true;
        }

        public void Reset()
        {
            lock (gate)
            {
                ordersByOrderId.Clear();
                lastExecution = null;
                position = ObservedPosition.Unknown;
                positionQuantity = 0;
                lastExecutionPrice = null;
                lastSummaryHash = null;
                observedAccountName = null;
                observedInstrumentFullName = null;
                lastUpdateUtc = DateTime.UtcNow;
            }
        }

        // Returns int.MinValue when the action text cannot be classified; the caller then
        // moves position to Unknown rather than guessing a sign.
        private static int SignedDelta(string actionText, int quantity)
        {
            if (string.IsNullOrEmpty(actionText)) return int.MinValue;

            if (actionText.IndexOf("BuyToCover", StringComparison.OrdinalIgnoreCase) >= 0) return +quantity;
            if (actionText.IndexOf("SellShort", StringComparison.OrdinalIgnoreCase) >= 0) return -quantity;
            if (actionText.IndexOf("Buy", StringComparison.OrdinalIgnoreCase) >= 0) return +quantity;
            if (actionText.IndexOf("Sell", StringComparison.OrdinalIgnoreCase) >= 0) return -quantity;
            // MarketPosition-style strings: "Long" / "Short" / "Flat".
            if (string.Equals(actionText, "Long", StringComparison.OrdinalIgnoreCase)) return +quantity;
            if (string.Equals(actionText, "Short", StringComparison.OrdinalIgnoreCase)) return -quantity;

            return int.MinValue;
        }

        private string LookupAccountForSummary()
        {
            if (!string.IsNullOrEmpty(observedAccountName))
                return observedAccountName;
            if (lastExecution != null && !string.IsNullOrEmpty(lastExecution.AccountName))
                return lastExecution.AccountName;
            foreach (KeyValuePair<string, ObservedOrderState> kvp in ordersByOrderId)
            {
                if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.AccountName))
                    return kvp.Value.AccountName;
            }
            return "?";
        }

        private string LookupInstrumentForSummary()
        {
            if (!string.IsNullOrEmpty(observedInstrumentFullName))
                return observedInstrumentFullName;
            if (lastExecution != null && !string.IsNullOrEmpty(lastExecution.InstrumentFullName))
                return lastExecution.InstrumentFullName;
            foreach (KeyValuePair<string, ObservedOrderState> kvp in ordersByOrderId)
            {
                if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.InstrumentFullName))
                    return kvp.Value.InstrumentFullName;
            }
            return "?";
        }

        private int AbsQuantityForSummary()
        {
            return positionQuantity < 0 ? -positionQuantity : positionQuantity;
        }

        private static bool IsFlatText(string text)
        {
            return string.Equals(text, "Flat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLongText(string text)
        {
            return string.Equals(text, "Long", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsShortText(string text)
        {
            return string.Equals(text, "Short", StringComparison.OrdinalIgnoreCase);
        }
    }
}

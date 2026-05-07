using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    // Last known state of one order, derived from OrderEventRecord. Approximate -- the
    // authoritative source remains NinjaTrader's Account.Orders. We rebuild this from
    // routed events only, so the field marked NinjaTrader-derived stay strings to avoid
    // pulling NT types into the Safe Core.
    public sealed class ObservedOrderState
    {
        public string OrderId { get; private set; }
        public string AccountName { get; set; }
        public string InstrumentFullName { get; set; }
        public string LastState { get; set; }
        public string ActionText { get; set; } // e.g. "Buy" / "Sell" / "SellShort" / "BuyToCover"
        public int Quantity { get; set; }
        public double? LastPrice { get; set; }
        public DateTime LastUpdateUtc { get; set; }

        public ObservedOrderState(string orderId)
        {
            if (orderId == null) throw new ArgumentNullException("orderId");
            OrderId = orderId;
            LastUpdateUtc = DateTime.UtcNow;
        }

        // Treat an order as "working" when it is live in the book. Strings are the
        // surface area we control: NinjaTrader's OrderState.ToString() values like
        // "Initialized", "Submitted", "Accepted", "Working", "Filled", "Cancelled",
        // "Rejected" are mapped here without importing the enum.
        public bool IsWorking
        {
            get
            {
                if (string.IsNullOrEmpty(LastState)) return false;
                if (string.Equals(LastState, "Working", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "Accepted", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "Submitted", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "ChangeSubmitted", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "ChangePending", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "CancelSubmitted", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "CancelPending", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        public bool IsTerminal
        {
            get
            {
                if (string.IsNullOrEmpty(LastState)) return false;
                if (string.Equals(LastState, "Filled", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "Cancelled", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "Rejected", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "Unknown", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(LastState, "Lapsed", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }
    }
}

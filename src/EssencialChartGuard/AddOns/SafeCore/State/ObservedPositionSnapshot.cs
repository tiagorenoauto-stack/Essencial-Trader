using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    // Neutral, read-only snapshot of an account/instrument position. The bridge fills this
    // by reading NinjaTrader's Account.Positions and converting MarketPosition into a string
    // ("Long" / "Short" / "Flat"). The Safe Core consumes the snapshot through
    // ObservedAccountState.ApplyPositionSnapshot to seed initial state at attach time.
    //
    // This is a one-shot reconciliation -- not a continuous mirror. After applying the
    // snapshot, ObservedAccountState continues to be updated by routed Order/Execution
    // events through OrderEventRouter.
    public sealed class ObservedPositionSnapshot
    {
        public string AccountName { get; private set; }
        public string InstrumentFullName { get; private set; }
        public string PositionText { get; private set; } // "Long" / "Short" / "Flat" (MarketPosition.ToString())
        public int Quantity { get; private set; }        // absolute size; sign inferred from PositionText
        public double? AveragePrice { get; private set; }
        public DateTime TimeUtc { get; private set; }

        public ObservedPositionSnapshot(
            string accountName,
            string instrumentFullName,
            string positionText,
            int quantity,
            double? averagePrice,
            DateTime timeUtc)
        {
            AccountName = accountName ?? string.Empty;
            InstrumentFullName = instrumentFullName ?? string.Empty;
            PositionText = positionText ?? string.Empty;
            Quantity = quantity < 0 ? -quantity : quantity;
            AveragePrice = averagePrice;
            TimeUtc = timeUtc == DateTime.MinValue ? DateTime.UtcNow : timeUtc;
        }

        public static ObservedPositionSnapshot Flat(string accountName, string instrumentFullName)
        {
            return new ObservedPositionSnapshot(
                accountName,
                instrumentFullName,
                "Flat",
                0,
                null,
                DateTime.UtcNow);
        }
    }
}

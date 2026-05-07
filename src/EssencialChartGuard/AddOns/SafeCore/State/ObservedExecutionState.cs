using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    // Last known execution observed by the bridge, derived from ExecutionEventRecord.
    // ActionText is a neutral string ("Long", "Short", "Buy", "SellShort", etc.) the
    // bridge fills from NinjaTrader Order/Execution -- the Safe Core never sees the
    // NinjaTrader enums.
    public sealed class ObservedExecutionState
    {
        public string ExecutionId { get; private set; }
        public string OrderId { get; set; }
        public string AccountName { get; set; }
        public string InstrumentFullName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public string ActionText { get; set; }
        public DateTime TimeUtc { get; set; }

        public ObservedExecutionState(string executionId)
        {
            ExecutionId = executionId ?? string.Empty;
            TimeUtc = DateTime.UtcNow;
        }
    }
}

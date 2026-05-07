using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    public sealed class ChartGuardState
    {
        public string AccountName { get; set; }
        public string InstrumentFullName { get; set; }
        public ProtectionState Protection { get; private set; }
        public bool EntriesBlocked { get; set; }
        public string EntryBlockReason { get; set; }
        public DateTime LastUpdateUtc { get; private set; }

        public ChartGuardState()
        {
            Protection = new ProtectionState();
            LastUpdateUtc = DateTime.UtcNow;
        }

        public void Touch()
        {
            LastUpdateUtc = DateTime.UtcNow;
        }
    }
}

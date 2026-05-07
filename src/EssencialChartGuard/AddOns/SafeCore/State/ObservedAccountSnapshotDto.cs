using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    // Neutral, value-typed snapshot of the observed account state, suitable for read-only
    // consumers like the side panel. Built by ObservedAccountState.BuildSnapshotDto() under
    // its internal lock so the consumer never sees a half-updated view. No NinjaTrader.Cbi
    // type is involved -- everything is plain strings/numbers/enums local to the Safe Core.
    public struct ObservedAccountSnapshotDto
    {
        public string AccountName;          // "?" when unknown
        public string InstrumentFullName;   // "?" when unknown
        public ObservedPosition Position;   // Unknown / Flat / Long / Short
        public int AbsoluteQuantity;        // absolute net size; sign is in Position
        public double? LastPrice;           // null when never observed
        public int WorkingOrdersCount;
        public DateTime LastUpdateUtc;
    }
}

using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    public sealed class RiskDecision
    {
        public bool AllowEntry { get; private set; }
        public string Reason { get; private set; }
        public DateTime TimestampUtc { get; private set; }

        private RiskDecision(bool allowEntry, string reason)
        {
            AllowEntry = allowEntry;
            Reason = reason ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }

        public static RiskDecision Allow(string reason = null)
        {
            return new RiskDecision(true, reason);
        }

        public static RiskDecision Block(string reason)
        {
            return new RiskDecision(false, reason);
        }
    }
}

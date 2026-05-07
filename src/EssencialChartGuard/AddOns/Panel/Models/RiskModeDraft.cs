namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models
{
    // Preview-only risk mode selection. The panel may render Alert / Block / Off,
    // but no risk service is configured from this object in Phase 2.
    public sealed class RiskModeDraft
    {
        public string Mode { get; set; }
        public string DailyLimit { get; set; }
        public string Status { get; set; }
        public string BlockStatus { get; set; }

        public RiskModeDraft()
        {
            Mode = "Alert";
            DailyLimit = string.Empty;
            Status = string.Empty;
            BlockStatus = string.Empty;
        }

        public static RiskModeDraft Alert()
        {
            return new RiskModeDraft();
        }
    }
}

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models
{
    // Preview-only protection settings for BE / Lock R / Trail display.
    public sealed class ProtectionDraft
    {
        public bool BreakevenEnabled { get; set; }
        public bool Lock1REnabled { get; set; }
        public bool Lock2REnabled { get; set; }
        public bool Lock3REnabled { get; set; }
        public bool TrailEnabled { get; set; }
        public string Summary { get; set; }

        public ProtectionDraft()
        {
            Summary = string.Empty;
        }

        public static ProtectionDraft Empty()
        {
            return new ProtectionDraft();
        }
    }
}

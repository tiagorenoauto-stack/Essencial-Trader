namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models
{
    // Preview-only stop configuration. It is a draft of intent, not an active
    // protective order and not a service command.
    public sealed class StopDraft
    {
        public string Current { get; set; }
        public string Unit { get; set; }
        public bool IsRequired { get; set; }

        public StopDraft()
        {
            Current = string.Empty;
            Unit = "Ticks";
            IsRequired = true;
        }

        public static StopDraft Empty()
        {
            return new StopDraft();
        }
    }
}

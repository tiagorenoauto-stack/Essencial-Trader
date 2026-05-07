namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models
{
    // Preview-only take target row. Quantity and value are rendered by the panel;
    // later phases can validate them before any command path exists.
    public sealed class TakeTargetDraft
    {
        public string Label { get; set; }
        public int Quantity { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }

        public TakeTargetDraft()
        {
            Label = string.Empty;
            Quantity = 0;
            Value = string.Empty;
            Unit = "Ticks";
        }
    }
}

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models
{
    // Preview-only entry plan. Values are intended for display and future
    // validation; this model does not execute or route anything.
    public sealed class EntryPlanDraft
    {
        public string EntryType { get; set; }
        public int Quantity { get; set; }
        public string SizingMode { get; set; }
        public string Unit { get; set; }
        public string Stop { get; set; }
        public string Target { get; set; }

        public EntryPlanDraft()
        {
            EntryType = "Market";
            Quantity = 1;
            SizingMode = "Fixed";
            Unit = "Ticks";
            Stop = string.Empty;
            Target = string.Empty;
        }

        public static EntryPlanDraft Default()
        {
            return new EntryPlanDraft();
        }
    }
}

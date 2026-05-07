namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models
{
    // Pure preview model for a named strategy. It carries UI defaults only; no
    // NinjaTrader account, instrument, order, or command reference belongs here.
    public sealed class StrategyDraft
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public EntryPlanDraft DefaultEntryPlan { get; set; }
        public StopDraft DefaultStop { get; set; }
        public TakeTargetDraft[] DefaultTargets { get; set; }
        public ProtectionDraft DefaultProtection { get; set; }
        public RiskModeDraft DefaultRiskMode { get; set; }

        public StrategyDraft()
        {
            Name = "default";
            Description = string.Empty;
            DefaultEntryPlan = EntryPlanDraft.Default();
            DefaultStop = StopDraft.Empty();
            DefaultTargets = new TakeTargetDraft[0];
            DefaultProtection = ProtectionDraft.Empty();
            DefaultRiskMode = RiskModeDraft.Alert();
        }
    }
}

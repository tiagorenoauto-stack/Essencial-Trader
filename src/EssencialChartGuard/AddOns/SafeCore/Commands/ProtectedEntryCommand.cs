namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands
{
    public sealed class ProtectedEntryCommand : TradeCommandBase
    {
        public override string CommandName { get { return "ProtectedEntry"; } }

        public CommandSide Side { get; set; }
        public int Quantity { get; set; }
        public CommandEntryType EntryType { get; set; }

        public double? EntryPrice { get; set; }

        public double? StopPrice { get; set; }
        public double? StopDistanceTicks { get; set; }

        public double? TargetPrice { get; set; }
        public double? TargetDistanceTicks { get; set; }

        public bool HasStop
        {
            get { return StopPrice.HasValue || StopDistanceTicks.HasValue; }
        }

        public bool HasTarget
        {
            get { return TargetPrice.HasValue || TargetDistanceTicks.HasValue; }
        }
    }
}

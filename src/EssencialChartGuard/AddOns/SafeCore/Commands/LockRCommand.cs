namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands
{
    public sealed class LockRCommand : TradeCommandBase
    {
        public override string CommandName { get { return "LockR"; } }

        public double RMultiple { get; set; }

        public LockRCommand()
        {
            RMultiple = 1.0;
        }
    }
}

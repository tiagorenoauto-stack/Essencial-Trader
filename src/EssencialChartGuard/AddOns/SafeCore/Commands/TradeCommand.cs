using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands
{
    public enum CommandSide
    {
        Buy,
        Sell
    }

    public enum CommandEntryType
    {
        Market,
        Limit,
        StopMarket,
        StopLimit
    }

    public interface ITradeCommand
    {
        string CommandName { get; }
        string AccountName { get; }
        string InstrumentFullName { get; }
        DateTime CreatedUtc { get; }
    }

    public abstract class TradeCommandBase : ITradeCommand
    {
        public abstract string CommandName { get; }
        public string AccountName { get; set; }
        public string InstrumentFullName { get; set; }
        public DateTime CreatedUtc { get; private set; }

        protected TradeCommandBase()
        {
            CreatedUtc = DateTime.UtcNow;
        }
    }
}

using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    public enum CommandOutcome
    {
        Accepted,
        Blocked,
        Rejected,
        Error
    }

    public sealed class CommandResult
    {
        public CommandOutcome Outcome { get; private set; }
        public string Reason { get; private set; }
        public string CommandName { get; private set; }
        public DateTime TimestampUtc { get; private set; }

        private CommandResult(CommandOutcome outcome, string commandName, string reason)
        {
            Outcome = outcome;
            CommandName = commandName ?? string.Empty;
            Reason = reason ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }

        public bool IsSuccess
        {
            get { return Outcome == CommandOutcome.Accepted; }
        }

        public static CommandResult Accepted(string commandName, string reason = null)
        {
            return new CommandResult(CommandOutcome.Accepted, commandName, reason);
        }

        public static CommandResult Blocked(string commandName, string reason)
        {
            return new CommandResult(CommandOutcome.Blocked, commandName, reason);
        }

        public static CommandResult Rejected(string commandName, string reason)
        {
            return new CommandResult(CommandOutcome.Rejected, commandName, reason);
        }

        public static CommandResult Error(string commandName, string reason)
        {
            return new CommandResult(CommandOutcome.Error, commandName, reason);
        }
    }
}

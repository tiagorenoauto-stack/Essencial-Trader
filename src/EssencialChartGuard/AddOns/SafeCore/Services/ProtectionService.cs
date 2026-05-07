using System;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    public sealed class StopChangeDecision
    {
        public CommandResult Result { get; private set; }
        public double? NewStopPrice { get; private set; }
        public string Reason { get; private set; }

        private StopChangeDecision(CommandResult result, double? newStopPrice, string reason)
        {
            Result = result;
            NewStopPrice = newStopPrice;
            Reason = reason ?? string.Empty;
        }

        public bool ShouldSubmit
        {
            get { return Result.IsSuccess && NewStopPrice.HasValue; }
        }

        public static StopChangeDecision Compute(CommandResult result, double newStopPrice, string reason)
        {
            return new StopChangeDecision(result, newStopPrice, reason);
        }

        public static StopChangeDecision Reject(CommandResult result)
        {
            return new StopChangeDecision(result, null, result.Reason);
        }
    }

    public sealed class ProtectionService
    {
        private readonly ISafeCoreLogger logger;

        public ProtectionService(ISafeCoreLogger logger)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            this.logger = logger;
        }

        public StopChangeDecision DecideBreakeven(BreakevenCommand command, ProtectionState protection)
        {
            if (command == null)
                return StopChangeDecision.Reject(CommandResult.Error("Breakeven", "command is null"));
            if (protection == null)
                return StopChangeDecision.Reject(CommandResult.Error(command.CommandName, "protection state is null"));

            if (!protection.HasOpenPosition)
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "no open position"));
            if (!protection.EntryPrice.HasValue)
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "no known entry price"));
            if (!protection.InitialStopPrice.HasValue)
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "no known initial stop"));

            double breakevenPrice = protection.EntryPrice.Value;

            logger.Protect(
                "Breakeven decided account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                " direction=" + protection.Direction +
                " entry=" + breakevenPrice.ToString("0.#####"));

            return StopChangeDecision.Compute(
                CommandResult.Accepted(command.CommandName, "breakeven computed"),
                breakevenPrice,
                "breakeven at entry");
        }

        public StopChangeDecision DecideLockR(LockRCommand command, ProtectionState protection)
        {
            if (command == null)
                return StopChangeDecision.Reject(CommandResult.Error("LockR", "command is null"));
            if (protection == null)
                return StopChangeDecision.Reject(CommandResult.Error(command.CommandName, "protection state is null"));

            if (!protection.HasOpenPosition)
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "no open position"));
            if (!protection.HasKnownInitialRisk)
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "no known initial risk"));
            if (command.RMultiple <= 0)
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "invalid R multiple"));

            double entry = protection.EntryPrice.Value;
            double initialRisk = protection.InitialRiskPerUnit.Value;

            // Always compute from initial risk (entry vs initial stop), never from current/moved stop.
            double targetStop;
            if (protection.Direction == PositionDirection.Long)
                targetStop = entry + (initialRisk * command.RMultiple);
            else if (protection.Direction == PositionDirection.Short)
                targetStop = entry - (initialRisk * command.RMultiple);
            else
                return StopChangeDecision.Reject(CommandResult.Rejected(command.CommandName, "unknown direction"));

            logger.Protect(
                "LockR decided account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                " direction=" + protection.Direction +
                " R=" + command.RMultiple.ToString("0.##") +
                " initialRisk=" + initialRisk.ToString("0.#####") +
                " targetStop=" + targetStop.ToString("0.#####"));

            return StopChangeDecision.Compute(
                CommandResult.Accepted(command.CommandName, "lockR computed"),
                targetStop,
                "lockR " + command.RMultiple.ToString("0.##"));
        }
    }
}

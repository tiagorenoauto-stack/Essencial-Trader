using System;
using System.Collections.Generic;
using System.Globalization;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    public sealed class TradeCommandService
    {
        private static readonly TimeSpan DefaultIdempotencyWindow = TimeSpan.FromMilliseconds(750);

        private readonly ISafeCoreLogger logger;
        private readonly RiskGuard riskGuard;
        private readonly ProtectionService protectionService;
        private readonly IOrderSubmitter submitter;
        private readonly Dictionary<string, DateTime> recentCommandsUtc;
        private readonly object gate;

        private TimeSpan idempotencyWindow;

        public TradeCommandService(
            ISafeCoreLogger logger,
            RiskGuard riskGuard,
            ProtectionService protectionService,
            IOrderSubmitter submitter)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            if (riskGuard == null) throw new ArgumentNullException("riskGuard");
            if (protectionService == null) throw new ArgumentNullException("protectionService");
            if (submitter == null) throw new ArgumentNullException("submitter");

            this.logger = logger;
            this.riskGuard = riskGuard;
            this.protectionService = protectionService;
            this.submitter = submitter;
            this.recentCommandsUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            this.gate = new object();
            this.idempotencyWindow = DefaultIdempotencyWindow;
        }

        public TimeSpan IdempotencyWindow
        {
            get { return idempotencyWindow; }
        }

        public string SubmitterName
        {
            get { return submitter.Name; }
        }

        public void SetIdempotencyWindow(TimeSpan window)
        {
            if (window < TimeSpan.Zero)
                throw new ArgumentException("Window must be non-negative.", "window");
            idempotencyWindow = window;
        }

        public CommandResult Submit(ProtectedEntryCommand command, ChartGuardState state)
        {
            if (command == null)
                return LogAndReturn(CommandResult.Error("ProtectedEntry", "command is null"), null);
            if (state == null)
                return LogAndReturn(CommandResult.Error(command.CommandName, "state is null"), null);

            CommandResult basic = ValidateScope(command, state);
            if (basic != null) return LogAndReturn(basic, null);

            CommandResult entryValidation = ValidateProtectedEntry(command);
            if (entryValidation != null) return LogAndReturn(entryValidation, null);

            string fingerprint = BuildFingerprint(command);
            CommandResult duplicate = CheckIdempotency(fingerprint, command.CommandName);
            if (duplicate != null) return LogAndReturn(duplicate, null);

            RiskDecision risk = riskGuard.EvaluateNewEntry(command.AccountName);
            if (!risk.AllowEntry)
            {
                logger.Risk("Entry blocked reason=" + risk.Reason);
                return LogAndReturn(CommandResult.Blocked(command.CommandName, "risk: " + risk.Reason), null);
            }

            logger.Command(
                "ProtectedEntry validated account=" + command.AccountName +
                " instrument=" + command.InstrumentFullName +
                " side=" + command.Side +
                " qty=" + command.Quantity +
                " entryType=" + command.EntryType);

            SubmitResult submitResult = submitter.SubmitProtectedEntry(command);
            CommandResult mapped = MapSubmitResult(command.CommandName, submitResult, "validated");
            if (submitResult != null && submitResult.IsSuccess)
                RegisterFingerprint(fingerprint);
            return LogAndReturn(mapped, submitResult);
        }

        public CommandResult Submit(FlattenCommand command, ChartGuardState state)
        {
            if (command == null)
                return LogAndReturn(CommandResult.Error("Flatten", "command is null"), null);
            if (state == null)
                return LogAndReturn(CommandResult.Error(command.CommandName, "state is null"), null);

            CommandResult basic = ValidateScope(command, state);
            if (basic != null) return LogAndReturn(basic, null);

            string fingerprint = BuildFingerprint(command);
            CommandResult duplicate = CheckIdempotency(fingerprint, command.CommandName);
            if (duplicate != null) return LogAndReturn(duplicate, null);

            logger.Command(
                "Flatten validated account=" + command.AccountName +
                " instrument=" + command.InstrumentFullName);

            SubmitResult submitResult = submitter.Flatten(command);
            CommandResult mapped = MapSubmitResult(command.CommandName, submitResult, "validated");
            if (submitResult != null && submitResult.IsSuccess)
                RegisterFingerprint(fingerprint);
            return LogAndReturn(mapped, submitResult);
        }

        public CommandResult Submit(CancelOrdersCommand command, ChartGuardState state)
        {
            if (command == null)
                return LogAndReturn(CommandResult.Error("CancelOrders", "command is null"), null);
            if (state == null)
                return LogAndReturn(CommandResult.Error(command.CommandName, "state is null"), null);

            CommandResult basic = ValidateScope(command, state);
            if (basic != null) return LogAndReturn(basic, null);

            string fingerprint = BuildFingerprint(command);
            CommandResult duplicate = CheckIdempotency(fingerprint, command.CommandName);
            if (duplicate != null) return LogAndReturn(duplicate, null);

            logger.Command(
                "CancelOrders validated account=" + command.AccountName +
                " instrument=" + command.InstrumentFullName);

            SubmitResult submitResult = submitter.CancelOrders(command);
            CommandResult mapped = MapSubmitResult(command.CommandName, submitResult, "validated");
            if (submitResult != null && submitResult.IsSuccess)
                RegisterFingerprint(fingerprint);
            return LogAndReturn(mapped, submitResult);
        }

        public CommandResult Submit(BreakevenCommand command, ChartGuardState state)
        {
            if (command == null)
                return LogAndReturn(CommandResult.Error("Breakeven", "command is null"), null);
            if (state == null)
                return LogAndReturn(CommandResult.Error(command.CommandName, "state is null"), null);

            CommandResult basic = ValidateScope(command, state);
            if (basic != null) return LogAndReturn(basic, null);

            string fingerprint = BuildFingerprint(command);
            CommandResult duplicate = CheckIdempotency(fingerprint, command.CommandName);
            if (duplicate != null) return LogAndReturn(duplicate, null);

            StopChangeDecision decision = protectionService.DecideBreakeven(command, state.Protection);
            return SubmitStopChangeAndLog(command, state, decision, fingerprint);
        }

        public CommandResult Submit(LockRCommand command, ChartGuardState state)
        {
            if (command == null)
                return LogAndReturn(CommandResult.Error("LockR", "command is null"), null);
            if (state == null)
                return LogAndReturn(CommandResult.Error(command.CommandName, "state is null"), null);

            CommandResult basic = ValidateScope(command, state);
            if (basic != null) return LogAndReturn(basic, null);

            string fingerprint = BuildFingerprint(command);
            CommandResult duplicate = CheckIdempotency(fingerprint, command.CommandName);
            if (duplicate != null) return LogAndReturn(duplicate, null);

            StopChangeDecision decision = protectionService.DecideLockR(command, state.Protection);
            return SubmitStopChangeAndLog(command, state, decision, fingerprint);
        }

        private CommandResult SubmitStopChangeAndLog(
            ITradeCommand command,
            ChartGuardState state,
            StopChangeDecision decision,
            string fingerprint)
        {
            if (!decision.ShouldSubmit)
                return LogAndReturn(decision.Result, null);

            StopModificationRequest request = new StopModificationRequest(
                command.CommandName,
                command.AccountName,
                command.InstrumentFullName,
                state.Protection.Direction,
                decision.NewStopPrice.Value,
                decision.Reason);

            SubmitResult submitResult = submitter.ModifyStop(request);
            CommandResult mapped = MapSubmitResult(command.CommandName, submitResult, decision.Result.Reason);
            if (submitResult != null && submitResult.IsSuccess)
                RegisterFingerprint(fingerprint);
            return LogAndReturn(mapped, submitResult);
        }

        private static CommandResult MapSubmitResult(string commandName, SubmitResult submitResult, string acceptedReason)
        {
            if (submitResult == null)
                return CommandResult.Error(commandName, "submitter returned null");

            string suffix = string.IsNullOrEmpty(submitResult.ReferenceId)
                ? string.Empty
                : " ref=" + submitResult.ReferenceId;

            switch (submitResult.Outcome)
            {
                case SubmitOutcome.Submitted:
                    return CommandResult.Accepted(
                        commandName,
                        acceptedReason + "; submitter=" + submitResult.SubmitterName + " mode=submitted" + suffix);
                case SubmitOutcome.Simulated:
                    return CommandResult.Accepted(
                        commandName,
                        "simulated by " + submitResult.SubmitterName + "; no NinjaTrader order submitted" + suffix);
                case SubmitOutcome.Skipped:
                    return CommandResult.Blocked(
                        commandName,
                        "submitter skipped: " + submitResult.Reason);
                case SubmitOutcome.Error:
                default:
                    return CommandResult.Error(
                        commandName,
                        "submitter error: " + submitResult.Reason);
            }
        }

        private static CommandResult ValidateScope(ITradeCommand command, ChartGuardState state)
        {
            if (string.IsNullOrWhiteSpace(command.AccountName))
                return CommandResult.Rejected(command.CommandName, "account is missing");
            if (string.IsNullOrWhiteSpace(command.InstrumentFullName))
                return CommandResult.Rejected(command.CommandName, "instrument is missing");

            if (!string.IsNullOrWhiteSpace(state.AccountName)
                && !string.Equals(state.AccountName, command.AccountName, StringComparison.Ordinal))
                return CommandResult.Rejected(
                    command.CommandName,
                    "account mismatch: command=" + command.AccountName + " state=" + state.AccountName);

            if (!string.IsNullOrWhiteSpace(state.InstrumentFullName)
                && !string.Equals(state.InstrumentFullName, command.InstrumentFullName, StringComparison.Ordinal))
                return CommandResult.Rejected(
                    command.CommandName,
                    "instrument mismatch: command=" + command.InstrumentFullName + " state=" + state.InstrumentFullName);

            return null;
        }

        private static CommandResult ValidateProtectedEntry(ProtectedEntryCommand command)
        {
            if (command.Quantity < 1)
                return CommandResult.Rejected(command.CommandName, "quantity less than 1");

            if (!command.HasStop)
                return CommandResult.Rejected(command.CommandName, "protected entry requires a stop");

            // Stop precedence rule: explicit price and tick distance must not be supplied together
            // because we have not defined a precedence yet. Rejecting now avoids ambiguity at submit time.
            if (command.StopPrice.HasValue && command.StopDistanceTicks.HasValue)
                return CommandResult.Rejected(command.CommandName, "stop ambiguous: both StopPrice and StopDistanceTicks were supplied");

            if (command.StopDistanceTicks.HasValue && command.StopDistanceTicks.Value <= 0)
                return CommandResult.Rejected(command.CommandName, "StopDistanceTicks must be greater than 0");

            // Same precedence rule applied to optional target.
            if (command.TargetPrice.HasValue && command.TargetDistanceTicks.HasValue)
                return CommandResult.Rejected(command.CommandName, "target ambiguous: both TargetPrice and TargetDistanceTicks were supplied");

            if (command.TargetDistanceTicks.HasValue && command.TargetDistanceTicks.Value <= 0)
                return CommandResult.Rejected(command.CommandName, "TargetDistanceTicks must be greater than 0");

            // Limit-style entries require an explicit entry price.
            if ((command.EntryType == CommandEntryType.Limit || command.EntryType == CommandEntryType.StopLimit)
                && !command.EntryPrice.HasValue)
                return CommandResult.Rejected(command.CommandName, "entry price required for " + command.EntryType);

            return null;
        }

        private static string BuildFingerprint(ITradeCommand command)
        {
            ProtectedEntryCommand entry = command as ProtectedEntryCommand;
            if (entry != null)
            {
                return string.Join(
                    "|",
                    entry.CommandName,
                    entry.AccountName ?? string.Empty,
                    entry.InstrumentFullName ?? string.Empty,
                    entry.Side.ToString(),
                    entry.Quantity.ToString(CultureInfo.InvariantCulture),
                    entry.EntryType.ToString(),
                    NullableToString(entry.EntryPrice),
                    NullableToString(entry.StopPrice),
                    NullableToString(entry.StopDistanceTicks),
                    NullableToString(entry.TargetPrice),
                    NullableToString(entry.TargetDistanceTicks));
            }

            LockRCommand lockr = command as LockRCommand;
            if (lockr != null)
            {
                return string.Join(
                    "|",
                    lockr.CommandName,
                    lockr.AccountName ?? string.Empty,
                    lockr.InstrumentFullName ?? string.Empty,
                    lockr.RMultiple.ToString("0.####", CultureInfo.InvariantCulture));
            }

            return string.Join(
                "|",
                command.CommandName,
                command.AccountName ?? string.Empty,
                command.InstrumentFullName ?? string.Empty);
        }

        private CommandResult CheckIdempotency(string fingerprint, string commandName)
        {
            if (idempotencyWindow <= TimeSpan.Zero) return null;
            DateTime nowUtc = DateTime.UtcNow;

            lock (gate)
            {
                PruneExpired(nowUtc);

                DateTime previousUtc;
                if (recentCommandsUtc.TryGetValue(fingerprint, out previousUtc))
                {
                    TimeSpan elapsed = nowUtc - previousUtc;
                    return CommandResult.Blocked(
                        commandName,
                        "duplicate within " + idempotencyWindow.TotalMilliseconds.ToString("0") +
                        "ms (elapsed=" + elapsed.TotalMilliseconds.ToString("0") + "ms)");
                }
            }

            return null;
        }

        // Only successful executions/simulations register a fingerprint. Rejected payloads, risk
        // blocks, invalid protection decisions, and submitter errors must not block a follow-up
        // valid retry within the window.
        private void RegisterFingerprint(string fingerprint)
        {
            DateTime nowUtc = DateTime.UtcNow;
            lock (gate)
            {
                recentCommandsUtc[fingerprint] = nowUtc;
            }
        }

        private void PruneExpired(DateTime nowUtc)
        {
            if (recentCommandsUtc.Count == 0) return;
            List<string> expired = null;
            foreach (KeyValuePair<string, DateTime> kvp in recentCommandsUtc)
            {
                if ((nowUtc - kvp.Value) > idempotencyWindow)
                {
                    if (expired == null) expired = new List<string>();
                    expired.Add(kvp.Key);
                }
            }
            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                    recentCommandsUtc.Remove(expired[i]);
            }
        }

        private static string NullableToString(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.#####", CultureInfo.InvariantCulture) : "-";
        }

        private CommandResult LogAndReturn(CommandResult result, SubmitResult submitResult)
        {
            string submitterTag;
            if (submitResult == null)
            {
                submitterTag = " submitter=- mode=- ref=-";
            }
            else
            {
                string mode = submitResult.Outcome.ToString().ToLowerInvariant();
                string refId = string.IsNullOrEmpty(submitResult.ReferenceId) ? "-" : submitResult.ReferenceId;
                string submitterName = string.IsNullOrEmpty(submitResult.SubmitterName) ? "-" : submitResult.SubmitterName;
                submitterTag = " submitter=" + submitterName + " mode=" + mode + " ref=" + refId;
            }

            logger.Command(
                "Result " + result.CommandName +
                " outcome=" + result.Outcome +
                submitterTag +
                " reason=" + (string.IsNullOrEmpty(result.Reason) ? "-" : result.Reason));
            return result;
        }
    }
}

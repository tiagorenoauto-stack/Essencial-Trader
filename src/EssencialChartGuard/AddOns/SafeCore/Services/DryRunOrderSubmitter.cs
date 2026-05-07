using System;
using System.Globalization;
using System.Threading;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    // Dry-run execution port. Logs the intent under the auditable prefixes ([EssencialCommand],
    // [EssencialProtect]) and never calls any NinjaTrader API. Used as the default until a
    // NinjaTrader-backed submitter is implemented and validated through the manual checklist.
    public sealed class DryRunOrderSubmitter : IOrderSubmitter
    {
        private const string DryRunSuffix = " (dry run; no NinjaTrader order submitted)";

        private readonly ISafeCoreLogger logger;
        private long counter;

        public DryRunOrderSubmitter(ISafeCoreLogger logger)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            this.logger = logger;
        }

        public string Name
        {
            get { return "DryRun"; }
        }

        public SubmitResult SubmitProtectedEntry(ProtectedEntryCommand command)
        {
            if (command == null) return SubmitResult.Error(Name, "command is null");

            string referenceId = NextReferenceId("entry");
            logger.Command(
                "DryRun SubmitProtectedEntry id=" + referenceId +
                " account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                " side=" + command.Side +
                " qty=" + command.Quantity.ToString(CultureInfo.InvariantCulture) +
                " entryType=" + command.EntryType +
                " entryPrice=" + Format(command.EntryPrice) +
                " stopPrice=" + Format(command.StopPrice) +
                " stopTicks=" + Format(command.StopDistanceTicks) +
                " targetPrice=" + Format(command.TargetPrice) +
                " targetTicks=" + Format(command.TargetDistanceTicks) +
                DryRunSuffix);

            return SubmitResult.Simulated(Name, referenceId, "dry run");
        }

        public SubmitResult Flatten(FlattenCommand command)
        {
            if (command == null) return SubmitResult.Error(Name, "command is null");

            string referenceId = NextReferenceId("flat");
            logger.Command(
                "DryRun Flatten id=" + referenceId +
                " account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                DryRunSuffix);

            return SubmitResult.Simulated(Name, referenceId, "dry run");
        }

        public SubmitResult CancelOrders(CancelOrdersCommand command)
        {
            if (command == null) return SubmitResult.Error(Name, "command is null");

            string referenceId = NextReferenceId("cxl");
            logger.Command(
                "DryRun CancelOrders id=" + referenceId +
                " account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                DryRunSuffix);

            return SubmitResult.Simulated(Name, referenceId, "dry run");
        }

        public SubmitResult ModifyStop(StopModificationRequest request)
        {
            if (request == null) return SubmitResult.Error(Name, "request is null");

            string referenceId = NextReferenceId("stop");
            logger.Protect(
                "DryRun ModifyStop id=" + referenceId +
                " source=" + request.SourceCommandName +
                " account=" + request.AccountName +
                " instrument=" + request.InstrumentFullName +
                " direction=" + request.Direction +
                " newStop=" + request.NewStopPrice.ToString("0.#####", CultureInfo.InvariantCulture) +
                " reason=" + (string.IsNullOrEmpty(request.Reason) ? "-" : request.Reason) +
                DryRunSuffix);

            return SubmitResult.Simulated(Name, referenceId, "dry run");
        }

        private string NextReferenceId(string kind)
        {
            long n = Interlocked.Increment(ref counter);
            return "dry-" + kind + "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        private static string Format(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.#####", CultureInfo.InvariantCulture) : "-";
        }
    }
}

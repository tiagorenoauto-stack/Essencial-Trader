using System;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    // Execution port. The Safe Core decides; an IOrderSubmitter executes (or simulates execution).
    // No NinjaTrader API may leak through this interface. A future NinjaTrader-backed implementation
    // must live behind this contract; production code must depend on the interface, not on a concrete type.
    public interface IOrderSubmitter
    {
        string Name { get; }

        SubmitResult SubmitProtectedEntry(ProtectedEntryCommand command);

        SubmitResult Flatten(FlattenCommand command);

        SubmitResult CancelOrders(CancelOrdersCommand command);

        SubmitResult ModifyStop(StopModificationRequest request);
    }

    public sealed class StopModificationRequest
    {
        public string SourceCommandName { get; private set; }
        public string AccountName { get; private set; }
        public string InstrumentFullName { get; private set; }
        public PositionDirection Direction { get; private set; }
        public double NewStopPrice { get; private set; }
        public string Reason { get; private set; }
        public DateTime CreatedUtc { get; private set; }

        public StopModificationRequest(
            string sourceCommandName,
            string accountName,
            string instrumentFullName,
            PositionDirection direction,
            double newStopPrice,
            string reason)
        {
            SourceCommandName = sourceCommandName ?? string.Empty;
            AccountName = accountName ?? string.Empty;
            InstrumentFullName = instrumentFullName ?? string.Empty;
            Direction = direction;
            NewStopPrice = newStopPrice;
            Reason = reason ?? string.Empty;
            CreatedUtc = DateTime.UtcNow;
        }
    }

    // Submitted: a real NinjaTrader-backed submitter actually placed/cancelled/modified an order.
    // Simulated: the core accepted the action but no NinjaTrader order was sent (e.g. DryRun).
    // Skipped: the submitter intentionally did not act (e.g. nothing to flatten).
    // Error: the submitter could not act because of an internal failure.
    public enum SubmitOutcome
    {
        Submitted,
        Simulated,
        Skipped,
        Error
    }

    public sealed class SubmitResult
    {
        public SubmitOutcome Outcome { get; private set; }
        public string Reason { get; private set; }
        public string SubmitterName { get; private set; }
        public string ReferenceId { get; private set; }
        public DateTime TimestampUtc { get; private set; }

        private SubmitResult(SubmitOutcome outcome, string submitterName, string reason, string referenceId)
        {
            Outcome = outcome;
            SubmitterName = submitterName ?? string.Empty;
            Reason = reason ?? string.Empty;
            ReferenceId = referenceId ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }

        // True when the action was accepted by the port, regardless of whether it was a real
        // submission or a simulation. Use Outcome to distinguish Submitted vs Simulated.
        public bool IsSuccess
        {
            get
            {
                return Outcome == SubmitOutcome.Submitted
                    || Outcome == SubmitOutcome.Simulated;
            }
        }

        public static SubmitResult Submitted(string submitterName, string referenceId = null, string reason = null)
        {
            return new SubmitResult(SubmitOutcome.Submitted, submitterName, reason, referenceId);
        }

        public static SubmitResult Simulated(string submitterName, string referenceId = null, string reason = null)
        {
            return new SubmitResult(SubmitOutcome.Simulated, submitterName, reason, referenceId);
        }

        public static SubmitResult Skipped(string submitterName, string reason)
        {
            return new SubmitResult(SubmitOutcome.Skipped, submitterName, reason, null);
        }

        public static SubmitResult Error(string submitterName, string reason)
        {
            return new SubmitResult(SubmitOutcome.Error, submitterName, reason, null);
        }
    }
}

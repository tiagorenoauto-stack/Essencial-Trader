using System;
using System.Globalization;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Commands;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    // First real-submit route. Scope for this iteration:
    //   * SubmitProtectedEntry, EntryType == Market, with required stop, no target,
    //     on a simulation/playback account -> real Submitted.
    //   * Any other entry type -> Skipped ("entry type not implemented for real submit yet").
    //   * Target supplied -> Skipped ("target not implemented for first real submit route").
    //   * Live (non sim/playback) account -> Skipped ("first real submit route is limited to simulation/playback").
    //   * Flatten / CancelOrders / ModifyStop -> Skipped (not implemented for real submit yet).
    //
    // The submitter never resolves accounts itself; it asks INinjaTraderAccountResolver for an
    // adapter that already targets the requested account+instrument. The adapter is the only
    // place in the project that talks to NinjaTrader.Cbi / Instrument / OrderAction. If the
    // resolver returns null, or returns an adapter whose scope does not match the command,
    // the submitter aborts with Skipped/Error -- it never falls back silently to another scope.
    public sealed class NinjaTraderOrderSubmitter : IOrderSubmitter
    {
        private const string EntryTypeNotImplemented = "entry type not implemented for real submit yet";
        private const string TargetNotImplemented = "target not implemented for first real submit route";
        private const string SimOnly = "first real submit route is limited to simulation/playback";
        private const string ActionNotImplemented = "not implemented for real submit yet";

        private readonly ISafeCoreLogger logger;
        private readonly INinjaTraderAccountResolver resolver;

        public NinjaTraderOrderSubmitter(ISafeCoreLogger logger, INinjaTraderAccountResolver resolver)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            if (resolver == null) throw new ArgumentNullException("resolver");
            this.logger = logger;
            this.resolver = resolver;
        }

        public string Name
        {
            get { return "NinjaTrader"; }
        }

        public SubmitResult SubmitProtectedEntry(ProtectedEntryCommand command)
        {
            if (command == null) return SubmitResult.Error(Name, "command is null");

            // Re-validate inside the submitter -- the core already did, but real submit must
            // never trust its caller. These checks duplicate the core ones on purpose.
            if (string.IsNullOrWhiteSpace(command.AccountName))
                return SubmitResult.Error(Name, "account is missing");
            if (string.IsNullOrWhiteSpace(command.InstrumentFullName))
                return SubmitResult.Error(Name, "instrument is missing");
            if (command.Quantity < 1)
                return SubmitResult.Error(Name, "quantity less than 1");
            if (command.EntryType != CommandEntryType.Market)
            {
                LogAttempt(command, computedStopPrice: null, mode: "skipped");
                logger.Command("Real submit skipped: " + EntryTypeNotImplemented +
                    " entryType=" + command.EntryType);
                return SubmitResult.Skipped(Name, EntryTypeNotImplemented);
            }
            if (!command.HasStop)
                return SubmitResult.Error(Name, "protected entry requires a stop");
            if (command.StopPrice.HasValue && command.StopDistanceTicks.HasValue)
                return SubmitResult.Error(Name, "stop ambiguous: both StopPrice and StopDistanceTicks");
            if (command.StopDistanceTicks.HasValue && command.StopDistanceTicks.Value <= 0)
                return SubmitResult.Error(Name, "StopDistanceTicks must be greater than 0");

            // Target is not part of the first real route. Block before any resolution work,
            // do not silently drop it. Entry must not be sent when a target was requested.
            if (command.HasTarget)
            {
                LogAttempt(command, computedStopPrice: null, mode: "skipped");
                logger.Command("Real submit skipped: " + TargetNotImplemented +
                    " targetPrice=" + Format(command.TargetPrice) +
                    " targetTicks=" + Format(command.TargetDistanceTicks));
                return SubmitResult.Skipped(Name, TargetNotImplemented);
            }

            INinjaTraderAccountAdapter adapter = resolver.Resolve(command.AccountName, command.InstrumentFullName);
            if (adapter == null)
            {
                LogAttempt(command, computedStopPrice: null, mode: "skipped");
                logger.Command("Real submit skipped: account/instrument could not be resolved");
                return SubmitResult.Skipped(Name, "account/instrument could not be resolved");
            }
            if (!string.Equals(adapter.AccountName, command.AccountName, StringComparison.Ordinal))
                return SubmitResult.Error(Name,
                    "resolver returned different account: requested=" + command.AccountName +
                    " resolved=" + (adapter.AccountName ?? "?"));
            if (!string.Equals(adapter.InstrumentFullName, command.InstrumentFullName, StringComparison.Ordinal))
                return SubmitResult.Error(Name,
                    "resolver returned different instrument: requested=" + command.InstrumentFullName +
                    " resolved=" + (adapter.InstrumentFullName ?? "?"));
            if (!adapter.IsConnected)
            {
                LogAttempt(command, computedStopPrice: null, mode: "skipped");
                logger.Command("Real submit skipped: account is not connected");
                return SubmitResult.Skipped(Name, "account is not connected");
            }
            if (!adapter.IsSimulationOrPlayback)
            {
                LogAttempt(command, computedStopPrice: null, mode: "skipped");
                logger.Command("Real submit skipped: " + SimOnly +
                    " account=" + adapter.AccountName);
                return SubmitResult.Skipped(Name, SimOnly);
            }

            // Capture reference price once; reuse for stop computation and coherence check.
            double? referencePrice = adapter.GetReferencePriceForMarketEntry();

            double? computedStop = ComputeStopPrice(command, adapter, referencePrice);
            if (!computedStop.HasValue)
            {
                LogAttempt(command, computedStopPrice: null, mode: "skipped");
                logger.Command("Real submit skipped: no reliable reference price to compute stop from ticks");
                return SubmitResult.Skipped(Name,
                    "no reliable reference price to compute stop from ticks");
            }

            double stopPrice = adapter.RoundToTick(computedStop.Value);

            string coherenceError = StopCoherenceCheck(command.Side, stopPrice, referencePrice);
            if (coherenceError != null)
                return SubmitResult.Error(Name, coherenceError);

            LogAttempt(command, computedStopPrice: stopPrice, mode: "submitting");

            MarketEntryRequest request = new MarketEntryRequest(
                adapter.AccountName,
                adapter.InstrumentFullName,
                command.Side == CommandSide.Buy,
                command.Quantity,
                stopPrice);

            SubmitMarketEntryResult adapterResult;
            try
            {
                adapterResult = adapter.SubmitMarketEntryWithStop(request);
            }
            catch (Exception ex)
            {
                logger.Command("Real submit error account=" + adapter.AccountName +
                    " instrument=" + adapter.InstrumentFullName +
                    " ex=" + ex.GetType().Name + ":" + ex.Message);
                return SubmitResult.Error(Name, "adapter exception: " + ex.Message);
            }

            if (adapterResult == null)
                return SubmitResult.Error(Name, "adapter returned null");
            if (!adapterResult.Success)
            {
                logger.Command("Real submit failed reason=" + adapterResult.ErrorReason);
                return SubmitResult.Error(Name, adapterResult.ErrorReason);
            }

            string referenceId = string.IsNullOrEmpty(adapterResult.ReferenceId)
                ? "nt-entry"
                : adapterResult.ReferenceId;

            logger.Command(
                "Real submit OK account=" + adapter.AccountName +
                " instrument=" + adapter.InstrumentFullName +
                " side=" + command.Side +
                " qty=" + command.Quantity +
                " stop=" + stopPrice.ToString("0.#####", CultureInfo.InvariantCulture) +
                " ref=" + referenceId);

            return SubmitResult.Submitted(Name, referenceId, "market entry with attached stop");
        }

        public SubmitResult Flatten(FlattenCommand command)
        {
            if (command == null) return SubmitResult.Error(Name, "command is null");
            logger.Command(
                "Real Flatten skipped account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                " reason=" + ActionNotImplemented);
            return SubmitResult.Skipped(Name, ActionNotImplemented);
        }

        public SubmitResult CancelOrders(CancelOrdersCommand command)
        {
            if (command == null) return SubmitResult.Error(Name, "command is null");
            logger.Command(
                "Real CancelOrders skipped account=" + (command.AccountName ?? "?") +
                " instrument=" + (command.InstrumentFullName ?? "?") +
                " reason=" + ActionNotImplemented);
            return SubmitResult.Skipped(Name, ActionNotImplemented);
        }

        public SubmitResult ModifyStop(StopModificationRequest request)
        {
            if (request == null) return SubmitResult.Error(Name, "request is null");
            logger.Protect(
                "Real ModifyStop skipped source=" + request.SourceCommandName +
                " account=" + request.AccountName +
                " instrument=" + request.InstrumentFullName +
                " reason=" + ActionNotImplemented);
            return SubmitResult.Skipped(Name, ActionNotImplemented);
        }

        private void LogAttempt(ProtectedEntryCommand command, double? computedStopPrice, string mode)
        {
            logger.Command(
                "Real submit attempt mode=" + mode +
                " account=" + command.AccountName +
                " instrument=" + command.InstrumentFullName +
                " side=" + command.Side +
                " qty=" + command.Quantity +
                " entryType=" + command.EntryType +
                " stopPrice=" + Format(command.StopPrice) +
                " stopTicks=" + Format(command.StopDistanceTicks) +
                " computedStop=" + Format(computedStopPrice) +
                " targetPrice=" + Format(command.TargetPrice) +
                " targetTicks=" + Format(command.TargetDistanceTicks));
        }

        private static double? ComputeStopPrice(
            ProtectedEntryCommand command,
            INinjaTraderAccountAdapter adapter,
            double? referencePrice)
        {
            if (command.StopPrice.HasValue)
                return command.StopPrice.Value;

            if (!command.StopDistanceTicks.HasValue) return null;

            double tickSize = adapter.TickSize;
            if (tickSize <= 0) return null;

            if (!referencePrice.HasValue) return null;

            double offset = command.StopDistanceTicks.Value * tickSize;
            return command.Side == CommandSide.Buy
                ? referencePrice.Value - offset
                : referencePrice.Value + offset;
        }

        private static string StopCoherenceCheck(CommandSide side, double stopPrice, double? referencePrice)
        {
            if (!referencePrice.HasValue) return null;

            if (side == CommandSide.Buy && stopPrice >= referencePrice.Value)
                return "stop not coherent with side: buy stop=" + stopPrice.ToString("0.#####", CultureInfo.InvariantCulture) +
                    " ref=" + referencePrice.Value.ToString("0.#####", CultureInfo.InvariantCulture);
            if (side == CommandSide.Sell && stopPrice <= referencePrice.Value)
                return "stop not coherent with side: sell stop=" + stopPrice.ToString("0.#####", CultureInfo.InvariantCulture) +
                    " ref=" + referencePrice.Value.ToString("0.#####", CultureInfo.InvariantCulture);
            return null;
        }

        private static string Format(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.#####", CultureInfo.InvariantCulture) : "-";
        }
    }
}

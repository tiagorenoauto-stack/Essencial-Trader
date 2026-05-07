using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge
{
    // Concrete adapter that bridges INinjaTraderAccountAdapter to the real NinjaTrader API.
    // This file is the only place in the project (together with the resolver) that imports
    // NinjaTrader.Cbi / NinjaTrader.Data trading types. The Safe Core must never reference
    // these types directly.
    //
    // Status: EXPERIMENTAL / BLOCKED.
    //   The real submit path is disabled by default. SubmitMarketEntryWithStop returns Fail with
    //   reason "real bridge disabled pending entry-stop lifecycle validation" until someone
    //   explicitly calls EnableForControlledTest(...) with the documented acknowledgement string.
    //   Even then it is restricted to Sim/Playback (enforced upstream) and to a single Replay
    //   session. The bridge must not be enabled in any UI flow, AddOn host, or automation until
    //   the entry-stop lifecycle has been validated against real OrderUpdate/ExecutionUpdate
    //   events through OrderEventRouter, or until a safer bracket/ATM mechanism is in place.
    //
    // Scope when enabled:
    //   * Market entry with a separately-submitted protective StopMarket order.
    //   * Simulation/Playback accounts only (enforced upstream by NinjaTraderOrderSubmitter).
    //   * No target. No ATM.
    //
    // Risk -- OCO between entry and stop is NOT a bracket parent/child relationship:
    //   We submit entry and stop in a single Account.Submit call and tag them with a shared
    //   OCO id. We do NOT assume the stop is parented to the entry. Two failure modes are
    //   plausible and must be confirmed in Replay before enabling for any non-controlled run:
    //     - The stop may be cancelled by OCO when the entry fills (the position would then
    //       have no protection -- this would invert the safety guarantee).
    //     - If the entry is Rejected, a working stop may remain orphan in the book.
    //   The bridge does not yet listen to OrderUpdate to react to either case. Until that
    //   wiring exists, this code path is unsafe to use beyond a single observation session.
    public sealed class NinjaTraderAccountAdapter : INinjaTraderAccountAdapter
    {
        // Acknowledgement string required to flip the safety switch. Kept verbose on purpose:
        // anyone enabling the bridge must read what they are agreeing to.
        public const string EnableAcknowledgement =
            "I have read ninjatrader-bridge-notes.md and accept the entry-stop lifecycle risk for one Replay session";

        private const string DisabledReason =
            "real bridge disabled pending entry-stop lifecycle validation";

        private readonly ISafeCoreLogger logger;
        private readonly Account account;
        private readonly Instrument instrument;
        private long submissionCounter;
        private bool enabled;

        public NinjaTraderAccountAdapter(ISafeCoreLogger logger, Account account, Instrument instrument)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            if (account == null) throw new ArgumentNullException("account");
            if (instrument == null) throw new ArgumentNullException("instrument");
            if (instrument.MasterInstrument == null)
                throw new ArgumentException("instrument.MasterInstrument is null", "instrument");

            this.logger = logger;
            this.account = account;
            this.instrument = instrument;
            this.enabled = false;
        }

        public bool IsEnabledForRealSubmit
        {
            get { return enabled; }
        }

        // Explicit, audited switch. Default is disabled. Caller must pass the exact
        // EnableAcknowledgement string and is logged. Use only inside a manually supervised
        // Replay test session that follows ninjatrader-bridge-notes.md and the manual
        // checklist. Disable() restores the safe default.
        public bool EnableForControlledTest(string acknowledgement)
        {
            if (!string.Equals(acknowledgement, EnableAcknowledgement, StringComparison.Ordinal))
            {
                logger.Order("Bridge enable refused: acknowledgement mismatch");
                return false;
            }
            enabled = true;
            logger.Order(
                "Bridge enabled for controlled test account=" + AccountName +
                " instrument=" + InstrumentFullName +
                " -- entry-stop lifecycle risk acknowledged");
            return true;
        }

        public void Disable()
        {
            if (enabled)
                logger.Order("Bridge disabled account=" + AccountName +
                    " instrument=" + InstrumentFullName);
            enabled = false;
        }

        public string AccountName
        {
            get { return account.Name ?? string.Empty; }
        }

        public string InstrumentFullName
        {
            get { return instrument.FullName ?? string.Empty; }
        }

        public bool IsConnected
        {
            get
            {
                Connection connection = account.Connection;
                return connection != null
                    && connection.Status == ConnectionStatus.Connected;
            }
        }

        // Conservative classification: only Simulation/Playback connections count.
        // Provider enum values differ across NT8 builds, so compare provider/name strings
        // instead of referencing version-specific enum members. Anything else is
        // treated as live -- the upstream submitter then refuses to act.
        public bool IsSimulationOrPlayback
        {
            get
            {
                Connection connection = account.Connection;
                if (connection == null || connection.Options == null) return false;

                string providerName = connection.Options.Provider.ToString();
                if (providerName.IndexOf("Replay", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (providerName.IndexOf("Playback", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (providerName.IndexOf("Sim", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (providerName.IndexOf("Simulator", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                string connectionName = connection.Options.Name ?? string.Empty;
                if (connectionName.IndexOf("Playback", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (connectionName.IndexOf("Sim", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                return false;
            }
        }

        public double TickSize
        {
            get
            {
                MasterInstrument master = instrument.MasterInstrument;
                if (master == null) return 0.0;
                double tick = master.TickSize;
                return tick > 0 ? tick : 0.0;
            }
        }

        public double? GetReferencePriceForMarketEntry()
        {
            try
            {
                MarketData md = instrument.MarketData;
                if (md == null) return null;

                var last = md.Last;
                if (last != null && IsUsablePrice(last.Price))
                    return last.Price;

                // Last is the most reliable anchor for a Market entry. We deliberately do not
                // fall back to bid/ask/close to keep the first real route conservative -- if
                // Last is unavailable, the submitter must Skip rather than guess.
                return null;
            }
            catch
            {
                // MarketData access can throw during connection transitions. Conservative: null.
                return null;
            }
        }

        public double RoundToTick(double price)
        {
            MasterInstrument master = instrument.MasterInstrument;
            if (master == null) return price;
            return master.RoundToTickSize(price);
        }

        public SubmitMarketEntryResult SubmitMarketEntryWithStop(MarketEntryRequest request)
        {
            if (request == null) return SubmitMarketEntryResult.Fail("request is null");

            // Hard kill-switch. Default is disabled; until the entry-stop lifecycle is validated
            // (or a safer bracket/ATM mechanism is in place), the real path must not run.
            if (!enabled)
            {
                logger.Order("Bridge refused submit: " + DisabledReason +
                    " account=" + AccountName +
                    " instrument=" + InstrumentFullName);
                return SubmitMarketEntryResult.Fail(DisabledReason);
            }

            // Re-validate scope inside the adapter -- the submitter already did, but a real
            // order path must never trust its caller. These checks duplicate on purpose.
            if (!string.Equals(request.AccountName, AccountName, StringComparison.Ordinal))
                return SubmitMarketEntryResult.Fail(
                    "account mismatch: request=" + request.AccountName + " adapter=" + AccountName);
            if (!string.Equals(request.InstrumentFullName, InstrumentFullName, StringComparison.Ordinal))
                return SubmitMarketEntryResult.Fail(
                    "instrument mismatch: request=" + request.InstrumentFullName + " adapter=" + InstrumentFullName);
            if (request.Quantity < 1)
                return SubmitMarketEntryResult.Fail("quantity less than 1");
            if (!IsUsablePrice(request.StopPrice))
                return SubmitMarketEntryResult.Fail("stop price not usable: " + request.StopPrice);
            if (!IsConnected)
                return SubmitMarketEntryResult.Fail("account not connected");
            if (!IsSimulationOrPlayback)
                return SubmitMarketEntryResult.Fail("account is not simulation/playback");

            // Stop/side coherence sanity check inside the adapter. The submitter already
            // checked this against its captured reference price; we re-check against the
            // current Last to refuse obviously broken stops at the boundary.
            double? referenceNow = GetReferencePriceForMarketEntry();
            if (referenceNow.HasValue)
            {
                if (request.IsBuy && request.StopPrice >= referenceNow.Value)
                    return SubmitMarketEntryResult.Fail(
                        "stop not coherent: buy stop=" + Format(request.StopPrice) +
                        " ref=" + Format(referenceNow.Value));
                if (!request.IsBuy && request.StopPrice <= referenceNow.Value)
                    return SubmitMarketEntryResult.Fail(
                        "stop not coherent: sell stop=" + Format(request.StopPrice) +
                        " ref=" + Format(referenceNow.Value));
            }

            string submissionId = NextSubmissionId();
            string entryName = "ECG-Entry-" + submissionId;
            string stopName = "ECG-Stop-" + submissionId;
            string ocoId = "ECG-OCO-" + submissionId;

            OrderAction entryAction = request.IsBuy ? OrderAction.Buy : OrderAction.SellShort;
            OrderAction stopAction = request.IsBuy ? OrderAction.Sell : OrderAction.BuyToCover;

            Order entryOrder;
            Order stopOrder;

            try
            {
                // CreateOrder builds Order objects detached from the account; Submit then sends
                // them in one batch. The shared ocoId only marks them as part of the same OCO
                // group from NinjaTrader's perspective -- it does NOT mean the stop is a child
                // of the entry. The risk that the stop is cancelled when the entry fills, or
                // that it survives an entry rejection, must be observed in Replay before this
                // path can be used outside a single controlled session.
                entryOrder = account.CreateOrder(
                    instrument,
                    entryAction,
                    OrderType.Market,
                    OrderEntry.Manual,
                    TimeInForce.Day,
                    request.Quantity,
                    /*limitPrice*/ 0.0,
                    /*stopPrice*/ 0.0,
                    /*ocoId*/ ocoId,
                    entryName,
                    /*gtd*/ Core.Globals.MaxDate,
                    /*customOrder*/ null);

                stopOrder = account.CreateOrder(
                    instrument,
                    stopAction,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Day,
                    request.Quantity,
                    /*limitPrice*/ 0.0,
                    /*stopPrice*/ request.StopPrice,
                    /*ocoId*/ ocoId,
                    stopName,
                    /*gtd*/ Core.Globals.MaxDate,
                    /*customOrder*/ null);
            }
            catch (Exception ex)
            {
                logger.Order("Bridge CreateOrder failed account=" + AccountName +
                    " instrument=" + InstrumentFullName +
                    " ex=" + ex.GetType().Name + ":" + ex.Message);
                return SubmitMarketEntryResult.Fail("CreateOrder exception: " + ex.Message);
            }

            if (entryOrder == null || stopOrder == null)
            {
                logger.Order("Bridge CreateOrder returned null entry=" +
                    (entryOrder == null ? "null" : "ok") +
                    " stop=" + (stopOrder == null ? "null" : "ok") +
                    " -- nothing submitted");
                return SubmitMarketEntryResult.Fail("CreateOrder returned null; nothing submitted");
            }

            try
            {
                List<Order> batch = new List<Order>(2);
                batch.Add(entryOrder);
                batch.Add(stopOrder);
                account.Submit(batch);
            }
            catch (Exception ex)
            {
                logger.Order("Bridge Submit failed account=" + AccountName +
                    " instrument=" + InstrumentFullName +
                    " ex=" + ex.GetType().Name + ":" + ex.Message);
                return SubmitMarketEntryResult.Fail("Submit exception: " + ex.Message);
            }

            string entryOrderId = SafeOrderId(entryOrder);
            string stopOrderId = SafeOrderId(stopOrder);

            logger.Order(
                "Bridge submitted account=" + AccountName +
                " instrument=" + InstrumentFullName +
                " side=" + (request.IsBuy ? "Buy" : "Sell") +
                " qty=" + request.Quantity.ToString(CultureInfo.InvariantCulture) +
                " stop=" + Format(request.StopPrice) +
                " entryName=" + entryName +
                " entryOrderId=" + entryOrderId +
                " stopName=" + stopName +
                " stopOrderId=" + stopOrderId +
                " oco=" + ocoId);

            return SubmitMarketEntryResult.Ok(submissionId);
        }

        private string NextSubmissionId()
        {
            long n = Interlocked.Increment(ref submissionCounter);
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) +
                "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsUsablePrice(double price)
        {
            return !double.IsNaN(price) && !double.IsInfinity(price) && price > 0.0;
        }

        private static string Format(double price)
        {
            return price.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        private static string SafeOrderId(Order order)
        {
            if (order == null) return "-";
            string id = order.OrderId;
            if (string.IsNullOrEmpty(id)) id = order.Id.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

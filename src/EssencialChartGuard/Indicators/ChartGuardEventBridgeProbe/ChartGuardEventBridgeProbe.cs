using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.Indicators.EssencialChartGuard
{
    // Temporary observation harness for NinjaTraderAccountEventBridge.
    //
    // Purpose: add this indicator to a Replay/Sim chart, set the AccountName, and watch the
    // Output Window Tab 1 to verify that real OrderUpdate / ExecutionUpdate events are routed
    // through OrderEventRouter without ChartGuard sending any order.
    //
    // What this probe does NOT do:
    //   * It does NOT create TradeCommandService.
    //   * It does NOT create NinjaTraderOrderSubmitter.
    //   * It does NOT create NinjaTraderAccountAdapter.
    //   * It does NOT call EnableForControlledTest(...).
    //   * It does NOT submit, cancel or modify any order.
    //   * It does NOT render trading controls. There are no buttons, no hotkeys, no panel.
    //
    // The probe holds: an OutputTabSafeCoreLogger, an OrderEventRouter, and a single
    // NinjaTraderAccountEventBridge bound to the configured account and the chart instrument.
    // Start happens at State.DataLoaded; Stop+Dispose happen at State.Terminated.
    public sealed class ChartGuardEventBridgeProbe : Indicator
    {
        // Visible sentinel users see in the Indicators dialog. The probe interprets this exact
        // string (and any blank/whitespace Account value) as "auto-pick a connected account".
        public const string AutoAccountSentinel = "<Auto>";

        private OutputTabSafeCoreLogger probeLogger;
        private ObservedAccountState observedState;
        private OrderEventRouter router;
        private NinjaTraderAccountEventBridge eventBridge;
        private bool started;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only probe for observing NinjaTrader account order/execution events.";
                Name = "Essencial ChartGuard - Event Bridge Probe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = false;

                AccountName = AutoAccountSentinel;
                FilterByChartInstrument = true;
            }
            else if (State == State.Configure)
            {
                probeLogger = new OutputTabSafeCoreLogger();
                observedState = new ObservedAccountState();
                router = new OrderEventRouter(probeLogger, observedState);
            }
            else if (State == State.DataLoaded)
            {
                StartProbe();
            }
            else if (State == State.Terminated)
            {
                StopProbe();
            }
        }

        // No bar logic. The probe must not influence the chart or trigger any OnBarUpdate-driven
        // behavior; it only listens to account events through NinjaTraderAccountEventBridge.
        protected override void OnBarUpdate()
        {
        }

        private void StartProbe()
        {
            if (started) return;

            ISafeCoreLogger log = probeLogger;
            if (log == null)
            {
                // Should not happen; State.Configure built it. Guard for safety.
                return;
            }

            bool autoMode = IsAutoMode(AccountName);
            log.UI("Probe account mode=" + (autoMode ? "<Auto>" : "manual") +
                " requested=" + (autoMode ? AutoAccountSentinel : (AccountName ?? string.Empty)));

            log.UI("Probe attaching account=" + (autoMode ? AutoAccountSentinel : AccountName) +
                " chartInstrument=" + (Instrument != null ? Instrument.FullName : "?") +
                " filterByChartInstrument=" + FilterByChartInstrument);

            AccountResolution resolution = ResolveAccount(AccountName);
            if (resolution.Account == null)
            {
                log.UI("Probe attach aborted: account not found accountName=" +
                    (autoMode ? AutoAccountSentinel : (AccountName ?? string.Empty)));
                return;
            }

            log.UI("Probe selected account=" + AccountLabel(resolution.Account) +
                " mode=" + resolution.Mode +
                " sim=" + resolution.IsLikelySimulationOrPlayback);

            if (!resolution.IsLikelySimulationOrPlayback)
                log.UI("Probe observing non-sim account read-only account=" + AccountLabel(resolution.Account));

            string instrumentFilter = null;
            if (FilterByChartInstrument)
            {
                if (Instrument == null)
                {
                    log.UI("Probe attach aborted: chart instrument is null while FilterByChartInstrument=true");
                    return;
                }
                instrumentFilter = Instrument.FullName ?? string.Empty;
            }

            // Initial read-only reconciliation. We seed ObservedAccountState from
            // Account.Positions before subscribing, so a probe attached on top of an existing
            // position starts in the right state instead of Unknown. If the snapshot fails or
            // we don't have an instrument to scope it, we still proceed with observation --
            // state simply starts Unknown until the next routed execution.
            string snapshotInstrument = !string.IsNullOrEmpty(instrumentFilter)
                ? instrumentFilter
                : (Instrument != null ? (Instrument.FullName ?? string.Empty) : string.Empty);
            if (!string.IsNullOrEmpty(snapshotInstrument) && observedState != null)
            {
                try
                {
                    NinjaTraderPositionSnapshotReader reader =
                        new NinjaTraderPositionSnapshotReader(log, resolution.Account, snapshotInstrument);
                    ObservedPositionSnapshot snapshot = reader.ReadSnapshot();
                    if (snapshot != null)
                    {
                        observedState.ApplyPositionSnapshot(snapshot);
                        observedState.TryLogSummaryIfChanged(log);
                    }
                    else
                    {
                        log.UI("Probe initial snapshot unavailable; state starts Unknown");
                    }
                }
                catch (Exception ex)
                {
                    log.UI("Probe snapshot error ex=" + ex.GetType().Name + ":" + ex.Message +
                        "; state starts Unknown");
                }
            }
            else
            {
                log.UI("Probe initial snapshot skipped: no instrument scope; state starts Unknown");
            }

            try
            {
                eventBridge = new NinjaTraderAccountEventBridge(log, resolution.Account, router, instrumentFilter);
                eventBridge.Start();
                started = true;
                log.UI("Probe attached account=" + AccountLabel(resolution.Account) +
                    " instrumentFilter=" + (string.IsNullOrEmpty(instrumentFilter) ? "*" : instrumentFilter));
            }
            catch (Exception ex)
            {
                log.UI("Probe attach error ex=" + ex.GetType().Name + ":" + ex.Message);
                SafeDisposeBridge();
                started = false;
            }
        }

        private void StopProbe()
        {
            ISafeCoreLogger log = probeLogger;

            if (eventBridge != null)
            {
                try
                {
                    eventBridge.Stop();
                }
                catch (Exception ex)
                {
                    if (log != null) log.UI("Probe stop error ex=" + ex.GetType().Name + ":" + ex.Message);
                }
            }

            SafeDisposeBridge();

            if (log != null)
                log.UI("Probe detached account=" + (IsAutoMode(AccountName) ? AutoAccountSentinel : AccountName));

            started = false;
            router = null;
            observedState = null;
            probeLogger = null;
        }

        private void SafeDisposeBridge()
        {
            if (eventBridge == null) return;
            try
            {
                eventBridge.Dispose();
            }
            catch
            {
                // Dispose must never throw upward in lifecycle; ignored on purpose.
            }
            eventBridge = null;
        }

        private struct AccountResolution
        {
            public Account Account;
            public string Mode; // "<Auto>" or "manual"
            public bool IsLikelySimulationOrPlayback;
        }

        private static bool IsAutoMode(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return string.Equals(name.Trim(), AutoAccountSentinel, StringComparison.OrdinalIgnoreCase);
        }

        private static AccountResolution ResolveAccount(string name)
        {
            AccountResolution resolution = new AccountResolution();
            resolution.Account = null;
            resolution.Mode = IsAutoMode(name) ? AutoAccountSentinel : "manual";
            resolution.IsLikelySimulationOrPlayback = false;

            try
            {
                if (Account.All == null) return resolution;

                if (!IsAutoMode(name))
                {
                    Account manual = Account.All.FirstOrDefault(a => a != null
                        && (string.Equals(a.Name, name, StringComparison.Ordinal)
                            || string.Equals(a.DisplayName, name, StringComparison.Ordinal)));
                    if (manual != null)
                    {
                        resolution.Account = manual;
                        resolution.IsLikelySimulationOrPlayback = IsLikelySimulationOrPlayback(manual);
                    }
                    return resolution;
                }

                Account[] connected = Account.All
                    .Where(a => a != null && a.ConnectionStatus == ConnectionStatus.Connected)
                    .ToArray();

                Account simOrPlayback = connected.FirstOrDefault(IsLikelySimulationOrPlayback);
                if (simOrPlayback != null)
                {
                    resolution.Account = simOrPlayback;
                    resolution.IsLikelySimulationOrPlayback = true;
                    return resolution;
                }

                Account anyConnected = connected.FirstOrDefault();
                if (anyConnected != null)
                {
                    resolution.Account = anyConnected;
                    resolution.IsLikelySimulationOrPlayback = false;
                }
                return resolution;
            }
            catch
            {
                resolution.Account = null;
                return resolution;
            }
        }

        private static bool IsLikelySimulationOrPlayback(Account account)
        {
            if (account == null) return false;
            try
            {
                string accountText = (account.Name ?? string.Empty) + " " + (account.DisplayName ?? string.Empty);
                if (accountText.IndexOf("Sim", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (accountText.IndexOf("Playback", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                Connection connection = account.Connection;
                if (connection == null || connection.Options == null) return false;

                string provider = connection.Options.Provider.ToString();
                string connectionName = connection.Options.Name ?? string.Empty;
                string combined = provider + " " + connectionName;

                return combined.IndexOf("Sim", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("Simulator", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("Replay", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("Playback", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static string AccountLabel(Account account)
        {
            if (account == null) return "?";
            string name = account.Name ?? string.Empty;
            string displayName = account.DisplayName ?? string.Empty;
            if (string.IsNullOrEmpty(displayName) || string.Equals(name, displayName, StringComparison.Ordinal))
                return name;
            return name + " (" + displayName + ")";
        }

        // ---- User properties ----

        [NinjaScriptProperty]
        [TypeConverter(typeof(ChartGuardProbeAccountNameConverter))]
        [Display(Name = "Account", Order = 1, GroupName = "Probe")]
        public string AccountName { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Filter events by chart instrument", Order = 2, GroupName = "Probe")]
        public bool FilterByChartInstrument { get; set; }
    }

    public sealed class ChartGuardProbeAccountNameConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> names = new List<string>();
            // <Auto> is always the first option so users can pick auto-detect explicitly.
            names.Add(ChartGuardEventBridgeProbe.AutoAccountSentinel);

            try
            {
                if (Account.All != null)
                {
                    foreach (Account account in Account.All)
                    {
                        if (account == null || account.ConnectionStatus != ConnectionStatus.Connected)
                            continue;

                        string displayName = account.DisplayName;
                        string name = account.Name;

                        if (!string.IsNullOrWhiteSpace(displayName) && !names.Contains(displayName))
                            names.Add(displayName);
                        if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                            names.Add(name);
                    }
                }
            }
            catch
            {
                // If account enumeration fails during connection changes, keep manual input usable.
            }

            return new StandardValuesCollection(names);
        }
    }
}

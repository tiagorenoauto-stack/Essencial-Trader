using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.ChartLines;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.Indicators.EssencialChartGuard
{
    // Host indicator for the first Essencial ChartGuard side panel.
    //
    // Responsibility:
    //   1. Reuse the same observation flow as the probe (auto/manual account selection,
    //      initial position snapshot, NinjaTraderAccountEventBridge), so the panel reflects
    //      real state.
    //   2. Inject EssencialChartGuardPanel into a new right-side column of the chart's
    //      parent Grid, with a small resize grip for horizontal resize.
    //   3. Refresh the panel from ObservedAccountState whenever something changes (a small
    //      DispatcherTimer pull-mode refresh keeps the host trivial and avoids hooking
    //      ObservedAccountState's internals).
    //   4. Stop/Dispose the bridge AND remove the panel/splitter/column at State.Terminated.
    //
    // What this host does NOT do:
    //   * No TradeCommandService, no NinjaTraderOrderSubmitter, no NinjaTraderAccountAdapter.
    //   * No call to EnableForControlledTest(...).
    //   * No buttons that submit, cancel, modify, or flatten any order.
    //   * No hotkeys.
    //   * No chart-click interception, no context-menu overrides, no NT-tool blocking.
    public sealed class ChartGuardPanelHost : Indicator
    {
        public const string AutoAccountSentinel = "<Auto>";

        // ---- Observation pipeline (same shape as the probe) ----
        private OutputTabSafeCoreLogger hostLogger;
        private ObservedAccountState observedState;
        private OrderEventRouter router;
        private NinjaTraderAccountEventBridge eventBridge;
        private bool started;

        // ---- Panel injection ----
        private EssencialChartGuardPanel panel;
        private Grid hostGrid;                  // the Grid we mutate
        private ColumnDefinition addedColumn;   // splitter+panel column we add
        private ColumnDefinition splitterColumn;
        private Thumb resizeThumb;
        private DispatcherTimer refreshTimer;
        private string lastSummaryRendered;
        private static readonly object PanelTagSentinel = new object();
        private bool panelInjected;

        // Last visibility we applied to the panel/splitter, so we only mutate
        // when the indicator's IsVisible flag actually flips. Tri-state: null
        // means "not yet decided" so the first OnRender always applies.
        private bool? lastPanelVisible;
        // Saved column widths captured the last time the panel was visible, so
        // hide → show restores the user's previous resize.
        private GridLength savedPanelWidth;
        private GridLength savedSplitterWidth;

        // ---- Read-only chart line renderer (Phase 3.1) ----
        // Built in State.Configure, fed by MaybeUpdateChartLines() whenever
        // the observed snapshot or the draft preview changes, and detached
        // in State.Terminated. The renderer never sends, modifies, or
        // cancels any order; it only draws horizontal lines + labels.
        private ChartGuardReadOnlyLineRenderer lineRenderer;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Essencial ChartGuard read-only side panel host. Observes account events and renders observed state. Sends no orders.";
                Name = "Essencial ChartGuard - Panel Host";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = false;

                AccountName = AutoAccountSentinel;
                FilterByChartInstrument = true;
                EnableDraftPreview = false;
            }
            else if (State == State.Configure)
            {
                hostLogger = new OutputTabSafeCoreLogger();
                observedState = new ObservedAccountState();
                router = new OrderEventRouter(hostLogger, observedState);
                lineRenderer = new ChartGuardReadOnlyLineRenderer(this, hostLogger);
            }
            else if (State == State.DataLoaded)
            {
                StartHost();
            }
            else if (State == State.Terminated)
            {
                StopHost();
            }
        }

        protected override void OnBarUpdate()
        {
            // No bar logic. The panel never reacts to bar updates; it lives off observed
            // events plus the dispatcher refresh timer.
        }

        // Mirror the indicator's "Visible" checkbox onto our injected side
        // panel. The panel is a Grid column on the ChartControl, not part of
        // the indicator's plot, so NinjaTrader's IsVisible does not hide it
        // automatically — and OnRender is not invoked on a plotless overlay
        // host like this one. We poll IsVisible from the existing 500ms
        // refresh timer (which runs on the chart dispatcher) and apply only
        // when the flag actually flips.
        private void SyncPanelVisibilityFromIndicatorFlag()
        {
            try
            {
                if (!panelInjected) return;

                bool wantVisible = IsVisible;
                if (lastPanelVisible.HasValue && lastPanelVisible.Value == wantVisible)
                    return;

                ApplyPanelVisibility(wantVisible);
                lastPanelVisible = wantVisible;
            }
            catch (Exception ex)
            {
                if (hostLogger != null)
                    hostLogger.UI("PanelHost visibility sync error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private void ApplyPanelVisibility(bool visible)
        {
            if (hostGrid == null) return;

            if (visible)
            {
                // Re-add splitter + panel columns (and the splitter / panel
                // themselves) at the right end of the host grid. We always
                // re-create the ColumnDefinitions because removing them above
                // detached them from the grid.
                splitterColumn = new ColumnDefinition
                {
                    Width = savedSplitterWidth.Value > 0
                        ? savedSplitterWidth
                        : new GridLength(EssencialChartGuardTheme.SplitterWidth, GridUnitType.Pixel)
                };
                hostGrid.ColumnDefinitions.Add(splitterColumn);
                int splitterColumnIndex = hostGrid.ColumnDefinitions.Count - 1;

                addedColumn = new ColumnDefinition
                {
                    Width = savedPanelWidth.Value > 0
                        ? savedPanelWidth
                        : new GridLength(EssencialChartGuardTheme.PanelInitialWidth, GridUnitType.Pixel),
                    MinWidth = EssencialChartGuardTheme.PanelMinWidth,
                    MaxWidth = EssencialChartGuardTheme.PanelMaxWidth
                };
                hostGrid.ColumnDefinitions.Add(addedColumn);
                int panelColumnIndex = hostGrid.ColumnDefinitions.Count - 1;

                if (resizeThumb != null && !hostGrid.Children.Contains(resizeThumb))
                {
                    Grid.SetColumn(resizeThumb, splitterColumnIndex);
                    Grid.SetRow(resizeThumb, 0);
                    if (hostGrid.RowDefinitions.Count > 1)
                        Grid.SetRowSpan(resizeThumb, hostGrid.RowDefinitions.Count);
                    hostGrid.Children.Add(resizeThumb);
                }
                if (panel != null && !hostGrid.Children.Contains(panel))
                {
                    Grid.SetColumn(panel, panelColumnIndex);
                    Grid.SetRow(panel, 0);
                    if (hostGrid.RowDefinitions.Count > 1)
                        Grid.SetRowSpan(panel, hostGrid.RowDefinitions.Count);
                    hostGrid.Children.Add(panel);
                }
            }
            else
            {
                if (addedColumn != null) savedPanelWidth = addedColumn.Width;
                if (splitterColumn != null) savedSplitterWidth = splitterColumn.Width;

                if (panel != null && hostGrid.Children.Contains(panel))
                    hostGrid.Children.Remove(panel);
                if (resizeThumb != null && hostGrid.Children.Contains(resizeThumb))
                    hostGrid.Children.Remove(resizeThumb);

                if (addedColumn != null && hostGrid.ColumnDefinitions.Contains(addedColumn))
                    hostGrid.ColumnDefinitions.Remove(addedColumn);
                if (splitterColumn != null && hostGrid.ColumnDefinitions.Contains(splitterColumn))
                    hostGrid.ColumnDefinitions.Remove(splitterColumn);

                addedColumn = null;
                splitterColumn = null;
            }
        }

        // ============================================================================
        // Lifecycle
        // ============================================================================

        private void StartHost()
        {
            if (started) return;

            ISafeCoreLogger log = hostLogger;
            if (log == null) return;

            bool autoMode = IsAutoMode(AccountName);
            log.UI("PanelHost account mode=" + (autoMode ? "<Auto>" : "manual") +
                " requested=" + (autoMode ? AutoAccountSentinel : (AccountName ?? string.Empty)));

            log.UI("PanelHost attaching account=" + (autoMode ? AutoAccountSentinel : AccountName) +
                " chartInstrument=" + (Instrument != null ? Instrument.FullName : "?") +
                " filterByChartInstrument=" + FilterByChartInstrument);

            AccountResolution resolution = ResolveAccount(AccountName);
            if (resolution.Account == null)
            {
                log.UI("PanelHost attach aborted: account not found accountName=" +
                    (autoMode ? AutoAccountSentinel : (AccountName ?? string.Empty)));
                // Still inject the panel so the user sees the read-only shell with "?".
                InjectPanelOnUi(null, null);
                return;
            }

            log.UI("PanelHost selected account=" + AccountLabel(resolution.Account) +
                " mode=" + resolution.Mode +
                " sim=" + resolution.IsLikelySimulationOrPlayback);

            if (!resolution.IsLikelySimulationOrPlayback)
                log.UI("PanelHost observing non-sim account read-only account=" + AccountLabel(resolution.Account));

            string instrumentFilter = null;
            if (FilterByChartInstrument)
            {
                if (Instrument == null)
                {
                    log.UI("PanelHost attach aborted: chart instrument is null while FilterByChartInstrument=true");
                    InjectPanelOnUi(resolution.Account != null ? resolution.Account.Name : null, null);
                    return;
                }
                instrumentFilter = Instrument.FullName ?? string.Empty;
            }

            // Inject the panel UI first so the user sees "subscribing..." while we attach.
            InjectPanelOnUi(resolution.Account.Name, instrumentFilter);

            // Same snapshot-then-subscribe order as the probe.
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
                        SafeUpdatePanelFromState();
                        SafeSetSnapshotStatus(ConnectionDot.Ok, "applied " + snapshot.PositionText.ToLowerInvariant() +
                            " qty=" + snapshot.Quantity.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        log.UI("PanelHost initial snapshot unavailable; state starts Unknown");
                        SafeSetSnapshotStatus(ConnectionDot.Warn, "unavailable; state starts Unknown");
                    }
                }
                catch (Exception ex)
                {
                    log.UI("PanelHost snapshot error ex=" + ex.GetType().Name + ":" + ex.Message +
                        "; state starts Unknown");
                    SafeSetSnapshotStatus(ConnectionDot.Error, "error " + ex.GetType().Name);
                }
            }
            else
            {
                log.UI("PanelHost initial snapshot skipped: no instrument scope; state starts Unknown");
                SafeSetSnapshotStatus(ConnectionDot.Warn, "skipped (no instrument)");
            }

            try
            {
                eventBridge = new NinjaTraderAccountEventBridge(log, resolution.Account, router, instrumentFilter);
                eventBridge.Start();
                started = true;
                log.UI("PanelHost attached account=" + AccountLabel(resolution.Account) +
                    " instrumentFilter=" + (string.IsNullOrEmpty(instrumentFilter) ? "*" : instrumentFilter));

                SafeSetBridgeStatus(ConnectionDot.Ok,
                    "subscribed (" + (string.IsNullOrEmpty(instrumentFilter) ? "*" : instrumentFilter) + ")");
                SafeSetConnectionStatus(
                    resolution.IsLikelySimulationOrPlayback ? ConnectionDot.Ok : ConnectionDot.Warn,
                    BuildModeLine(resolution));
                StartRefreshTimer();
                MaybeApplyDraftPreview();
            }
            catch (Exception ex)
            {
                log.UI("PanelHost attach error ex=" + ex.GetType().Name + ":" + ex.Message);
                SafeSetBridgeStatus(ConnectionDot.Error, "error " + ex.GetType().Name);
                SafeDisposeBridge();
                started = false;
            }
        }

        private void StopHost()
        {
            ISafeCoreLogger log = hostLogger;

            StopRefreshTimer();

            if (eventBridge != null)
            {
                try
                {
                    eventBridge.Stop();
                }
                catch (Exception ex)
                {
                    if (log != null) log.UI("PanelHost stop error ex=" + ex.GetType().Name + ":" + ex.Message);
                }
            }
            SafeDisposeBridge();

            // Remove every read-only chart line we drew before tearing down
            // the panel. Doing this before the panel removal keeps the
            // chart visually consistent during the teardown frame.
            if (lineRenderer != null)
            {
                try { lineRenderer.DetachAll(); }
                catch (Exception ex)
                {
                    if (log != null)
                        log.UI("PanelHost line renderer detach error ex=" + ex.GetType().Name + ":" + ex.Message);
                }
                lineRenderer = null;
            }

            RemovePanelFromUi();

            if (log != null)
                log.UI("PanelHost detached account=" + (IsAutoMode(AccountName) ? AutoAccountSentinel : AccountName));

            started = false;
            router = null;
            observedState = null;
            hostLogger = null;
        }

        private void SafeDisposeBridge()
        {
            if (eventBridge == null) return;
            try { eventBridge.Dispose(); } catch { /* dispose must not throw upward */ }
            eventBridge = null;
        }

        // ============================================================================
        // Panel injection (right-side column + splitter)
        // ============================================================================

        private void InjectPanelOnUi(string accountName, string instrumentFullName)
        {
            ChartControl cc = ChartControl;
            if (cc == null)
            {
                if (hostLogger != null) hostLogger.UI("PanelHost injection skipped: ChartControl is null");
                return;
            }

            Action inject = delegate
            {
                try
                {
                    if (panelInjected) return;

                    Grid grid = FindHostGrid(cc);
                    if (grid == null)
                    {
                        if (hostLogger != null) hostLogger.UI("PanelHost injection failed: no host Grid found");
                        return;
                    }

                    panel = new EssencialChartGuardPanel();
                    panel.SetAccountAndInstrument(accountName, instrumentFullName);
                    panel.SetConnectionStatus(ConnectionDot.Idle, "Observer");

                    // Add splitter column then panel column.
                    splitterColumn = new ColumnDefinition
                    {
                        Width = new GridLength(EssencialChartGuardTheme.SplitterWidth, GridUnitType.Pixel)
                    };
                    grid.ColumnDefinitions.Add(splitterColumn);
                    int splitterColumnIndex = grid.ColumnDefinitions.Count - 1;

                    addedColumn = new ColumnDefinition
                    {
                        Width = new GridLength(EssencialChartGuardTheme.PanelInitialWidth, GridUnitType.Pixel),
                        MinWidth = EssencialChartGuardTheme.PanelMinWidth,
                        MaxWidth = EssencialChartGuardTheme.PanelMaxWidth
                    };
                    grid.ColumnDefinitions.Add(addedColumn);
                    int panelColumnIndex = grid.ColumnDefinitions.Count - 1;

                    resizeThumb = new Thumb
                    {
                        Width = EssencialChartGuardTheme.SplitterWidth,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Background = EssencialChartGuardTheme.BorderStrong,
                        Cursor = System.Windows.Input.Cursors.SizeWE,
                        Tag = PanelTagSentinel
                    };
                    resizeThumb.DragDelta += OnResizeThumbDragDelta;
                    Grid.SetColumn(resizeThumb, splitterColumnIndex);
                    Grid.SetRow(resizeThumb, 0);
                    if (grid.RowDefinitions.Count > 1)
                        Grid.SetRowSpan(resizeThumb, grid.RowDefinitions.Count);
                    grid.Children.Add(resizeThumb);

                    panel.Tag = PanelTagSentinel;
                    Grid.SetColumn(panel, panelColumnIndex);
                    Grid.SetRow(panel, 0);
                    if (grid.RowDefinitions.Count > 1)
                        Grid.SetRowSpan(panel, grid.RowDefinitions.Count);
                    grid.Children.Add(panel);

                    hostGrid = grid;
                    panelInjected = true;

                    if (hostLogger != null)
                        hostLogger.UI("PanelHost panel injected initialWidth=" +
                            EssencialChartGuardTheme.PanelInitialWidth.ToString(CultureInfo.InvariantCulture) +
                            "px minWidth=" + EssencialChartGuardTheme.PanelMinWidth.ToString(CultureInfo.InvariantCulture) + "px");
                }
                catch (Exception ex)
                {
                    if (hostLogger != null)
                        hostLogger.UI("PanelHost injection error ex=" + ex.GetType().Name + ":" + ex.Message);
                }
            };

            try
            {
                if (cc.Dispatcher.CheckAccess())
                    inject();
                else
                    cc.Dispatcher.Invoke(inject);
            }
            catch (Exception ex)
            {
                if (hostLogger != null)
                    hostLogger.UI("PanelHost injection dispatch error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private void RemovePanelFromUi()
        {
            ChartControl cc = ChartControl;
            Action removal = delegate
            {
                try
                {
                    if (!panelInjected || hostGrid == null) return;

                    if (resizeThumb != null)
                    {
                        try { resizeThumb.DragDelta -= OnResizeThumbDragDelta; } catch { /* ignore */ }
                        if (hostGrid.Children.Contains(resizeThumb))
                            hostGrid.Children.Remove(resizeThumb);
                    }
                    if (panel != null && hostGrid.Children.Contains(panel))
                        hostGrid.Children.Remove(panel);

                    // Remove our two columns by Tag identity, not by index, so we never
                    // remove a column added by something else.
                    if (addedColumn != null && hostGrid.ColumnDefinitions.Contains(addedColumn))
                        hostGrid.ColumnDefinitions.Remove(addedColumn);
                    if (splitterColumn != null && hostGrid.ColumnDefinitions.Contains(splitterColumn))
                        hostGrid.ColumnDefinitions.Remove(splitterColumn);

                    resizeThumb = null;
                    addedColumn = null;
                    splitterColumn = null;
                    panel = null;
                    hostGrid = null;
                    panelInjected = false;
                    lastPanelVisible = null;
                    savedPanelWidth = new GridLength(0, GridUnitType.Pixel);
                    savedSplitterWidth = new GridLength(0, GridUnitType.Pixel);

                    if (hostLogger != null) hostLogger.UI("PanelHost panel removed");
                }
                catch (Exception ex)
                {
                    if (hostLogger != null)
                        hostLogger.UI("PanelHost removal error ex=" + ex.GetType().Name + ":" + ex.Message);
                }
            };

            if (cc != null && cc.Dispatcher != null)
            {
                try { cc.Dispatcher.Invoke(removal); }
                catch
                {
                    // Dispatcher may already be torn down on shutdown; fall back to direct call.
                    try { removal(); } catch { /* ignore */ }
                }
            }
            else
            {
                try { removal(); } catch { /* ignore */ }
            }
        }

        // The chart's ChartControl has several internal Grid ancestors. The immediate one can
        // be part of the plot surface; mutating it makes the panel appear over the chart and
        // can hide candles. For the side panel we only accept a higher-level layout Grid that
        // already has at least two columns (chart area + side area / chart-trader region).
        // If that host is not found we abort rather than touching an unsafe internal grid.
        private static Grid FindHostGrid(ChartControl cc)
        {
            DependencyObject node = cc;
            while (node != null)
            {
                node = VisualTreeHelper.GetParent(node);
                Grid grid = node as Grid;
                if (grid != null && grid.ColumnDefinitions != null && grid.ColumnDefinitions.Count >= 2)
                    return grid;
            }
            return null;
        }

        // ============================================================================
        // Resize grip
        // ============================================================================

        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                if (addedColumn == null) return;
                double current = addedColumn.ActualWidth;
                if (double.IsNaN(current) || current <= 0)
                    current = EssencialChartGuardTheme.PanelInitialWidth;

                // The grip sits on the panel's left edge: dragging left increases width,
                // dragging right decreases it. Only our panel column changes.
                double next = current - e.HorizontalChange;
                if (next < EssencialChartGuardTheme.PanelMinWidth)
                    next = EssencialChartGuardTheme.PanelMinWidth;
                if (next > EssencialChartGuardTheme.PanelMaxWidth)
                    next = EssencialChartGuardTheme.PanelMaxWidth;

                addedColumn.Width = new GridLength(next, GridUnitType.Pixel);
            }
            catch
            {
                // Resize must never affect chart stability.
            }
        }

        // ============================================================================
        // Refresh timer
        // ============================================================================

        private void StartRefreshTimer()
        {
            ChartControl cc = ChartControl;
            if (cc == null) return;
            cc.Dispatcher.InvokeAsync(delegate
            {
                if (refreshTimer != null) return;
                refreshTimer = new DispatcherTimer(DispatcherPriority.Background, cc.Dispatcher);
                refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
                refreshTimer.Tick += OnRefreshTick;
                refreshTimer.Start();
            });
        }

        private void StopRefreshTimer()
        {
            DispatcherTimer t = refreshTimer;
            refreshTimer = null;
            if (t != null)
            {
                try { t.Stop(); } catch { /* ignore */ }
                try { t.Tick -= OnRefreshTick; } catch { /* ignore */ }
            }
        }

        private void OnRefreshTick(object sender, EventArgs e)
        {
            SyncPanelVisibilityFromIndicatorFlag();
            SafeUpdatePanelFromState();
        }

        private void SafeUpdatePanelFromState()
        {
            ObservedAccountState s = observedState;
            EssencialChartGuardPanel p = panel;
            if (s == null || p == null) return;

            try
            {
                // Use the existing summary string as a cheap change-detector so the panel
                // does not re-paint every 500ms when nothing changed.
                string summary = s.BuildSummary();
                if (string.Equals(summary, lastSummaryRendered, StringComparison.Ordinal)) return;
                lastSummaryRendered = summary;

                ObservedAccountSnapshotDto dto = s.BuildSnapshotDto();
                p.SetObservedState(dto);

                // Phase 3.1: refresh the read-only chart lines from the same
                // snapshot the panel just consumed. The renderer is idempotent
                // per tag, so re-pushing identical state is a cheap no-op.
                MaybeUpdateChartLines();
            }
            catch (Exception ex)
            {
                if (hostLogger != null)
                    hostLogger.UI("PanelHost refresh error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private void SafeSetConnectionStatus(ConnectionDot dot, string text)
        {
            EssencialChartGuardPanel p = panel;
            if (p == null) return;
            try { p.SetConnectionStatus(dot, text); } catch { /* never let UI surface errors */ }
        }

        private void SafeSetSnapshotStatus(ConnectionDot dot, string text)
        {
            EssencialChartGuardPanel p = panel;
            if (p == null) return;
            try { p.SetSnapshotStatus(dot, text); } catch { /* ignore */ }
        }

        private void SafeSetBridgeStatus(ConnectionDot dot, string text)
        {
            EssencialChartGuardPanel p = panel;
            if (p == null) return;
            try { p.SetBridgeStatus(dot, text); } catch { /* ignore */ }
        }

        // ============================================================================
        // Account resolution (mirrors the probe; intentionally kept local to avoid coupling)
        // ============================================================================

        private struct AccountResolution
        {
            public Account Account;
            public string Mode;
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

        // Builds a short, operator-friendly mode label. Always starts with "Observer" to
        // make the read-only nature obvious even at a glance, followed by a connection-type
        // qualifier ("Playback" / "Sim" / "non-sim") inferred from provider/connection text.
        // Falls back to plain "Observer" when no signal is available.
        // ============================================================================
        // Draft preview (Phase 2.3)
        // ============================================================================

        // When EnableDraftPreview is true, the host pushes a hard-coded preview StrategyDraft
        // into the panel right after the bridge is up. Every control on the panel remains
        // disabled and unresponsive; this only exercises the read-only Set*Draft mutators
        // so the visual model can be validated end-to-end. Default is false: with the
        // checkbox left at its default the host behaves exactly like before.
        //
        // This method:
        //   * never enables a control,
        //   * never attaches an event handler,
        //   * never creates a TradeCommandService / submitter / adapter,
        //   * never calls EnableForControlledTest,
        //   * never persists anything (no settings, no file IO).
        private void MaybeApplyDraftPreview()
        {
            if (!EnableDraftPreview) return;

            EssencialChartGuardPanel p = panel;
            if (p == null) return;

            try
            {
                StrategyDraft draft = BuildPreviewStrategyDraft();
                p.SetStrategyDraft(draft);
                if (hostLogger != null)
                    hostLogger.UI("PanelHost draft preview applied strategy=" +
                        (draft != null ? draft.Name : "?"));
                // Phase 3.1: also push the same draft + the latest observed
                // snapshot into the chart line renderer. The renderer is a
                // pure visual surface; it never creates a command path.
                MaybeUpdateChartLines();
            }
            catch (Exception ex)
            {
                if (hostLogger != null)
                    hostLogger.UI("PanelHost draft preview error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        // ============================================================================
        // Read-only chart lines (Phase 3.1)
        // ============================================================================

        // Computes the current ChartGuardLineState from the observed snapshot and the
        // hard-coded preview StrategyDraft (when EnableDraftPreview is true), then
        // hands it to the renderer. When EnableDraftPreview is false, the host pushes
        // an "all-hidden" state to make sure no leftover lines remain on the chart.
        //
        // No order, command, or trading API is touched here. The only NinjaTrader
        // primitive this method reads is Instrument.MasterInstrument.TickSize, which
        // is required to translate a ticks-based draft (stop=40 ticks, etc.) into
        // a chart price. If TickSize is unavailable or the reference price is not
        // safe, the draft lines stay hidden and the host logs the skip reason.
        private void MaybeUpdateChartLines()
        {
            ChartGuardReadOnlyLineRenderer r = lineRenderer;
            if (r == null) return;

            ObservedAccountState obs = observedState;
            ObservedAccountSnapshotDto dto = obs != null ? obs.BuildSnapshotDto() : default(ObservedAccountSnapshotDto);

            ChartGuardLineState state = BuildLineState(dto, EnableDraftPreview);
            r.ApplyState(state);
        }

        // Pure conversion: observed snapshot + preview-on flag => line state.
        // Kept testable in isolation. The Instrument lookup happens through the
        // host's own Instrument property (not a NinjaTrader.Cbi import inside
        // SafeCore/Panel/Models), and only TickSize / FullName are used.
        private ChartGuardLineState BuildLineState(ObservedAccountSnapshotDto dto, bool draftPreviewOn)
        {
            ChartGuardLineState state = ChartGuardLineState.Empty();

            bool hasOpenPosition = dto.AbsoluteQuantity > 0
                && (dto.Position == ObservedPosition.Long || dto.Position == ObservedPosition.Short);
            bool hasLastPrice = dto.LastPrice.HasValue
                && !double.IsNaN(dto.LastPrice.Value)
                && !double.IsInfinity(dto.LastPrice.Value)
                && dto.LastPrice.Value > 0.0;

            // ---- Entry / Avg ----
            if (hasOpenPosition && hasLastPrice)
            {
                state.ShowEntryAvg = true;
                state.EntryAvgPrice = dto.LastPrice.Value;
                state.EntryAvgLabel = "ECG Entry " + FormatPrice(dto.LastPrice.Value);
            }

            // ---- Last fill ----
            if (hasLastPrice)
            {
                state.ShowLastFill = true;
                state.LastFillPrice = dto.LastPrice.Value;
                state.LastFillLabel = "ECG Last " + FormatPrice(dto.LastPrice.Value);
            }

            // ---- Draft lines (only when EnableDraftPreview is true AND the
            // chart instrument matches the snapshot AND we have a safe reference
            // price + a valid tick size). Otherwise we deliberately do not draw
            // and we log the skip reason. The reference price for ticks-based
            // drafts is the observed last price, never a synthesized value.
            if (draftPreviewOn)
            {
                double tickSize = SafeTickSize();
                bool tickOk = tickSize > 0.0;
                bool instrumentMatches = SnapshotMatchesChartInstrument(dto);

                if (!tickOk)
                {
                    if (hostLogger != null)
                        hostLogger.UI("PanelHost chart lines skipped: tick size unavailable for chart instrument");
                }
                else if (!instrumentMatches)
                {
                    if (hostLogger != null)
                        hostLogger.UI("PanelHost chart lines skipped: snapshot instrument does not match chart instrument");
                }
                else if (!hasLastPrice)
                {
                    if (hostLogger != null)
                        hostLogger.UI("PanelHost chart lines skipped: no safe reference price for draft levels");
                }
                else
                {
                    int sign = ResolveDraftDirectionSign(dto);
                    if (sign == 0)
                    {
                        // Flat / Unknown: we cannot orient stop/targets without a side.
                        if (hostLogger != null)
                            hostLogger.UI("PanelHost chart lines skipped: no observed direction (Flat/Unknown) -- draft levels need a side");
                    }
                    else
                    {
                        double reference = dto.LastPrice.Value;
                        StrategyDraft draft = BuildPreviewStrategyDraft();

                        // Draft stop: 40 ticks against the side.
                        int stopTicks = ParseTicks(draft != null && draft.DefaultStop != null
                            ? draft.DefaultStop.Current : null, defaultTicks: 40);
                        if (stopTicks > 0)
                        {
                            double stopPrice = reference - sign * stopTicks * tickSize;
                            state.ShowDraftStop = true;
                            state.DraftStopPrice = stopPrice;
                            state.DraftStopLabel = "ECG Stop Draft " + FormatPrice(stopPrice);
                        }

                        // Draft targets in favor of the side.
                        TakeTargetDraft[] targets = draft != null ? draft.DefaultTargets : null;
                        TakeTargetDraft t1 = targets != null && targets.Length > 0 ? targets[0] : null;
                        TakeTargetDraft t2 = targets != null && targets.Length > 1 ? targets[1] : null;

                        int t1Ticks = ParseTicks(t1 != null ? t1.Value : null, defaultTicks: 40);
                        if (t1Ticks > 0)
                        {
                            double tp = reference + sign * t1Ticks * tickSize;
                            state.ShowDraftT1 = true;
                            state.DraftT1Price = tp;
                            state.DraftT1Label = "ECG T1 Draft " + FormatPrice(tp);
                        }

                        int t2Ticks = ParseTicks(t2 != null ? t2.Value : null, defaultTicks: 80);
                        if (t2Ticks > 0)
                        {
                            double tp = reference + sign * t2Ticks * tickSize;
                            state.ShowDraftT2 = true;
                            state.DraftT2Price = tp;
                            state.DraftT2Label = "ECG T2 Draft " + FormatPrice(tp);
                        }
                    }
                }
            }

            return state;
        }

        // Direction sign for draft level orientation. Long=+1, Short=-1, otherwise 0
        // (flat/unknown -> caller should not draw drafts).
        private static int ResolveDraftDirectionSign(ObservedAccountSnapshotDto dto)
        {
            if (dto.Position == ObservedPosition.Long && dto.AbsoluteQuantity > 0) return +1;
            if (dto.Position == ObservedPosition.Short && dto.AbsoluteQuantity > 0) return -1;
            return 0;
        }

        // Reads the chart instrument's tick size. Wrapped because NT can throw
        // during very early/late lifecycle.
        private double SafeTickSize()
        {
            try
            {
                if (Instrument != null && Instrument.MasterInstrument != null)
                {
                    double t = Instrument.MasterInstrument.TickSize;
                    if (!double.IsNaN(t) && !double.IsInfinity(t) && t > 0.0) return t;
                }
            }
            catch
            {
                // ignore -- caller treats as "unavailable"
            }
            return 0.0;
        }

        // True when the host has no instrument scope (FilterByChartInstrument=false
        // and DTO has no instrument), or when the DTO instrument matches the chart
        // instrument FullName. False when scopes disagree -- we refuse to draw
        // draft levels in that case.
        private bool SnapshotMatchesChartInstrument(ObservedAccountSnapshotDto dto)
        {
            try
            {
                string dtoInstrument = dto.InstrumentFullName ?? string.Empty;
                string chartInstrument = Instrument != null ? (Instrument.FullName ?? string.Empty) : string.Empty;

                if (string.IsNullOrEmpty(dtoInstrument) || dtoInstrument == "?")
                    return !FilterByChartInstrument;

                if (string.IsNullOrEmpty(chartInstrument)) return false;
                return string.Equals(dtoInstrument, chartInstrument, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // Parses a draft value like "40" or "40 Ticks" into a tick count. Returns
        // defaultTicks when the value is null/empty so the demo preview always
        // produces lines even if a future StopDraft.Current loses its number.
        private static int ParseTicks(string value, int defaultTicks)
        {
            if (string.IsNullOrEmpty(value)) return defaultTicks > 0 ? defaultTicks : 0;
            string trimmed = value.Trim();
            string digits = string.Empty;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c >= '0' && c <= '9') digits += c;
                else if (digits.Length > 0) break;
            }
            int parsed;
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                return parsed;
            return defaultTicks > 0 ? defaultTicks : 0;
        }

        private static string FormatPrice(double price)
        {
            return price.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        // Hard-coded "Preview Scalper" demo draft. Pure data only -- no NinjaTrader trading
        // type is touched here. Mirrors the example documented for Phase 2.3 in
        // docs/manual-test-checklist.md.
        private static StrategyDraft BuildPreviewStrategyDraft()
        {
            EntryPlanDraft entry = new EntryPlanDraft
            {
                EntryType = "Market",
                Quantity = 2,
                SizingMode = "Fixed",
                Unit = "Ticks",
                Stop = "40",
                Target = "80"
            };

            StopDraft stop = new StopDraft
            {
                Current = "40 Ticks",
                Unit = "Ticks",
                IsRequired = true
            };

            TakeTargetDraft t1 = new TakeTargetDraft
            {
                Label = "T1",
                Quantity = 1,
                Value = "40",
                Unit = "Ticks"
            };
            TakeTargetDraft t2 = new TakeTargetDraft
            {
                Label = "T2",
                Quantity = 1,
                Value = "80",
                Unit = "Ticks"
            };

            ProtectionDraft protection = new ProtectionDraft
            {
                BreakevenEnabled = true,
                Lock1REnabled = true,
                Lock2REnabled = false,
                Lock3REnabled = false,
                TrailEnabled = true,
                Summary = "BE | Lock 1R | Trail"
            };

            RiskModeDraft risk = new RiskModeDraft
            {
                Mode = "Alert",
                DailyLimit = "preview only",
                Status = "draft preview",
                BlockStatus = "-"
            };

            return new StrategyDraft
            {
                Name = "Preview Scalper",
                Description = "Phase 2.3 hard-coded preview; not a saved strategy",
                DefaultEntryPlan = entry,
                DefaultStop = stop,
                DefaultTargets = new[] { t1, t2 },
                DefaultProtection = protection,
                DefaultRiskMode = risk
            };
        }

        private static string BuildModeLine(AccountResolution resolution)
        {
            if (resolution.Account == null) return "Observer";
            string qualifier = ClassifyConnectionTypeForLabel(resolution.Account, resolution.IsLikelySimulationOrPlayback);
            return string.IsNullOrEmpty(qualifier) ? "Observer" : "Observer / " + qualifier;
        }

        private static string ClassifyConnectionTypeForLabel(Account account, bool isLikelySim)
        {
            try
            {
                string accountText = (account.Name ?? string.Empty) + " " + (account.DisplayName ?? string.Empty);
                Connection connection = account.Connection;
                string provider = connection != null && connection.Options != null
                    ? (connection.Options.Provider.ToString() ?? string.Empty)
                    : string.Empty;
                string connectionName = connection != null && connection.Options != null
                    ? (connection.Options.Name ?? string.Empty)
                    : string.Empty;
                string combined = accountText + " " + provider + " " + connectionName;

                if (combined.IndexOf("Playback", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("Replay", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Playback";

                if (isLikelySim
                    || combined.IndexOf("Simulator", StringComparison.OrdinalIgnoreCase) >= 0
                    || combined.IndexOf("Sim", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Sim";

                return "non-sim";
            }
            catch
            {
                return isLikelySim ? "Sim" : string.Empty;
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
        [TypeConverter(typeof(ChartGuardPanelHostAccountNameConverter))]
        [Display(Name = "Account", Order = 1, GroupName = "Panel")]
        public string AccountName { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Filter events by chart instrument", Order = 2, GroupName = "Panel")]
        public bool FilterByChartInstrument { get; set; }

        // Phase 2.3 visual validation toggle. When true, the host applies a hard-coded
        // StrategyDraft right after attach so the read-only Set*Draft mutators can be
        // exercised end-to-end. The panel stays fully disabled in this mode -- no control
        // becomes operational, no command path is created, no settings are persisted.
        // Default false: the host renders exactly the prior Phase 2 idle state.
        [NinjaScriptProperty]
        [Display(Name = "Enable draft preview (read-only)", Order = 3, GroupName = "Panel")]
        public bool EnableDraftPreview { get; set; }
    }

    public sealed class ChartGuardPanelHostAccountNameConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) { return false; }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> names = new List<string>();
            names.Add(ChartGuardPanelHost.AutoAccountSentinel);

            try
            {
                if (Account.All != null)
                {
                    foreach (Account account in Account.All)
                    {
                        if (account == null || account.ConnectionStatus != ConnectionStatus.Connected) continue;
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

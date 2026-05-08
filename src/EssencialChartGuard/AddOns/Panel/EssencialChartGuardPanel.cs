using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel
{
    // Phase 1/2 read-only side-panel shell.
    //
    // Visual identity is the same of the legacy "Cunha" panel (header gold
    // brand + conn dot + warn + settings, bare card with Modo/Unidade/Conta/
    // Instrumento/ATM, Entrada section with Tipo/Qty/Sizing/Buy/Sell/Panic,
    // Posição Ativa with Takes/Stop/Trail/Lock R/BE, Risco with progress
    // bars, Sessão placeholder, Toast overlay). The WPF building blocks are
    // copied verbatim where they are pure layout; every event-handler and
    // every reference to Account / Instrument / BracketManager / RiskEngine
    // / HotKeyManager / Settings is intentionally NOT carried over. The
    // controls render but do nothing -- they are kept IsEnabled=false so the
    // panel is read-only by contract while the Safe Core wiring evolves in
    // later phases.
    //
    // Public surface preserved (the host calls these and they must keep
    // working without any host change):
    //   * SetConnectionStatus / SetAccountAndInstrument
    //   * SetObservedState / SetSnapshotStatus / SetBridgeStatus
    //   * SetEntryPlanPlaceholders
    //   * SetActivePositionPnL / SetActivePositionProtection
    //   * SetRiskMetrics / SetSessionMetrics / SetStrategyName
    //   * SetSectionsVisibility / ResetVisualToIdle
    //   * SetStrategyDraft / SetEntryPlanDraft / SetTakeTargetsDraft
    //     SetStopDraft / SetProtectionDraft / SetRiskModeDraft
    //   * Enums ConnectionDot, struct EssencialChartGuardPanelSections
    //
    // The panel does NOT:
    //   * attach Click / SelectionChanged / TextChanged / MouseDown / MouseUp
    //     / PreviewMouse* / ContextMenu / KeyBinding / InputBindings handlers
    //     to any input control,
    //   * import NinjaTrader.Cbi or NinjaTrader.Data,
    //   * call Account.Submit / Account.CreateOrder / AtmStrategyCreate /
    //     EnableForControlledTest,
    //   * persist any settings.
    public sealed class EssencialChartGuardPanel : UserControl
    {
        // =====================================================================
        // UI fields (mirrors the legacy panel; pure references)
        // =====================================================================

        // Header
        private TextBlock headerTitle;
        private TextBlock headerSubtitle;
        private Ellipse connDot;
        private Button warnButton;
        private Button settingsButton;

        // Top controls (Modo / Unidade / Conta / Instrumento / ATM)
        private ComboBox modeCombo;
        private ComboBox unitCombo;
        private ComboBox accountCombo;
        private ComboBox instrumentCombo;
        private ComboBox atmStrategyCombo;

        // Header summary chip (Position · qty · avg · wo) -- carried from the
        // existing panel so the host's SetObservedState keeps lighting up the
        // exact same line.
        private TextBlock summaryPositionText;
        private TextBlock summaryQtyText;
        private TextBlock summaryAvgText;
        private TextBlock summaryWorkingOrdersText;

        // Entry section
        private ComboBox orderTypeCombo;
        private StackPanel limitPriceRow;
        private TextBox limitPriceBox;
        private TextBox qtyBox;
        private CheckBox usePosSizingCheck;
        private TextBox riscoPctBox;
        private TextBlock sizingResultText;
        private Button buyButton;
        private Button sellButton;
        private Button panicButton;

        // Active position section
        private Border activePosCard;
        private TextBlock activePosHeader;
        private TextBlock activePosPnL;
        private WrapPanel takesList;
        private TextBox takeInputBox;
        private Button addTakeButton;
        private TextBlock stopValueText;
        private WrapPanel stopChipsHost;
        private TextBox stopInputBox;
        private Button setStopButton;
        private ComboBox trailCombo;
        private Button lock1RBtn;
        private Button lock2RBtn;
        private Button lock3RBtn;
        private Button beBtn;

        // Active position rich rows (kept from previous panel for the host's
        // SetActivePositionPnL / SetActivePositionProtection mutators)
        private TextBlock activePosDirectionValue;
        private TextBlock activePosQtyValue;
        private TextBlock activePosEntryValue;
        private TextBlock activePosLastFillValue;
        private TextBlock activePosPnLTicksValue;
        private TextBlock activePosPnLPointsValue;
        private TextBlock activePosPnLPercentValue;
        private TextBlock activePosPnLCashValue;
        private TextBlock activePosWorkingOrdersValue;
        private TextBlock activePosStopValue;
        private TextBlock activePosTargetsValue;
        private TextBlock activePosProtectionValue;

        // Risk section
        private Border riskCard;
        private TextBlock saldoValue;
        private TextBlock quebraEmValue;
        private TextBlock pctUsedValue;
        private Border pctUsedBar;
        private TextBlock metaProgressText;
        private Border metaBar;
        private TextBlock riskDailyLimitValue;
        private TextBlock riskStatusValue;
        private TextBlock riskBlockStatusValue;
        private ComboBox riskModeSelect;

        // Session section
        private Border sessionCard;
        private TextBlock sessionTradesValue;
        private TextBlock sessionPnLValue;
        private TextBlock sessionTimeValue;

        // Observation card (Snapshot dot + EventBridge dot)
        private Border observationCard;
        private Ellipse snapshotDot;
        private TextBlock snapshotText;
        private Ellipse bridgeDot;
        private TextBlock bridgeText;

        // Strategy / Entry plan / Takes / Stop / Protection cards
        private Border strategyCard;
        private ComboBox strategySelect;
        private Border entryCard;
        private ComboBox entryTypeSelect;
        private TextBox entryQtyBox;
        private ComboBox entrySizingSelect;
        private ComboBox entryUnitSelect;
        private TextBox entryStopBox;
        private TextBox entryTargetBox;
        private Border takesCard;
        private TextBlock takesEmptyHint;
        private Border stopCard;
        private TextBlock stopCurrentValue;
        private Border protectionCard;

        // Toast overlay
        private Border toastHost;
        private TextBlock toastText;
        private DispatcherTimer toastTimer;

        // =====================================================================
        // Construction
        // =====================================================================

        public EssencialChartGuardPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = EssencialChartGuardTheme.BgRoot;
            Focusable = false;
            MinWidth = 380;

            BuildUI();
            ResetVisualToIdle();
        }

        // =====================================================================
        // Public mutators (host-only). NONE of them issues any order/command.
        // =====================================================================

        public void SetConnectionStatus(ConnectionDot dot, string modeLine)
        {
            RunOnUi(delegate
            {
                if (connDot != null) connDot.Fill = ResolveDot(dot);
                if (headerSubtitle != null && !string.IsNullOrEmpty(modeLine))
                    headerSubtitle.Text = modeLine;
            });
        }

        public void SetAccountAndInstrument(string accountName, string instrumentFullName)
        {
            RunOnUi(delegate
            {
                string acc = string.IsNullOrEmpty(accountName) ? "?" : accountName;
                string ins = string.IsNullOrEmpty(instrumentFullName) ? "?" : instrumentFullName;
                if (headerSubtitle != null) headerSubtitle.Text = acc + " · " + ins;
            });
        }

        public void SetObservedState(ObservedAccountSnapshotDto dto)
        {
            string positionText = dto.Position.ToString();
            Brush positionBrush = ResolvePositionBrush(dto.Position);
            string qtyText = dto.AbsoluteQuantity.ToString(CultureInfo.InvariantCulture);
            string lastPriceText = dto.LastPrice.HasValue
                ? dto.LastPrice.Value.ToString("0.#####", CultureInfo.InvariantCulture)
                : "-";
            bool hasOpenPosition = dto.AbsoluteQuantity > 0
                && (dto.Position == ObservedPosition.Long || dto.Position == ObservedPosition.Short);
            string avgPriceText = hasOpenPosition ? lastPriceText : "-";
            string workingText = dto.WorkingOrdersCount.ToString(CultureInfo.InvariantCulture);

            RunOnUi(delegate
            {
                if (headerSubtitle != null)
                {
                    string acc = string.IsNullOrEmpty(dto.AccountName) ? "?" : dto.AccountName;
                    string ins = string.IsNullOrEmpty(dto.InstrumentFullName) ? "?" : dto.InstrumentFullName;
                    headerSubtitle.Text = acc + " · " + ins;
                }

                if (summaryPositionText != null)
                {
                    summaryPositionText.Text = positionText;
                    summaryPositionText.Foreground = positionBrush;
                }
                if (summaryQtyText != null) summaryQtyText.Text = "qty " + qtyText;
                if (summaryAvgText != null) summaryAvgText.Text = "avg " + avgPriceText;
                if (summaryWorkingOrdersText != null) summaryWorkingOrdersText.Text = "wo " + workingText;

                if (activePosDirectionValue != null)
                {
                    activePosDirectionValue.Text = positionText;
                    activePosDirectionValue.Foreground = positionBrush;
                }
                if (activePosQtyValue != null) activePosQtyValue.Text = qtyText;
                if (activePosEntryValue != null) activePosEntryValue.Text = avgPriceText;
                if (activePosLastFillValue != null) activePosLastFillValue.Text = lastPriceText;
                if (activePosWorkingOrdersValue != null) activePosWorkingOrdersValue.Text = workingText;

                if (activePosHeader != null)
                    activePosHeader.Text = hasOpenPosition
                        ? (dto.Position == ObservedPosition.Long ? "COMPRADO " : "VENDIDO ") + qtyText + " @ " + avgPriceText
                        : "(sem posição ativa)";
            });
        }

        public void SetSnapshotStatus(ConnectionDot dot, string text)
        {
            RunOnUi(delegate
            {
                if (snapshotDot != null) snapshotDot.Fill = ResolveDot(dot);
                if (snapshotText != null) snapshotText.Text = text ?? string.Empty;
            });
        }

        public void SetBridgeStatus(ConnectionDot dot, string text)
        {
            RunOnUi(delegate
            {
                if (bridgeDot != null) bridgeDot.Fill = ResolveDot(dot);
                if (bridgeText != null) bridgeText.Text = text ?? string.Empty;
            });
        }

        public void SetEntryPlanPlaceholders(string orderType, string qty, string stop, string target)
        {
            RunOnUi(delegate
            {
                if (orderTypeCombo != null && !string.IsNullOrEmpty(orderType))
                    SetComboPlaceholder(orderTypeCombo, orderType);
                if (qtyBox != null) qtyBox.Text = NullToDash(qty);
                if (entryStopBox != null) entryStopBox.Text = NullToDash(stop);
                if (entryTargetBox != null) entryTargetBox.Text = NullToDash(target);
            });
        }

        public void SetActivePositionPnL(string pnlTicks, string pnlPoints, string pnlPercent, string pnlCash)
        {
            RunOnUi(delegate
            {
                if (activePosPnLTicksValue != null) activePosPnLTicksValue.Text = NullToDash(pnlTicks);
                if (activePosPnLPointsValue != null) activePosPnLPointsValue.Text = NullToDash(pnlPoints);
                if (activePosPnLPercentValue != null) activePosPnLPercentValue.Text = NullToDash(pnlPercent);
                if (activePosPnLCashValue != null) activePosPnLCashValue.Text = NullToDash(pnlCash);
            });
        }

        public void SetActivePositionProtection(string activeStop, string activeTargets, string protectionState)
        {
            RunOnUi(delegate
            {
                if (activePosStopValue != null) activePosStopValue.Text = NullToDash(activeStop);
                if (activePosTargetsValue != null) activePosTargetsValue.Text = NullToDash(activeTargets);
                if (activePosProtectionValue != null) activePosProtectionValue.Text = NullToDash(protectionState);
            });
        }

        public void SetRiskMetrics(string dailyLimit, string status, string blockStatus)
        {
            RunOnUi(delegate
            {
                if (riskDailyLimitValue != null) riskDailyLimitValue.Text = NullToDash(dailyLimit);
                if (riskStatusValue != null) riskStatusValue.Text = NullToDash(status);
                if (riskBlockStatusValue != null) riskBlockStatusValue.Text = NullToDash(blockStatus);
            });
        }

        public void SetSessionMetrics(string trades, string pnl, string timeOrSessionStatus)
        {
            RunOnUi(delegate
            {
                if (sessionTradesValue != null) sessionTradesValue.Text = NullToDash(trades);
                if (sessionPnLValue != null) sessionPnLValue.Text = NullToDash(pnl);
                if (sessionTimeValue != null) sessionTimeValue.Text = NullToDash(timeOrSessionStatus);
            });
        }

        public void SetStrategyName(string strategyName)
        {
            RunOnUi(delegate
            {
                if (strategySelect != null) SetComboPlaceholder(strategySelect, NullToDash(strategyName));
            });
        }

        public void SetStrategyDraft(StrategyDraft draft)
        {
            RunOnUi(delegate
            {
                if (strategySelect != null)
                    SetComboPlaceholder(strategySelect, NullToDash(draft == null ? null : draft.Name));
                if (draft == null) return;
                SetEntryPlanDraft(draft.DefaultEntryPlan);
                SetStopDraft(draft.DefaultStop);
                SetTakeTargetsDraft(draft.DefaultTargets);
                SetProtectionDraft(draft.DefaultProtection);
                SetRiskModeDraft(draft.DefaultRiskMode);
            });
        }

        public void SetEntryPlanDraft(EntryPlanDraft draft)
        {
            RunOnUi(delegate
            {
                EntryPlanDraft safe = draft ?? EntryPlanDraft.Default();
                if (orderTypeCombo != null) SetComboPlaceholder(orderTypeCombo, NullToDash(safe.EntryType));
                if (qtyBox != null) qtyBox.Text = QuantityToText(safe.Quantity);
                if (entrySizingSelect != null) SetComboPlaceholder(entrySizingSelect, NullToDash(safe.SizingMode));
                if (entryUnitSelect != null) SetComboPlaceholder(entryUnitSelect, NullToDash(safe.Unit));
                if (entryStopBox != null) entryStopBox.Text = NullToDash(safe.Stop);
                if (entryTargetBox != null) entryTargetBox.Text = NullToDash(safe.Target);
            });
        }

        public void SetTakeTargetsDraft(TakeTargetDraft[] targets)
        {
            RunOnUi(delegate
            {
                string summary = BuildTargetsSummary(targets);
                if (takesEmptyHint != null)
                {
                    takesEmptyHint.Text = string.IsNullOrEmpty(summary) ? "(no targets defined)" : summary;
                    takesEmptyHint.FontStyle = string.IsNullOrEmpty(summary) ? FontStyles.Italic : FontStyles.Normal;
                }
                if (activePosTargetsValue != null) activePosTargetsValue.Text = NullToDash(summary);
            });
        }

        public void SetStopDraft(StopDraft draft)
        {
            RunOnUi(delegate
            {
                string current = draft == null ? null : draft.Current;
                if (stopCurrentValue != null) stopCurrentValue.Text = NullToDash(current);
                if (entryStopBox != null) entryStopBox.Text = NullToDash(current);
                if (activePosStopValue != null) activePosStopValue.Text = NullToDash(current);
            });
        }

        public void SetProtectionDraft(ProtectionDraft draft)
        {
            RunOnUi(delegate
            {
                string summary = BuildProtectionSummary(draft);
                if (activePosProtectionValue != null) activePosProtectionValue.Text = NullToDash(summary);
            });
        }

        public void SetRiskModeDraft(RiskModeDraft draft)
        {
            RunOnUi(delegate
            {
                RiskModeDraft safe = draft ?? RiskModeDraft.Alert();
                if (riskModeSelect != null) SetComboPlaceholder(riskModeSelect, NullToDash(safe.Mode));
                if (riskDailyLimitValue != null) riskDailyLimitValue.Text = NullToDash(safe.DailyLimit);
                if (riskStatusValue != null) riskStatusValue.Text = NullToDash(safe.Status);
                if (riskBlockStatusValue != null) riskBlockStatusValue.Text = NullToDash(safe.BlockStatus);
            });
        }

        public void SetSectionsVisibility(EssencialChartGuardPanelSections sections)
        {
            RunOnUi(delegate
            {
                ApplyVisibility(strategyCard, sections.ShowStrategy);
                ApplyVisibility(entryCard, sections.ShowEntry);
                ApplyVisibility(activePosCard, sections.ShowActivePosition);
                ApplyVisibility(takesCard, sections.ShowTakes);
                ApplyVisibility(stopCard, sections.ShowStop);
                ApplyVisibility(protectionCard, sections.ShowProtection);
                ApplyVisibility(riskCard, sections.ShowRisk);
                ApplyVisibility(sessionCard, sections.ShowSession);
                ApplyVisibility(observationCard, sections.ShowObservation);
            });
        }

        public void ResetVisualToIdle()
        {
            RunOnUi(delegate
            {
                if (connDot != null) connDot.Fill = EssencialChartGuardTheme.AccentDotIdle;
                if (headerSubtitle != null) headerSubtitle.Text = "—";

                if (summaryPositionText != null)
                {
                    summaryPositionText.Text = "-";
                    summaryPositionText.Foreground = EssencialChartGuardTheme.TextSecondary;
                }
                if (summaryQtyText != null) summaryQtyText.Text = "qty -";
                if (summaryAvgText != null) summaryAvgText.Text = "avg -";
                if (summaryWorkingOrdersText != null) summaryWorkingOrdersText.Text = "wo -";

                if (activePosHeader != null) activePosHeader.Text = "(sem posição ativa)";
                if (activePosPnL != null) activePosPnL.Text = " ";

                if (activePosDirectionValue != null)
                {
                    activePosDirectionValue.Text = "-";
                    activePosDirectionValue.Foreground = EssencialChartGuardTheme.TextSecondary;
                }
                if (activePosQtyValue != null) activePosQtyValue.Text = "-";
                if (activePosEntryValue != null) activePosEntryValue.Text = "-";
                if (activePosLastFillValue != null) activePosLastFillValue.Text = "-";
                if (activePosPnLTicksValue != null) activePosPnLTicksValue.Text = "-";
                if (activePosPnLPointsValue != null) activePosPnLPointsValue.Text = "-";
                if (activePosPnLPercentValue != null) activePosPnLPercentValue.Text = "-";
                if (activePosPnLCashValue != null) activePosPnLCashValue.Text = "-";
                if (activePosWorkingOrdersValue != null) activePosWorkingOrdersValue.Text = "-";
                if (activePosStopValue != null) activePosStopValue.Text = "-";
                if (activePosTargetsValue != null) activePosTargetsValue.Text = "-";
                if (activePosProtectionValue != null) activePosProtectionValue.Text = "-";

                if (snapshotDot != null) snapshotDot.Fill = EssencialChartGuardTheme.AccentDotIdle;
                if (snapshotText != null) snapshotText.Text = "not read yet";
                if (bridgeDot != null) bridgeDot.Fill = EssencialChartGuardTheme.AccentDotIdle;
                if (bridgeText != null) bridgeText.Text = "not subscribed";

                if (saldoValue != null) saldoValue.Text = "—";
                if (quebraEmValue != null) quebraEmValue.Text = "—";
                if (pctUsedValue != null) pctUsedValue.Text = "—";
                if (metaProgressText != null) metaProgressText.Text = "Meta diária: —";

                if (riskDailyLimitValue != null) riskDailyLimitValue.Text = "-";
                if (riskStatusValue != null) riskStatusValue.Text = "no risk data yet";
                if (riskBlockStatusValue != null) riskBlockStatusValue.Text = "-";

                if (sessionTradesValue != null) sessionTradesValue.Text = "-";
                if (sessionPnLValue != null) sessionPnLValue.Text = "-";
                if (sessionTimeValue != null) sessionTimeValue.Text = "no session data yet";

                if (takesList != null) RebuildTakesPlaceholder();
                if (stopChipsHost != null) stopChipsHost.Children.Clear();
                if (stopCurrentValue != null) stopCurrentValue.Text = "-";
                if (takesEmptyHint != null)
                {
                    takesEmptyHint.Text = "(no targets defined)";
                    takesEmptyHint.FontStyle = FontStyles.Italic;
                }
            });
        }

        // Toast overlay public API. Visual-only; never wired to a command path
        // in this build. Kept for future phases.
        public void ShowToast(string message, ToastKind kind = ToastKind.Info)
        {
            if (string.IsNullOrEmpty(message)) return;
            RunOnUi(delegate
            {
                if (toastHost == null || toastText == null) return;
                toastText.Text = message;
                switch (kind)
                {
                    case ToastKind.Warn:
                        toastHost.BorderBrush = EssencialChartGuardTheme.AccentWarn;
                        toastText.Foreground = EssencialChartGuardTheme.AccentWarn;
                        break;
                    case ToastKind.Error:
                        toastHost.BorderBrush = EssencialChartGuardTheme.AccentDanger;
                        toastText.Foreground = EssencialChartGuardTheme.AccentDanger;
                        break;
                    default:
                        toastHost.BorderBrush = EssencialChartGuardTheme.Gold;
                        toastText.Foreground = EssencialChartGuardTheme.TextPrimary;
                        break;
                }
                toastHost.Visibility = Visibility.Visible;
                if (toastTimer != null)
                {
                    toastTimer.Stop();
                    toastTimer.Start();
                }
            });
        }

        // =====================================================================
        // Layout (mirrors the legacy panel; no event handlers attached)
        // =====================================================================

        private void BuildUI()
        {
            DockPanel root = new DockPanel
            {
                LastChildFill = true,
                Background = EssencialChartGuardTheme.BgRoot
            };

            Border header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            ScrollViewer scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(EssencialChartGuardTheme.SpaceLg, 0,
                                        EssencialChartGuardTheme.SpaceLg,
                                        EssencialChartGuardTheme.SpaceLg)
            };

            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            // Bare card on top: Modo + Unidade + Conta + Instrumento + ATM
            StackPanel topGroup = new StackPanel { Orientation = Orientation.Vertical };
            topGroup.Children.Add(BuildModeBar());
            topGroup.Children.Add(BuildAccountInstrumentBar());
            topGroup.Children.Add(BuildAtmStrategyBar());
            content.Children.Add(EssencialChartGuardTheme.MakeBareCard(topGroup));

            // Header summary chip line lives just below the bare card so the
            // observed Position/qty/avg/wo are easy to read. Keeps the host's
            // SetObservedState wiring identical.
            content.Children.Add(BuildHeaderSummaryRow());

            content.Children.Add(BuildEntrySection());
            content.Children.Add(BuildActivePositionSection());
            content.Children.Add(BuildRiskSection());
            content.Children.Add(BuildSessionSection());
            content.Children.Add(BuildObservationCard());

            scroller.Content = content;
            root.Children.Add(scroller);

            Grid layered = new Grid();
            layered.Children.Add(root);
            layered.Children.Add(BuildToastOverlay());

            Content = layered;
        }

        private Border BuildHeader()
        {
            Grid grid = new Grid
            {
                Margin = new Thickness(EssencialChartGuardTheme.SpaceMd, EssencialChartGuardTheme.SpaceSm,
                                        EssencialChartGuardTheme.SpaceMd, EssencialChartGuardTheme.SpaceSm)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            connDot = new Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = EssencialChartGuardTheme.AccentDotIdle,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, EssencialChartGuardTheme.SpaceSm, 0),
                ToolTip = EssencialChartGuardTheme.WrapTooltip("Status da conexão / modo de operação.")
            };
            ToolTipService.SetInitialShowDelay(connDot, 350);
            Grid.SetColumn(connDot, 0);
            grid.Children.Add(connDot);

            StackPanel titleStack = new StackPanel { Orientation = Orientation.Vertical };
            headerTitle = new TextBlock
            {
                Text = "Essencial ChartGuard",
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeTitle,
                FontWeight = FontWeights.Bold,
                Foreground = EssencialChartGuardTheme.Gold
            };
            headerSubtitle = new TextBlock
            {
                Text = "—",
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeSmall,
                Foreground = EssencialChartGuardTheme.TextSecondary
            };
            titleStack.Children.Add(headerTitle);
            titleStack.Children.Add(headerSubtitle);
            Grid.SetColumn(titleStack, 1);
            grid.Children.Add(titleStack);

            warnButton = MakeIconButton("⚠",
                "Aviso. Visual reservado; nenhuma ação está ligada nesta versão.");
            warnButton.Foreground = EssencialChartGuardTheme.AccentWarn;
            warnButton.Visibility = Visibility.Collapsed;
            warnButton.IsEnabled = false;
            Grid.SetColumn(warnButton, 2);
            grid.Children.Add(warnButton);

            settingsButton = MakeGoldIconButton("⚙",
                "Configurações (preview / disabled). Não está ligada nesta versão.");
            settingsButton.IsEnabled = false;
            Grid.SetColumn(settingsButton, 3);
            grid.Children.Add(settingsButton);

            return new Border
            {
                Background = EssencialChartGuardTheme.BgSurface,
                BorderBrush = EssencialChartGuardTheme.GoldDim,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = grid
            };
        }

        // Compact summary row carried from the previous panel: one-line
        // "<Position> · qty <n> · avg <price> · wo <n>" so the observed
        // state is glanceable.
        private FrameworkElement BuildHeaderSummaryRow()
        {
            StackPanel summary = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, EssencialChartGuardTheme.SpaceSm, 0, EssencialChartGuardTheme.SpaceSm)
            };

            summaryPositionText = new TextBlock
            {
                Text = "-",
                Foreground = EssencialChartGuardTheme.TextSecondary,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeValue,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            summary.Children.Add(summaryPositionText);
            summary.Children.Add(BuildHeaderSeparator());

            summaryQtyText = BuildHeaderChip("qty -");
            summary.Children.Add(summaryQtyText);
            summary.Children.Add(BuildHeaderSeparator());

            summaryAvgText = BuildHeaderChip("avg -");
            summary.Children.Add(summaryAvgText);
            summary.Children.Add(BuildHeaderSeparator());

            summaryWorkingOrdersText = BuildHeaderChip("wo -");
            summary.Children.Add(summaryWorkingOrdersText);

            return summary;
        }

        private static TextBlock BuildHeaderChip(string text)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = EssencialChartGuardTheme.TextPrimary,
                FontFamily = EssencialChartGuardTheme.FontMono,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static FrameworkElement BuildHeaderSeparator()
        {
            return new TextBlock
            {
                Text = "  ·  ",
                Foreground = EssencialChartGuardTheme.TextMuted,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private Border BuildModeBar()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel modeCol = MakeFieldColumn("Modo");
            modeCombo = EssencialChartGuardTheme.MakeCombo(
                "Preset operacional. Visual reservado; ainda não muda comportamento.");
            modeCombo.Items.Add("Apertado");
            modeCombo.Items.Add("Médio");
            modeCombo.Items.Add("Longo");
            modeCombo.Items.Add("Swing");
            modeCombo.SelectedIndex = 0;
            DisableInput(modeCombo);
            modeCol.Children.Add(modeCombo);
            Grid.SetColumn(modeCol, 0);
            g.Children.Add(modeCol);

            StackPanel unitCol = MakeFieldColumn("Unidade");
            unitCombo = EssencialChartGuardTheme.MakeCombo(
                "Como exibir distâncias (ticks / pontos / moeda). Preview / disabled.");
            unitCombo.Items.Add("Ticks");
            unitCombo.Items.Add("Pontos");
            unitCombo.Items.Add("Moeda");
            unitCombo.SelectedIndex = 0;
            DisableInput(unitCombo);
            unitCol.Children.Add(unitCombo);
            Grid.SetColumn(unitCol, 1);
            g.Children.Add(unitCol);

            return new Border { Margin = new Thickness(0, 0, 0, EssencialChartGuardTheme.SpaceSm), Child = g };
        }

        private Border BuildAccountInstrumentBar()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel accCol = MakeFieldColumn("Conta");
            accountCombo = EssencialChartGuardTheme.MakeCombo(
                "Conta detectada pelo host. Preview / disabled (a seleção real vem do indicator).");
            DisableInput(accountCombo);
            accCol.Children.Add(accountCombo);
            Grid.SetColumn(accCol, 0);
            g.Children.Add(accCol);

            StackPanel instCol = MakeFieldColumn("Instrumento");
            instrumentCombo = EssencialChartGuardTheme.MakeCombo(
                "Instrumento detectado do chart. Preview / disabled.");
            instrumentCombo.IsEditable = false;
            DisableInput(instrumentCombo);
            instCol.Children.Add(instrumentCombo);
            Grid.SetColumn(instCol, 1);
            g.Children.Add(instCol);

            return new Border { Margin = new Thickness(0, 0, 0, EssencialChartGuardTheme.SpaceSm), Child = g };
        }

        private Border BuildAtmStrategyBar()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel atmCol = MakeFieldColumn("Estratégia");
            atmCol.HorizontalAlignment = HorizontalAlignment.Stretch;
            atmCol.Margin = new Thickness(0);
            atmStrategyCombo = EssencialChartGuardTheme.MakeCombo(
                "Estratégia (preview). Persistência e seleção real entram em fase futura.",
                minWidth: 200);
            atmStrategyCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            atmStrategyCombo.Items.Add("Personalizada");
            atmStrategyCombo.SelectedIndex = 0;
            DisableInput(atmStrategyCombo);
            atmCol.Children.Add(atmStrategyCombo);
            Grid.SetColumn(atmCol, 0);
            Grid.SetColumnSpan(atmCol, 2);
            g.Children.Add(atmCol);

            // Strategy is the first card; expose the combobox to SetStrategyDraft.
            strategySelect = atmStrategyCombo;

            return new Border { Margin = new Thickness(0, 0, 0, 0), Child = g };
        }

        private Border BuildEntrySection()
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Vertical };

            // Top row: Tipo / @Preço (collapsed) / Qtd / Sizing %
            Grid topRow = new Grid { Margin = new Thickness(0, 0, 0, EssencialChartGuardTheme.SpaceXs) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel tipoCol = MakeFieldColumn("Tipo");
            orderTypeCombo = EssencialChartGuardTheme.MakeCombo(
                "Tipo da ordem de entrada (Mercado / Limite). Preview / disabled.",
                minWidth: 95);
            orderTypeCombo.Items.Add("Mercado");
            orderTypeCombo.Items.Add("Limite");
            orderTypeCombo.SelectedIndex = 0;
            DisableInput(orderTypeCombo);
            tipoCol.Children.Add(orderTypeCombo);
            Grid.SetColumn(tipoCol, 0);
            topRow.Children.Add(tipoCol);

            limitPriceRow = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(EssencialChartGuardTheme.SpaceSm, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            TextBlock priceLabel = EssencialChartGuardTheme.MakeLabel("@ Preço", "field");
            priceLabel.FontSize = EssencialChartGuardTheme.FontSizeSmall;
            priceLabel.Margin = new Thickness(0, 0, 0, 1);
            limitPriceRow.Children.Add(priceLabel);
            limitPriceBox = EssencialChartGuardTheme.MakeNumberBox("",
                "Preço limite (preview / disabled).", width: 90);
            DisableInput(limitPriceBox);
            limitPriceRow.Children.Add(limitPriceBox);
            Grid.SetColumn(limitPriceRow, 1);
            topRow.Children.Add(limitPriceRow);

            StackPanel qtyCol = MakeFieldColumn("Qtd");
            qtyCol.Margin = new Thickness(EssencialChartGuardTheme.SpaceLg, 0, 0, 0);
            StackPanel qtySpinner = EssencialChartGuardTheme.MakeNumericUpDown(
                defaultText: "1",
                tooltip: "Quantidade de contratos. Preview / disabled.",
                inputBox: out qtyBox,
                inputWidth: 44, step: 1, min: 1, max: 9999);
            DisableInput(qtyBox);
            DisableChildren(qtySpinner);
            qtyCol.Children.Add(qtySpinner);
            Grid.SetColumn(qtyCol, 2);
            topRow.Children.Add(qtyCol);

            StackPanel sizingCol = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(EssencialChartGuardTheme.SpaceLg, 0, 0, 0)
            };
            TextBlock spacer = new TextBlock
            {
                Text = " ",
                FontSize = EssencialChartGuardTheme.FontSizeSmall,
                Margin = new Thickness(0, 0, 0, 3)
            };
            sizingCol.Children.Add(spacer);

            Grid sizingRow = new Grid
            {
                MinHeight = 28,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            sizingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sizingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            usePosSizingCheck = new CheckBox
            {
                Content = " Sizing %",
                Foreground = EssencialChartGuardTheme.Gold,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeSmall,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, EssencialChartGuardTheme.SpaceMd, 0)
            };
            usePosSizingCheck.ToolTip = EssencialChartGuardTheme.WrapTooltip(
                "Sizing por % de risco. Preview / disabled.");
            ToolTipService.SetInitialShowDelay(usePosSizingCheck, 350);
            DisableInput(usePosSizingCheck);
            Grid.SetColumn(usePosSizingCheck, 0);
            sizingRow.Children.Add(usePosSizingCheck);

            riscoPctBox = EssencialChartGuardTheme.MakeNumberBox("0,5",
                "Risco por trade (preview / disabled).", width: 50);
            riscoPctBox.VerticalAlignment = VerticalAlignment.Center;
            DisableInput(riscoPctBox);
            Grid.SetColumn(riscoPctBox, 1);
            sizingRow.Children.Add(riscoPctBox);
            sizingCol.Children.Add(sizingRow);
            Grid.SetColumn(sizingCol, 3);
            topRow.Children.Add(sizingCol);

            sp.Children.Add(topRow);

            sizingResultText = EssencialChartGuardTheme.MakeLabel("(sizing manual: usando Qtd direta)", "muted");
            sizingResultText.FontSize = EssencialChartGuardTheme.FontSizeSmall;
            sizingResultText.FontStyle = FontStyles.Italic;
            sizingResultText.Margin = new Thickness(0, EssencialChartGuardTheme.SpaceXs, 0, EssencialChartGuardTheme.SpaceSm);
            sp.Children.Add(sizingResultText);

            // BUY / SELL big buttons
            Grid btnRow = new Grid { Margin = new Thickness(0, EssencialChartGuardTheme.SpaceXs, 0, EssencialChartGuardTheme.SpaceMd) };
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceMd) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            buyButton = EssencialChartGuardTheme.MakeButton("▲ COMPRAR", ButtonRole.Buy,
                "COMPRAR (preview / disabled). A rota de envio entra em fase futura via TradeCommandService.");
            buyButton.MinHeight = 54;
            buyButton.FontSize = EssencialChartGuardTheme.FontSizeButtonBig;
            DisableInput(buyButton);
            Grid.SetColumn(buyButton, 0);
            btnRow.Children.Add(buyButton);

            sellButton = EssencialChartGuardTheme.MakeButton("▼ VENDER", ButtonRole.Sell,
                "VENDER (preview / disabled). A rota de envio entra em fase futura via TradeCommandService.");
            sellButton.MinHeight = 54;
            sellButton.FontSize = EssencialChartGuardTheme.FontSizeButtonBig;
            DisableInput(sellButton);
            Grid.SetColumn(sellButton, 2);
            btnRow.Children.Add(sellButton);

            sp.Children.Add(btnRow);

            // PÂNICO
            panicButton = EssencialChartGuardTheme.MakeButton(string.Empty, ButtonRole.Danger,
                "PÂNICO — cancelar tudo + flatten (preview / disabled). Visual reservado.");
            TextBlock panicLabel = new TextBlock
            {
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeButton,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panicLabel.Inlines.Add(new System.Windows.Documents.Run("⚠")
            { Foreground = EssencialChartGuardTheme.Gold, FontSize = 16 });
            panicLabel.Inlines.Add(new System.Windows.Documents.Run("  PÂNICO — CANCELAR TUDO  ")
            { Foreground = EssencialChartGuardTheme.TextPrimary });
            panicButton.Content = panicLabel;
            panicButton.MinHeight = 40;
            panicButton.Margin = new Thickness(0, EssencialChartGuardTheme.SpaceMd, 0, EssencialChartGuardTheme.SpaceXs);
            DisableInput(panicButton);
            sp.Children.Add(panicButton);

            // Aliases the host already populates via SetEntryPlanDraft etc.
            entryTypeSelect = orderTypeCombo;
            entryQtyBox = qtyBox;
            entrySizingSelect = null; // sizing comes from a checkbox, kept null
            entryUnitSelect = unitCombo;
            // entryStopBox / entryTargetBox set by the active-position chips path below.

            entryCard = EssencialChartGuardTheme.MakeSection("Entrada", sp,
                "Disparo de novo trade. Tudo aqui é preview / disabled nesta versão.");
            return entryCard;
        }

        private Border BuildActivePositionSection()
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Vertical };

            activePosHeader = new TextBlock
            {
                Text = "(sem posição ativa)",
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeBody,
                Foreground = EssencialChartGuardTheme.TextMuted,
                Margin = new Thickness(0, 0, 0, EssencialChartGuardTheme.SpaceXs)
            };
            sp.Children.Add(activePosHeader);

            activePosPnL = new TextBlock
            {
                Text = " ",
                FontFamily = EssencialChartGuardTheme.FontMono,
                FontSize = EssencialChartGuardTheme.FontSizeValue,
                Foreground = EssencialChartGuardTheme.TextSecondary,
                Margin = new Thickness(0, 0, 0, EssencialChartGuardTheme.SpaceSm)
            };
            sp.Children.Add(activePosPnL);

            // Detailed observed-state rows (mirror what the host already pushes)
            sp.Children.Add(BuildLabelValueRow("Direction", out activePosDirectionValue));
            sp.Children.Add(BuildLabelValueRow("Qty", out activePosQtyValue));
            sp.Children.Add(BuildLabelValueRow("Entry / Avg", out activePosEntryValue));
            sp.Children.Add(BuildLabelValueRow("Last fill", out activePosLastFillValue));
            sp.Children.Add(BuildLabelValueRow("PnL ticks", out activePosPnLTicksValue));
            sp.Children.Add(BuildLabelValueRow("PnL points", out activePosPnLPointsValue));
            sp.Children.Add(BuildLabelValueRow("PnL %", out activePosPnLPercentValue));
            sp.Children.Add(BuildLabelValueRow("PnL $", out activePosPnLCashValue));
            sp.Children.Add(BuildLabelValueRow("Working orders", out activePosWorkingOrdersValue));
            sp.Children.Add(BuildLabelValueRow("Stop", out activePosStopValue));
            sp.Children.Add(BuildLabelValueRow("Targets", out activePosTargetsValue));
            sp.Children.Add(BuildLabelValueRow("Protection", out activePosProtectionValue));

            // Takes inline chips row
            Grid takesInline = BuildInlineChipsRow(
                labelText: "Takes",
                labelTooltip: "Alvos. Preview / disabled. Adicionar pelo + ainda não está ligado.",
                inputAssign: tb => takeInputBox = tb,
                addAssign: bt => addTakeButton = bt,
                addTooltip: "Adicionar alvo (preview / disabled).",
                chipsHost: out takesList);
            sp.Children.Add(takesInline);

            // Stop inline + trail combo on the same Grid
            Grid stopRow = new Grid { Margin = new Thickness(0, EssencialChartGuardTheme.SpaceSm, 0, EssencialChartGuardTheme.SpaceSm) };
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceSm) });
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceSm) });
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceSm) });
            stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid stopInlineRow = BuildInlineChipsRow(
                labelText: "Stop",
                labelTooltip: "Stop loss. Preview / disabled.",
                inputAssign: tb => stopInputBox = tb,
                addAssign: bt => setStopButton = bt,
                addTooltip: "Aplicar valor (preview / disabled).",
                chipsHost: out WrapPanel stopChipsWrap);
            stopChipsHost = stopChipsWrap;
            stopValueText = new TextBlock { Text = "—" };
            Grid.SetColumn(stopInlineRow, 0);
            Grid.SetColumnSpan(stopInlineRow, 5);
            stopRow.Children.Add(stopInlineRow);

            StackPanel trailCol = MakeFieldColumn("Stop móvel");
            trailCol.HorizontalAlignment = HorizontalAlignment.Stretch;
            trailCol.Margin = new Thickness(0);
            trailCombo = EssencialChartGuardTheme.MakeCombo(
                "Trail / stop móvel (preview / disabled).", minWidth: 90);
            trailCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            trailCombo.Items.Add("Off");
            trailCombo.Items.Add("A cada 1pt");
            trailCombo.Items.Add("A cada 5pts");
            trailCombo.Items.Add("A cada 10pts");
            trailCombo.SelectedIndex = 0;
            DisableInput(trailCombo);
            trailCol.Children.Add(trailCombo);
            Grid.SetColumn(trailCol, 6);
            stopRow.Children.Add(trailCol);
            sp.Children.Add(stopRow);

            // Lock R + BE row
            Grid lockRow = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceSm) });
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceSm) });
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EssencialChartGuardTheme.SpaceSm) });
            lockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            lock1RBtn = EssencialChartGuardTheme.MakeButton("Travar 1R", ButtonRole.Secondary,
                "Travar 1R (preview / disabled).");
            lock1RBtn.MinHeight = 32;
            DisableInput(lock1RBtn);
            Grid.SetColumn(lock1RBtn, 0);
            lockRow.Children.Add(lock1RBtn);

            lock2RBtn = EssencialChartGuardTheme.MakeButton("Travar 2R", ButtonRole.Secondary,
                "Travar 2R (preview / disabled).");
            lock2RBtn.MinHeight = 32;
            DisableInput(lock2RBtn);
            Grid.SetColumn(lock2RBtn, 2);
            lockRow.Children.Add(lock2RBtn);

            lock3RBtn = EssencialChartGuardTheme.MakeButton("Travar 3R", ButtonRole.Secondary,
                "Travar 3R (preview / disabled).");
            lock3RBtn.MinHeight = 32;
            DisableInput(lock3RBtn);
            Grid.SetColumn(lock3RBtn, 4);
            lockRow.Children.Add(lock3RBtn);

            beBtn = EssencialChartGuardTheme.MakeButton("→ BE", ButtonRole.Info,
                "Breakeven (preview / disabled).");
            beBtn.MinHeight = 32;
            DisableInput(beBtn);
            Grid.SetColumn(beBtn, 6);
            lockRow.Children.Add(beBtn);

            sp.Children.Add(lockRow);

            // Aliases used by the existing host mutators. Take/Stop visuals
            // double as the entry-plan stop/target preview targets.
            entryStopBox = stopInputBox;
            entryTargetBox = takeInputBox;
            takesEmptyHint = new TextBlock();   // placeholder so SetTakeTargetsDraft never NRE's
            stopCurrentValue = stopValueText;
            takesCard = null;                   // visibility flags map to activePosCard for now
            stopCard = null;
            protectionCard = null;
            strategyCard = null;
            riskModeSelect = null;              // risk-mode preview lives in the risk card below

            activePosCard = EssencialChartGuardTheme.MakeSection("Posição ativa", sp,
                "Aparece dados quando há posição aberta. Botões preview / disabled.");
            return activePosCard;
        }

        private Border BuildRiskSection()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());

            StackPanel saldoCol = MakeMetricColumn("Saldo", "Saldo da conta (preview).");
            saldoValue = MakeMetricValue("—");
            saldoCol.Children.Add(saldoValue);
            Grid.SetRow(saldoCol, 0); Grid.SetColumn(saldoCol, 0);
            g.Children.Add(saldoCol);

            StackPanel quebraCol = MakeMetricColumn("Quebra em",
                "Saldo abaixo do qual a regra zera a conta (preview).");
            quebraEmValue = MakeMetricValue("—");
            quebraEmValue.Foreground = EssencialChartGuardTheme.AccentSell;
            quebraCol.Children.Add(quebraEmValue);
            Grid.SetRow(quebraCol, 0); Grid.SetColumn(quebraCol, 1);
            g.Children.Add(quebraCol);

            StackPanel pctCol = MakeMetricColumn("% comprometido",
                "% do colchão até a Quebra (preview).");
            pctUsedValue = MakeMetricValue("—");
            pctCol.Children.Add(pctUsedValue);
            Grid.SetRow(pctCol, 0); Grid.SetColumn(pctCol, 2);
            g.Children.Add(pctCol);

            pctUsedBar = EssencialChartGuardTheme.MakeProgressBar(0, EssencialChartGuardTheme.RiskGreen, 6);
            pctUsedBar.Margin = new Thickness(0, EssencialChartGuardTheme.SpaceSm, 0, EssencialChartGuardTheme.SpaceMd);
            Grid.SetRow(pctUsedBar, 1); Grid.SetColumn(pctUsedBar, 0); Grid.SetColumnSpan(pctUsedBar, 3);
            g.Children.Add(pctUsedBar);

            metaProgressText = EssencialChartGuardTheme.MakeLabel("Meta diária: —", "secondary");
            metaProgressText.ToolTip = EssencialChartGuardTheme.WrapTooltip(
                "Quanto você já fez do Daily Profit Cap (preview).");
            ToolTipService.SetInitialShowDelay(metaProgressText, 350);
            Grid.SetRow(metaProgressText, 2); Grid.SetColumn(metaProgressText, 0); Grid.SetColumnSpan(metaProgressText, 3);
            g.Children.Add(metaProgressText);

            metaBar = EssencialChartGuardTheme.MakeProgressBar(0, EssencialChartGuardTheme.AccentInfo, 6);
            metaBar.Margin = new Thickness(0, EssencialChartGuardTheme.SpaceXs, 0, 0);
            Grid.SetRow(metaBar, 3); Grid.SetColumn(metaBar, 0); Grid.SetColumnSpan(metaBar, 3);
            g.Children.Add(metaBar);

            // Three labelled rows for the host's SetRiskMetrics
            FrameworkElement r4 = BuildLabelValueRow("Daily limit", out riskDailyLimitValue);
            Grid.SetRow(r4, 4); Grid.SetColumnSpan(r4, 3);
            g.Children.Add(r4);
            FrameworkElement r5 = BuildLabelValueRow("Status", out riskStatusValue);
            Grid.SetRow(r5, 5); Grid.SetColumnSpan(r5, 3);
            g.Children.Add(r5);
            FrameworkElement r6 = BuildLabelValueRow("Block", out riskBlockStatusValue);
            // Append below the grid via a wrapping stack so the row count stays right.
            StackPanel riskOuter = new StackPanel { Orientation = Orientation.Vertical };
            riskOuter.Children.Add(g);
            riskOuter.Children.Add(r6);

            riskCard = EssencialChartGuardTheme.MakeSection("Risco da conta", riskOuter,
                "Status do drawdown / meta (preview). Sem persistência nesta versão.");
            return riskCard;
        }

        private Border BuildSessionSection()
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(BuildLabelValueRow("Trades", out sessionTradesValue));
            sp.Children.Add(BuildLabelValueRow("PnL", out sessionPnLValue));
            sp.Children.Add(BuildLabelValueRow("Time", out sessionTimeValue));
            sessionCard = EssencialChartGuardTheme.MakeSection("Sessão", sp,
                "Resumo da sessão atual (preview).");
            return sessionCard;
        }

        private Border BuildObservationCard()
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(BuildDotRow("Snapshot", out snapshotDot, out snapshotText));
            sp.Children.Add(BuildDotRow("Event bridge", out bridgeDot, out bridgeText));
            observationCard = EssencialChartGuardTheme.MakeSection("Observation", sp,
                "Estado do snapshot e do event bridge (read-only).");
            return observationCard;
        }

        private Border BuildToastOverlay()
        {
            toastText = new TextBlock
            {
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeBody,
                Foreground = EssencialChartGuardTheme.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            toastHost = new Border
            {
                Background = EssencialChartGuardTheme.BgSurface2,
                BorderBrush = EssencialChartGuardTheme.Gold,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(EssencialChartGuardTheme.Radius),
                Padding = new Thickness(EssencialChartGuardTheme.SpaceMd, EssencialChartGuardTheme.SpaceSm,
                                        EssencialChartGuardTheme.SpaceMd, EssencialChartGuardTheme.SpaceSm),
                Margin = new Thickness(EssencialChartGuardTheme.SpaceLg, 64, EssencialChartGuardTheme.SpaceLg, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                Child = toastText
            };

            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            toastTimer.Tick += OnToastTick;

            return toastHost;
        }

        // The toast tick is the only event handler on this control. It is a
        // visual auto-dismiss timer; it does not touch any account, order or
        // command. Kept as a named method so tooling can audit it easily.
        private void OnToastTick(object sender, EventArgs e)
        {
            if (toastTimer != null) toastTimer.Stop();
            if (toastHost != null) toastHost.Visibility = Visibility.Collapsed;
        }

        // =====================================================================
        // Inline chips row + small builders
        // =====================================================================

        private Grid BuildInlineChipsRow(string labelText, string labelTooltip,
            Action<TextBox> inputAssign, Action<Button> addAssign,
            string addTooltip, out WrapPanel chipsHost)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, EssencialChartGuardTheme.SpaceXs) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock label = EssencialChartGuardTheme.MakeLabel(labelText, "field");
            label.FontSize = EssencialChartGuardTheme.FontSizeSmall;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MinWidth = 38;
            label.Margin = new Thickness(0, 0, EssencialChartGuardTheme.SpaceSm, 0);
            if (!string.IsNullOrEmpty(labelTooltip))
            {
                label.ToolTip = EssencialChartGuardTheme.WrapTooltip(labelTooltip);
                ToolTipService.SetInitialShowDelay(label, 350);
                label.Cursor = Cursors.Help;
            }
            Grid.SetColumn(label, 0);
            g.Children.Add(label);

            TextBox input = EssencialChartGuardTheme.MakeNumberBox(string.Empty,
                "Distância na unidade selecionada (preview / disabled).", width: 60);
            input.MinHeight = 26;
            input.VerticalAlignment = VerticalAlignment.Center;
            DisableInput(input);
            Grid.SetColumn(input, 1);
            g.Children.Add(input);
            if (inputAssign != null) inputAssign(input);

            Button addBtn = new Button
            {
                Content = "+",
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = EssencialChartGuardTheme.Gold,
                Background = Brushes.Transparent,
                BorderBrush = EssencialChartGuardTheme.GoldDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                MinWidth = 26,
                MinHeight = 26,
                Margin = new Thickness(EssencialChartGuardTheme.SpaceXs, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
                Style = EssencialChartGuardTheme.MakeButtonStyle()
            };
            addBtn.ToolTip = EssencialChartGuardTheme.WrapTooltip(addTooltip);
            ToolTipService.SetInitialShowDelay(addBtn, 350);
            DisableInput(addBtn);
            Grid.SetColumn(addBtn, 2);
            g.Children.Add(addBtn);
            if (addAssign != null) addAssign(addBtn);

            chipsHost = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(EssencialChartGuardTheme.SpaceMd, 0, 0, 0)
            };
            Grid.SetColumn(chipsHost, 3);
            g.Children.Add(chipsHost);

            return g;
        }

        private void RebuildTakesPlaceholder()
        {
            takesList.Children.Clear();
            TextBlock empty = EssencialChartGuardTheme.MakeLabel("(nenhum take configurado)", "muted");
            empty.FontSize = EssencialChartGuardTheme.FontSizeSmall;
            empty.FontStyle = FontStyles.Italic;
            empty.Margin = new Thickness(2, 4, 0, 4);
            takesList.Children.Add(empty);
        }

        private static Button MakeIconButton(string glyph, string tooltip)
        {
            Button b = new Button
            {
                Content = glyph,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = 16,
                Foreground = EssencialChartGuardTheme.TextSecondary,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
                Style = EssencialChartGuardTheme.MakeButtonStyle()
            };
            b.ToolTip = EssencialChartGuardTheme.WrapTooltip(tooltip);
            ToolTipService.SetInitialShowDelay(b, 350);
            ToolTipService.SetShowDuration(b, 30000);
            return b;
        }

        private static Button MakeGoldIconButton(string glyph, string tooltip)
        {
            Button b = new Button
            {
                Content = glyph,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = EssencialChartGuardTheme.GoldDim,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
                Style = EssencialChartGuardTheme.MakeButtonStyle()
            };
            b.ToolTip = EssencialChartGuardTheme.WrapTooltip(tooltip);
            ToolTipService.SetInitialShowDelay(b, 350);
            ToolTipService.SetShowDuration(b, 30000);
            return b;
        }

        private static StackPanel MakeFieldColumn(string label)
        {
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, EssencialChartGuardTheme.SpaceMd, 0)
            };
            if (!string.IsNullOrEmpty(label) && label.Trim().Length > 0)
            {
                TextBlock l = EssencialChartGuardTheme.MakeLabel(label, "field");
                l.FontSize = EssencialChartGuardTheme.FontSizeSmall;
                l.HorizontalAlignment = HorizontalAlignment.Left;
                l.Margin = new Thickness(0, 0, 0, 3);
                sp.Children.Add(l);
            }
            return sp;
        }

        private static StackPanel MakeMetricColumn(string label, string tooltip)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Vertical };
            TextBlock l = EssencialChartGuardTheme.MakeLabel(label, "field");
            l.FontSize = EssencialChartGuardTheme.FontSizeSmall;
            l.Margin = new Thickness(0, 0, 0, 3);
            if (!string.IsNullOrEmpty(tooltip))
            {
                l.ToolTip = EssencialChartGuardTheme.WrapTooltip(tooltip);
                ToolTipService.SetInitialShowDelay(l, 350);
                l.Cursor = Cursors.Help;
            }
            sp.Children.Add(l);
            return sp;
        }

        private static TextBlock MakeMetricValue(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = EssencialChartGuardTheme.FontMono,
                FontSize = EssencialChartGuardTheme.FontSizeValue,
                FontWeight = FontWeights.SemiBold,
                Foreground = EssencialChartGuardTheme.TextPrimary
            };
        }

        private static FrameworkElement BuildLabelValueRow(string labelText, out TextBlock value)
        {
            Grid row = new Grid { Margin = EssencialChartGuardTheme.RowSpacing };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = EssencialChartGuardTheme.CreateLabel(labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            value = EssencialChartGuardTheme.CreateValue("-");
            value.VerticalAlignment = VerticalAlignment.Center;
            value.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(value, 1);
            row.Children.Add(value);
            return row;
        }

        private static FrameworkElement BuildDotRow(string labelText, out Ellipse dot, out TextBlock value)
        {
            Grid row = new Grid { Margin = EssencialChartGuardTheme.RowSpacing };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = EssencialChartGuardTheme.CreateLabel(labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            StackPanel right = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            dot = EssencialChartGuardTheme.CreateStatusDot(EssencialChartGuardTheme.AccentDotIdle);
            dot.Margin = new Thickness(0, 0, 6, 0);
            right.Children.Add(dot);

            value = new TextBlock
            {
                Text = "-",
                Foreground = EssencialChartGuardTheme.TextPrimary,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            right.Children.Add(value);

            Grid.SetColumn(right, 1);
            row.Children.Add(right);
            return row;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static void DisableInput(Control c)
        {
            if (c == null) return;
            c.IsEnabled = false;
            c.Focusable = false;
            c.IsTabStop = false;
            c.Cursor = Cursors.No;
        }

        // Note: this file lives in a namespace ending in `.Panel`, so the
        // unqualified name `Panel` resolves to the namespace rather than
        // System.Windows.Controls.Panel. The fully-qualified name avoids the
        // ambiguity.
        private static void DisableChildren(System.Windows.Controls.Panel p)
        {
            if (p == null) return;
            foreach (UIElement child in p.Children)
            {
                Control c = child as Control;
                if (c != null) DisableInput(c);
                System.Windows.Controls.Panel inner = child as System.Windows.Controls.Panel;
                if (inner != null) DisableChildren(inner);
            }
        }

        private static void ApplyVisibility(UIElement element, bool visible)
        {
            if (element == null) return;
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string NullToDash(string s)
        {
            return string.IsNullOrEmpty(s) ? "-" : s;
        }

        private static string QuantityToText(int qty)
        {
            return qty.ToString(CultureInfo.InvariantCulture);
        }

        private static void SetComboPlaceholder(ComboBox c, string text)
        {
            if (c == null) return;
            c.Items.Clear();
            ComboBoxItem item = new ComboBoxItem
            {
                Content = text ?? string.Empty,
                IsSelected = true
            };
            c.Items.Add(item);
        }

        private static string BuildTargetsSummary(TakeTargetDraft[] targets)
        {
            if (targets == null || targets.Length == 0) return string.Empty;
            string s = string.Empty;
            for (int i = 0; i < targets.Length; i++)
            {
                TakeTargetDraft t = targets[i];
                if (t == null) continue;
                if (s.Length > 0) s += " | ";
                string label = string.IsNullOrEmpty(t.Label) ? ("T" + (i + 1)) : t.Label;
                string qty = "x" + t.Quantity.ToString(CultureInfo.InvariantCulture);
                string val = string.IsNullOrEmpty(t.Value) ? "-" : t.Value;
                string unit = string.IsNullOrEmpty(t.Unit) ? string.Empty : (" " + t.Unit);
                s += label + " " + qty + " @ " + val + unit;
            }
            return s;
        }

        private static string BuildProtectionSummary(ProtectionDraft draft)
        {
            if (draft == null) return string.Empty;
            if (!string.IsNullOrEmpty(draft.Summary)) return draft.Summary;
            List<string> parts = new List<string>();
            if (draft.BreakevenEnabled) parts.Add("BE");
            if (draft.Lock1REnabled) parts.Add("Lock 1R");
            if (draft.Lock2REnabled) parts.Add("Lock 2R");
            if (draft.Lock3REnabled) parts.Add("Lock 3R");
            if (draft.TrailEnabled) parts.Add("Trail");
            return string.Join(" | ", parts.ToArray());
        }

        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (Dispatcher == null || Dispatcher.CheckAccess())
            {
                try { action(); } catch { /* never let UI updates surface as crashes */ }
                return;
            }
            try { Dispatcher.BeginInvoke(action); }
            catch { /* dispatcher may already be tearing down -- ignore */ }
        }

        private static Brush ResolveDot(ConnectionDot dot)
        {
            switch (dot)
            {
                case ConnectionDot.Ok: return EssencialChartGuardTheme.AccentDotOk;
                case ConnectionDot.Warn: return EssencialChartGuardTheme.AccentDotWarn;
                case ConnectionDot.Error: return EssencialChartGuardTheme.AccentDotError;
                case ConnectionDot.Idle:
                default:
                    return EssencialChartGuardTheme.AccentDotIdle;
            }
        }

        private static Brush ResolvePositionBrush(ObservedPosition position)
        {
            switch (position)
            {
                case ObservedPosition.Long: return EssencialChartGuardTheme.AccentBuy;
                case ObservedPosition.Short: return EssencialChartGuardTheme.AccentSell;
                case ObservedPosition.Flat: return EssencialChartGuardTheme.TextPrimary;
                case ObservedPosition.Unknown:
                default:
                    return EssencialChartGuardTheme.TextSecondary;
            }
        }
    }

    // Status dot color picked by the host without exposing brushes.
    public enum ConnectionDot
    {
        Idle,
        Ok,
        Warn,
        Error
    }

    // Toast severity. Visual-only. Public so a future host can dispatch
    // user-facing notifications without coupling to the panel internals.
    public enum ToastKind
    {
        Info,
        Warn,
        Error
    }

    // Section visibility flags. Same shape as before so the host's
    // SetSectionsVisibility call site does not need to change.
    public struct EssencialChartGuardPanelSections
    {
        public bool ShowStrategy;
        public bool ShowEntry;
        public bool ShowActivePosition;
        public bool ShowTakes;
        public bool ShowStop;
        public bool ShowProtection;
        public bool ShowRisk;
        public bool ShowSession;
        public bool ShowObservation;

        public static EssencialChartGuardPanelSections All()
        {
            EssencialChartGuardPanelSections s;
            s.ShowStrategy = true;
            s.ShowEntry = true;
            s.ShowActivePosition = true;
            s.ShowTakes = true;
            s.ShowStop = true;
            s.ShowProtection = true;
            s.ShowRisk = true;
            s.ShowSession = true;
            s.ShowObservation = true;
            return s;
        }
    }
}

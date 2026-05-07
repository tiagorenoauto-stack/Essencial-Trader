using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.Models;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel
{
    // Phase 1 visual shell of the Essencial ChartGuard side panel.
    //
    // CONTRACT (do NOT loosen without updating docs/manual-test-checklist.md):
    //   * Read-only. The panel renders observed state and disabled placeholders for the
    //     final operational layout (header, strategy, entry, active position, takes, stop,
    //     protection, risk, session, observation). NO control here issues a command, sends
    //     an order, cancels an order, modifies an order, or flattens a position.
    //   * Every actionable control (Button / ComboBox / TextBox / settings icon) is built
    //     with IsEnabled=false. None of them attaches Click/MouseDown/MouseUp/PreviewMouse
    //     handlers. There is no ContextMenu, no KeyBinding, no InputBindings, no hotkey.
    //   * No reference to NinjaTrader.Cbi / NinjaTrader.Data trading types.
    //   * No code, no class names, no namespaces, no visible strings inherited from any
    //     historical CunhaTrader / CunhaScalper / Gold panel. The visual *direction* (right
    //     side, dark dense card layout, gold section labels, big buy/sell/panic) is
    //     inspired only by docs/panel-visual-audit-cunha.md and docs/plano-integrado-chartguard-pt.md.
    //
    // The control is built entirely in code (no XAML) so it ships with the rest of the .cs
    // files into NinjaTrader's user folder without an extra resource pipeline.
    public sealed class EssencialChartGuardPanel : UserControl
    {
        // ---- Header ----
        private TextBlock brandText;
        private System.Windows.Shapes.Ellipse connectionDot;
        private TextBlock modeText;
        private Button settingsIconButton; // gear icon, disabled
        private TextBlock accountInstrumentText;
        private TextBlock summaryPositionText;
        private TextBlock summaryQtyText;
        private TextBlock summaryAvgText;
        private TextBlock summaryWorkingOrdersText;

        // ---- Strategy section (disabled) ----
        private Border strategyCard;
        private ComboBox strategySelect;
        private Button strategyAddButton;
        private Button strategyEditButton;
        private Button strategyDuplicateButton;
        private Button strategyDeleteButton;

        // ---- Entry section (disabled) ----
        private Border entryCard;
        private ComboBox entryTypeSelect;
        private TextBox entryQtyBox;
        private ComboBox entrySizingSelect;
        private ComboBox entryUnitSelect;
        private TextBox entryStopBox;
        private TextBox entryTargetBox;
        private Button entryBuyButton;
        private Button entrySellButton;
        private Button entryPanicButton;

        // ---- Active Position section (read-only) ----
        private Border activePositionCard;
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

        // ---- Takes section (disabled) ----
        private Border takesCard;
        private TextBlock takesEmptyHint;
        private Button takesAddButton;
        private Button takesEditButton;
        private Button takesRemoveButton;

        // ---- Stop section (disabled) ----
        private Border stopCard;
        private TextBlock stopCurrentValue;
        private Button stopEditButton;

        // ---- Protection section (disabled) ----
        private Border protectionCard;
        private Button protectionBeButton;
        private Button protectionLock1RButton;
        private Button protectionLock2RButton;
        private Button protectionLock3RButton;
        private Button protectionTrailButton;

        // ---- Risk section (read-only + disabled mode select) ----
        private Border riskCard;
        private TextBlock riskDailyLimitValue;
        private TextBlock riskStatusValue;
        private TextBlock riskBlockStatusValue;
        private ComboBox riskModeSelect;

        // ---- Session section (read-only) ----
        private Border sessionCard;
        private TextBlock sessionTradesValue;
        private TextBlock sessionPnLValue;
        private TextBlock sessionTimeValue;

        // ---- Observation section (read-only) ----
        private Border observationCard;
        private System.Windows.Shapes.Ellipse snapshotDot;
        private TextBlock snapshotText;
        private System.Windows.Shapes.Ellipse bridgeDot;
        private TextBlock bridgeText;

        public EssencialChartGuardPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = EssencialChartGuardTheme.BackgroundRoot;
            Focusable = false;

            ScrollViewer scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = EssencialChartGuardTheme.PanelOuterPadding,
                Background = EssencialChartGuardTheme.BackgroundRoot,
                Focusable = false
            };

            StackPanel root = new StackPanel { Orientation = Orientation.Vertical };
            scroller.Content = root;
            Content = scroller;

            root.Children.Add(BuildHeader());
            root.Children.Add(BuildStrategyCard());
            root.Children.Add(BuildEntryCard());
            root.Children.Add(BuildActivePositionCard());
            root.Children.Add(BuildTakesCard());
            root.Children.Add(BuildStopCard());
            root.Children.Add(BuildProtectionCard());
            root.Children.Add(BuildRiskCard());
            root.Children.Add(BuildSessionCard());
            root.Children.Add(BuildObservationCard());

            ResetVisualToIdle();
        }

        // ============================================================================
        // Public mutators — host-only. NONE of them emits any order/command.
        // ============================================================================

        public void SetConnectionStatus(ConnectionDot dot, string modeLine)
        {
            RunOnUi(delegate
            {
                if (connectionDot != null) connectionDot.Fill = ResolveDot(dot);
                if (modeText != null) modeText.Text = modeLine ?? string.Empty;
            });
        }

        public void SetAccountAndInstrument(string accountName, string instrumentFullName)
        {
            RunOnUi(delegate
            {
                if (accountInstrumentText == null) return;
                string acc = string.IsNullOrEmpty(accountName) ? "?" : accountName;
                string ins = string.IsNullOrEmpty(instrumentFullName) ? "?" : instrumentFullName;
                accountInstrumentText.Text = acc + " / " + ins;
            });
        }

        // Pushes the observed account snapshot into the header summary line and the
        // Active Position card. PnL fields stay "-" until a real PnL source is wired.
        public void SetObservedState(ObservedAccountSnapshotDto dto)
        {
            string positionText = dto.Position.ToString();
            Brush positionBrush = ResolvePositionBrush(dto.Position);
            string qtyText = dto.AbsoluteQuantity.ToString(CultureInfo.InvariantCulture);
            string priceText = dto.LastPrice.HasValue
                ? dto.LastPrice.Value.ToString("0.#####", CultureInfo.InvariantCulture)
                : "-";
            string workingText = dto.WorkingOrdersCount.ToString(CultureInfo.InvariantCulture);

            RunOnUi(delegate
            {
                if (accountInstrumentText != null)
                {
                    string acc = string.IsNullOrEmpty(dto.AccountName) ? "?" : dto.AccountName;
                    string ins = string.IsNullOrEmpty(dto.InstrumentFullName) ? "?" : dto.InstrumentFullName;
                    accountInstrumentText.Text = acc + " / " + ins;
                }

                if (summaryPositionText != null)
                {
                    summaryPositionText.Text = positionText;
                    summaryPositionText.Foreground = positionBrush;
                }
                if (summaryQtyText != null) summaryQtyText.Text = "qty " + qtyText;
                if (summaryAvgText != null) summaryAvgText.Text = "avg " + priceText;
                if (summaryWorkingOrdersText != null) summaryWorkingOrdersText.Text = "wo " + workingText;

                if (activePosDirectionValue != null)
                {
                    activePosDirectionValue.Text = positionText;
                    activePosDirectionValue.Foreground = positionBrush;
                }
                if (activePosQtyValue != null) activePosQtyValue.Text = qtyText;
                if (activePosEntryValue != null) activePosEntryValue.Text = priceText;
                if (activePosLastFillValue != null) activePosLastFillValue.Text = priceText;
                if (activePosWorkingOrdersValue != null) activePosWorkingOrdersValue.Text = workingText;
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

        // Disabled-placeholder labels for the ENTRY card. Empty/null becomes "-".
        public void SetEntryPlanPlaceholders(string orderType, string qty, string stop, string target)
        {
            RunOnUi(delegate
            {
                if (entryTypeSelect != null && !string.IsNullOrEmpty(orderType))
                    SetComboPlaceholder(entryTypeSelect, orderType);
                if (entryQtyBox != null) entryQtyBox.Text = NullToDash(qty);
                if (entryStopBox != null) entryStopBox.Text = NullToDash(stop);
                if (entryTargetBox != null) entryTargetBox.Text = NullToDash(target);
            });
        }

        // Phase 2 preview model mutator. It renders draft values only.
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

        // Phase 2 preview model mutator. It updates disabled ENTRY fields only.
        public void SetEntryPlanDraft(EntryPlanDraft draft)
        {
            RunOnUi(delegate
            {
                EntryPlanDraft safe = draft ?? EntryPlanDraft.Default();
                if (entryTypeSelect != null) SetComboPlaceholder(entryTypeSelect, NullToDash(safe.EntryType));
                if (entryQtyBox != null) entryQtyBox.Text = QuantityToText(safe.Quantity);
                if (entrySizingSelect != null) SetComboPlaceholder(entrySizingSelect, NullToDash(safe.SizingMode));
                if (entryUnitSelect != null) SetComboPlaceholder(entryUnitSelect, NullToDash(safe.Unit));
                if (entryStopBox != null) entryStopBox.Text = NullToDash(safe.Stop);
                if (entryTargetBox != null) entryTargetBox.Text = NullToDash(safe.Target);
            });
        }

        // Phase 2 preview model mutator. It summarizes draft targets in the TAKES card
        // and mirrors the same summary in ACTIVE POSITION / Targets as a draft preview.
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

        // Phase 2 preview model mutator. It updates disabled STOP/ENTRY labels only.
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

        // Phase 2 preview model mutator. It updates the protection summary only.
        public void SetProtectionDraft(ProtectionDraft draft)
        {
            RunOnUi(delegate
            {
                string summary = BuildProtectionSummary(draft);
                if (activePosProtectionValue != null) activePosProtectionValue.Text = NullToDash(summary);
            });
        }

        // Phase 2 preview model mutator. It updates disabled RISK fields only.
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

        // Read-only PnL/protection labels for the Active Position card. Empty/null becomes "-".
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

        // Sets the disabled strategy combobox's preview label. Wiring real strategies is
        // a Phase 2 task; for now this just lets the host show "default" or a chosen name.
        public void SetStrategyName(string strategyName)
        {
            RunOnUi(delegate
            {
                if (strategySelect != null) SetComboPlaceholder(strategySelect, NullToDash(strategyName));
            });
        }

        public void SetSectionsVisibility(EssencialChartGuardPanelSections sections)
        {
            RunOnUi(delegate
            {
                ApplyVisibility(strategyCard, sections.ShowStrategy);
                ApplyVisibility(entryCard, sections.ShowEntry);
                ApplyVisibility(activePositionCard, sections.ShowActivePosition);
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
                if (connectionDot != null) connectionDot.Fill = EssencialChartGuardTheme.AccentDotIdle;
                if (modeText != null) modeText.Text = "Observer";
                if (accountInstrumentText != null) accountInstrumentText.Text = "? / ?";

                if (summaryPositionText != null)
                {
                    summaryPositionText.Text = "-";
                    summaryPositionText.Foreground = EssencialChartGuardTheme.TextSecondary;
                }
                if (summaryQtyText != null) summaryQtyText.Text = "qty -";
                if (summaryAvgText != null) summaryAvgText.Text = "avg -";
                if (summaryWorkingOrdersText != null) summaryWorkingOrdersText.Text = "wo -";

                if (strategySelect != null) SetComboPlaceholder(strategySelect, "default");
                if (entryTypeSelect != null) SetComboPlaceholder(entryTypeSelect, "Market");
                if (entryQtyBox != null) entryQtyBox.Text = "1";
                if (entrySizingSelect != null) SetComboPlaceholder(entrySizingSelect, "Fixed");
                if (entryUnitSelect != null) SetComboPlaceholder(entryUnitSelect, "Ticks");
                if (entryStopBox != null) entryStopBox.Text = "-";
                if (entryTargetBox != null) entryTargetBox.Text = "-";
                if (takesEmptyHint != null)
                {
                    takesEmptyHint.Text = "(no targets defined)";
                    takesEmptyHint.FontStyle = FontStyles.Italic;
                }
                if (stopCurrentValue != null) stopCurrentValue.Text = "-";
                if (riskModeSelect != null) SetComboPlaceholder(riskModeSelect, "Alert");

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

                if (riskDailyLimitValue != null) riskDailyLimitValue.Text = "-";
                if (riskStatusValue != null) riskStatusValue.Text = "no risk data yet";
                if (riskBlockStatusValue != null) riskBlockStatusValue.Text = "-";

                if (sessionTradesValue != null) sessionTradesValue.Text = "-";
                if (sessionPnLValue != null) sessionPnLValue.Text = "-";
                if (sessionTimeValue != null) sessionTimeValue.Text = "no session data yet";
            });
        }

        // ============================================================================
        // Layout builders
        // ============================================================================

        private FrameworkElement BuildHeader()
        {
            // Row 0: brand (left) | dot+mode+gear (right)
            // Row 1: account / instrument (monospace)
            // Row 2: position chip · qty · avg · wo
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            brandText = new TextBlock
            {
                Text = "Essencial ChartGuard",
                Foreground = EssencialChartGuardTheme.TextPrimary,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeBrand,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Essencial ChartGuard side panel — read-only preview of the final operational layout."
            };
            Grid.SetColumn(brandText, 0);
            topRow.Children.Add(brandText);

            StackPanel right = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            connectionDot = EssencialChartGuardTheme.CreateStatusDot(EssencialChartGuardTheme.AccentDotIdle);
            connectionDot.Margin = new Thickness(0, 0, 6, 0);
            connectionDot.ToolTip = "Connection / mode status. Green = sim/playback observer, amber = non-sim observer, red = error, gray = idle. Read-only.";
            right.Children.Add(connectionDot);

            modeText = new TextBlock
            {
                Text = "Observer",
                Foreground = EssencialChartGuardTheme.TextSecondary,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeSubtitle,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Operating mode. The panel is always Observer in this build."
            };
            right.Children.Add(modeText);

            settingsIconButton = BuildPlaceholderButton(
                "⚙",
                EssencialChartGuardTheme.TextSecondary,
                "Settings (preview / disabled). Show/hide sections, units, default sizing — coming in a later phase.");
            settingsIconButton.MinWidth = 28;
            settingsIconButton.Padding = new Thickness(6, 2, 6, 2);
            settingsIconButton.Margin = new Thickness(8, 0, 0, 0);
            right.Children.Add(settingsIconButton);

            Grid.SetColumn(right, 1);
            topRow.Children.Add(right);
            Grid.SetRow(topRow, 0);
            header.Children.Add(topRow);

            accountInstrumentText = new TextBlock
            {
                Text = "? / ?",
                Foreground = EssencialChartGuardTheme.TextMuted,
                FontFamily = EssencialChartGuardTheme.FontMono,
                FontSize = EssencialChartGuardTheme.FontSizeSubtitle,
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = "Account / chart instrument the panel is observing. Read-only."
            };
            Grid.SetRow(accountInstrumentText, 1);
            header.Children.Add(accountInstrumentText);

            StackPanel summary = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            summaryPositionText = new TextBlock
            {
                Text = "-",
                Foreground = EssencialChartGuardTheme.TextSecondary,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeValue,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Observed position direction (Long / Short / Flat / Unknown)."
            };
            summary.Children.Add(summaryPositionText);
            summary.Children.Add(BuildHeaderSeparator());

            summaryQtyText = BuildHeaderChip("qty -", "Observed absolute net quantity.");
            summary.Children.Add(summaryQtyText);
            summary.Children.Add(BuildHeaderSeparator());

            summaryAvgText = BuildHeaderChip("avg -", "Observed average / last fill price.");
            summary.Children.Add(summaryAvgText);
            summary.Children.Add(BuildHeaderSeparator());

            summaryWorkingOrdersText = BuildHeaderChip("wo -", "Observed working orders for this account/instrument.");
            summary.Children.Add(summaryWorkingOrdersText);

            Grid.SetRow(summary, 2);
            header.Children.Add(summary);

            return header;
        }

        private static TextBlock BuildHeaderChip(string text, string tooltip)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = EssencialChartGuardTheme.TextPrimary,
                FontFamily = EssencialChartGuardTheme.FontMono,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip
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

        // STRATEGY (disabled). Combobox + small action buttons for create/edit/duplicate/delete.
        // Wiring strategies to real config is a Phase 2 task. Until then everything is inert.
        private FrameworkElement BuildStrategyCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            card.Background = EssencialChartGuardTheme.BackgroundDisabled;
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Strategy"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            strategySelect = BuildPlaceholderCombo(
                "default",
                "Strategy / mode selector (preview / disabled). A future iteration will let you create, save and select named strategies with their own defaults.");
            Grid.SetColumn(strategySelect, 0);
            row.Children.Add(strategySelect);

            strategyAddButton = BuildIconButton("+", EssencialChartGuardTheme.AccentGold,
                "Create strategy (preview / disabled). Will let you save current entry/stop/target/sizing defaults under a name.");
            Grid.SetColumn(strategyAddButton, 1);
            row.Children.Add(strategyAddButton);

            strategyEditButton = BuildIconButton("✎", EssencialChartGuardTheme.TextSecondary,
                "Edit strategy (preview / disabled). Will open the selected strategy for editing.");
            Grid.SetColumn(strategyEditButton, 2);
            row.Children.Add(strategyEditButton);

            strategyDuplicateButton = BuildIconButton("❏", EssencialChartGuardTheme.TextSecondary,
                "Duplicate strategy (preview / disabled). Will copy the selected strategy under a new name.");
            Grid.SetColumn(strategyDuplicateButton, 3);
            row.Children.Add(strategyDuplicateButton);

            strategyDeleteButton = BuildIconButton("✕", EssencialChartGuardTheme.AccentRed,
                "Delete strategy (preview / disabled). Will remove the selected strategy after confirmation.");
            Grid.SetColumn(strategyDeleteButton, 4);
            row.Children.Add(strategyDeleteButton);

            content.Children.Add(row);
            content.Children.Add(BuildPlaceholderFootnote(
                "Read-only preview. Strategy persistence is not wired in this build."));

            card.Child = content;
            strategyCard = card;
            return card;
        }

        // ENTRY (disabled). Type / Qty / Sizing / Unit / Stop / Target + Buy/Sell/Panic.
        private FrameworkElement BuildEntryCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            card.Background = EssencialChartGuardTheme.BackgroundDisabled;
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Entry"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            // Row 1: Type | Qty
            Grid r1 = TwoColumnRow();
            entryTypeSelect = BuildPlaceholderCombo("Market",
                "Entry type (preview / disabled). Will choose between Market / Limit / Stop Market / Stop Limit.");
            r1.Children.Add(WrapField("Type", entryTypeSelect, 0));
            entryQtyBox = BuildPlaceholderTextBox("1",
                "Quantity (preview / disabled). The number of contracts the entry will use.");
            r1.Children.Add(WrapField("Qty", entryQtyBox, 1));
            content.Children.Add(r1);

            // Row 2: Sizing | Unit
            Grid r2 = TwoColumnRow();
            r2.Margin = new Thickness(0, 4, 0, 0);
            entrySizingSelect = BuildPlaceholderCombo("Fixed",
                "Sizing mode (preview / disabled). Fixed contracts, risk-based, or other future modes.");
            r2.Children.Add(WrapField("Sizing", entrySizingSelect, 0));
            entryUnitSelect = BuildPlaceholderCombo("Ticks",
                "Stop/target unit (preview / disabled). Choose ticks, points or price.");
            r2.Children.Add(WrapField("Unit", entryUnitSelect, 1));
            content.Children.Add(r2);

            // Row 3: Stop | Target
            Grid r3 = TwoColumnRow();
            r3.Margin = new Thickness(0, 4, 0, 0);
            entryStopBox = BuildPlaceholderTextBox("-",
                "Stop value (preview / disabled). Required by default for protected entries; configurable per strategy in the future.");
            r3.Children.Add(WrapField("Stop", entryStopBox, 0));
            entryTargetBox = BuildPlaceholderTextBox("-",
                "Target value (preview / disabled). Optional; multiple targets will be supported via the Takes section.");
            r3.Children.Add(WrapField("Target", entryTargetBox, 1));
            content.Children.Add(r3);

            // Big BUY / SELL row
            Grid bs = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            bs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
            bs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            entryBuyButton = BuildBigButton("BUY", EssencialChartGuardTheme.AccentGreen,
                "Buy (preview / disabled). Will create a ProtectedEntryCommand and route it through TradeCommandService once Phase 4 is enabled.");
            Grid.SetColumn(entryBuyButton, 0);
            bs.Children.Add(entryBuyButton);

            entrySellButton = BuildBigButton("SELL", EssencialChartGuardTheme.AccentRed,
                "Sell (preview / disabled). Will create a ProtectedEntryCommand for short and route it through TradeCommandService once Phase 4 is enabled.");
            Grid.SetColumn(entrySellButton, 2);
            bs.Children.Add(entrySellButton);
            content.Children.Add(bs);

            // Panic row (full width)
            entryPanicButton = BuildBigButton("PANIC", EssencialChartGuardTheme.AccentRed,
                "Panic — flatten + cancel (preview / disabled). Will cancel live orders and flatten the open position for the selected account/instrument once the Flatten command route is enabled.");
            entryPanicButton.Margin = new Thickness(0, 4, 0, 0);
            content.Children.Add(entryPanicButton);

            content.Children.Add(BuildPlaceholderFootnote(
                "Read-only preview. Entry / panic routes are not wired in this build."));

            card.Child = content;
            entryCard = card;
            return card;
        }

        // ACTIVE POSITION (read-only). Renders observed direction/qty/avg today, and reserves
        // labelled rows for last fill, PnL ticks/points/percent/cash, working orders, stop,
        // targets and protection state. PnL/protection rows stay "-" until a real source is
        // wired through the host.
        private FrameworkElement BuildActivePositionCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Active position"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            content.Children.Add(BuildLabelValueRow("Direction", out activePosDirectionValue,
                "Observed direction (Long / Short / Flat / Unknown)."));
            content.Children.Add(BuildLabelValueRow("Qty", out activePosQtyValue,
                "Observed absolute net quantity."));
            content.Children.Add(BuildLabelValueRow("Entry / Avg", out activePosEntryValue,
                "Observed average / entry price."));
            content.Children.Add(BuildLabelValueRow("Last fill", out activePosLastFillValue,
                "Last observed execution price for this account/instrument."));
            content.Children.Add(BuildLabelValueRow("PnL ticks", out activePosPnLTicksValue,
                "Open PnL in ticks (preview). Will be wired to a real PnL source in a later phase."));
            content.Children.Add(BuildLabelValueRow("PnL points", out activePosPnLPointsValue,
                "Open PnL in points (preview). Will be wired to a real PnL source in a later phase."));
            content.Children.Add(BuildLabelValueRow("PnL %", out activePosPnLPercentValue,
                "Open PnL in percent (preview). Will be wired to a real PnL source in a later phase."));
            content.Children.Add(BuildLabelValueRow("PnL $", out activePosPnLCashValue,
                "Open PnL in account currency (preview). Will be wired to a real PnL source in a later phase."));
            content.Children.Add(BuildLabelValueRow("Working orders", out activePosWorkingOrdersValue,
                "Number of working orders for this account/instrument (observed)."));
            content.Children.Add(BuildLabelValueRow("Stop", out activePosStopValue,
                "Active stop summary (preview). Will reflect the protective stop once the Protection route is wired."));
            content.Children.Add(BuildLabelValueRow("Targets", out activePosTargetsValue,
                "Active targets summary (preview). Will reflect take-profit orders once the Takes route is wired."));
            content.Children.Add(BuildLabelValueRow("Protection", out activePosProtectionValue,
                "Protection state summary (preview). Will reflect Breakeven / Lock R / Trail state once Phase 5 is enabled."));

            card.Child = content;
            activePositionCard = card;
            return card;
        }

        // TAKES (disabled). List placeholder + add/edit/remove buttons.
        private FrameworkElement BuildTakesCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            card.Background = EssencialChartGuardTheme.BackgroundDisabled;
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Takes"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            takesEmptyHint = new TextBlock
            {
                Text = "(no targets defined)",
                Foreground = EssencialChartGuardTheme.TextMuted,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "Future list of take-profit targets. Read-only in this build."
            };
            content.Children.Add(takesEmptyHint);

            Grid btnRow = ThreeColumnRow();
            takesAddButton = BuildPlaceholderButton("+ Add target", EssencialChartGuardTheme.AccentGold,
                "Add target (preview / disabled). Will append a take-profit target to the active plan via the command route.");
            Grid.SetColumn(takesAddButton, 0);
            btnRow.Children.Add(takesAddButton);

            takesEditButton = BuildPlaceholderButton("Edit", EssencialChartGuardTheme.TextSecondary,
                "Edit target (preview / disabled). Will edit the selected target via the command route.");
            Grid.SetColumn(takesEditButton, 2);
            btnRow.Children.Add(takesEditButton);

            takesRemoveButton = BuildPlaceholderButton("Remove", EssencialChartGuardTheme.AccentRed,
                "Remove target (preview / disabled). Will remove the selected target via the command route.");
            Grid.SetColumn(takesRemoveButton, 4);
            btnRow.Children.Add(takesRemoveButton);

            content.Children.Add(btnRow);
            content.Children.Add(BuildPlaceholderFootnote(
                "Read-only preview. Takes route is not wired in this build."));

            card.Child = content;
            takesCard = card;
            return card;
        }

        // STOP (disabled). Current stop placeholder + edit button.
        private FrameworkElement BuildStopCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            card.Background = EssencialChartGuardTheme.BackgroundDisabled;
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Stop"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            content.Children.Add(BuildLabelValueRow("Current", out stopCurrentValue,
                "Current protective stop (preview). Will display the active stop price/distance once the Protection route is wired."));

            stopEditButton = BuildPlaceholderButton("Edit stop", EssencialChartGuardTheme.AccentBlue,
                "Edit stop (preview / disabled). Will move the protective stop via ProtectionService once Phase 5 is enabled.");
            stopEditButton.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(stopEditButton);

            content.Children.Add(BuildPlaceholderFootnote(
                "Read-only preview. Stop edit route is not wired in this build."));

            card.Child = content;
            stopCard = card;
            return card;
        }

        // PROTECTION (disabled). BE / Lock 1R / Lock 2R / Lock 3R / Trail.
        private FrameworkElement BuildProtectionCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            card.Background = EssencialChartGuardTheme.BackgroundDisabled;
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Protection"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            Grid row1 = BuildButtonRow(
                out protectionBeButton, "BE", EssencialChartGuardTheme.AccentBlue,
                "Breakeven (preview / disabled). Will move the stop to entry price via ProtectionService once Phase 5 is enabled.",
                out protectionLock1RButton, "Lock 1R", EssencialChartGuardTheme.AccentBlue,
                "Lock 1R (preview / disabled). Will move the stop to +/- 1R based on initial risk via ProtectionService.");
            content.Children.Add(row1);

            Grid row2 = BuildButtonRow(
                out protectionLock2RButton, "Lock 2R", EssencialChartGuardTheme.AccentBlue,
                "Lock 2R (preview / disabled). Will move the stop to +/- 2R based on initial risk via ProtectionService.",
                out protectionLock3RButton, "Lock 3R", EssencialChartGuardTheme.AccentBlue,
                "Lock 3R (preview / disabled). Will move the stop to +/- 3R based on initial risk via ProtectionService.");
            row2.Margin = new Thickness(0, 4, 0, 0);
            content.Children.Add(row2);

            protectionTrailButton = BuildPlaceholderButton("Trail", EssencialChartGuardTheme.AccentBlue,
                "Trailing stop (preview / disabled). Will manage a trailing stop via ProtectionService once Phase 5 is enabled.");
            protectionTrailButton.Margin = new Thickness(0, 4, 0, 0);
            content.Children.Add(protectionTrailButton);

            content.Children.Add(BuildPlaceholderFootnote(
                "Read-only preview. Protection route is not wired in this build."));

            card.Child = content;
            protectionCard = card;
            return card;
        }

        // RISK (read-only labels + disabled mode select for Alert/Block/Off).
        private FrameworkElement BuildRiskCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Risk"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            content.Children.Add(BuildLabelValueRow("Daily limit", out riskDailyLimitValue,
                "Daily loss limit (preview). Will reflect the configured maximum daily loss for the account."));
            content.Children.Add(BuildLabelValueRow("Status", out riskStatusValue,
                "Risk status (preview). Will indicate whether new entries are allowed, alerted, or blocked."));
            content.Children.Add(BuildLabelValueRow("Block", out riskBlockStatusValue,
                "Block reason (preview). Will explain why new entries are blocked when the risk guard fires."));

            // Mode select: Alert / Block / Off — disabled placeholder.
            Grid row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = EssencialChartGuardTheme.CreateLabel("Mode");
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            riskModeSelect = BuildPlaceholderCombo("Alert",
                "Risk mode (preview / disabled). Will let you choose between Alert (warn only), Block (refuse new entries), or Off.");
            riskModeSelect.MinWidth = 110;
            Grid.SetColumn(riskModeSelect, 1);
            row.Children.Add(riskModeSelect);
            content.Children.Add(row);

            content.Children.Add(BuildPlaceholderFootnote(
                "Read-only preview. Risk mode is not wired in this build."));

            card.Child = content;
            riskCard = card;
            return card;
        }

        private FrameworkElement BuildSessionCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Session"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            content.Children.Add(BuildLabelValueRow("Trades", out sessionTradesValue,
                "Trades counted in the current session (preview)."));
            content.Children.Add(BuildLabelValueRow("PnL", out sessionPnLValue,
                "Realized + open PnL of the current session (preview)."));
            content.Children.Add(BuildLabelValueRow("Time", out sessionTimeValue,
                "Session time / state (preview)."));

            card.Child = content;
            sessionCard = card;
            return card;
        }

        private FrameworkElement BuildObservationCard()
        {
            Border card = EssencialChartGuardTheme.CreateCard();
            StackPanel content = new StackPanel { Orientation = Orientation.Vertical };

            content.Children.Add(EssencialChartGuardTheme.CreateSectionTitle("Observation"));
            content.Children.Add(EssencialChartGuardTheme.CreateSectionUnderline());

            content.Children.Add(BuildDotRow("Snapshot", out snapshotDot, out snapshotText,
                "Initial position snapshot status. Read by the host from Account.Positions before subscribing to events."));
            content.Children.Add(BuildDotRow("Event bridge", out bridgeDot, out bridgeText,
                "Event bridge subscription status. Read-only stream of order/execution updates from NinjaTrader."));

            card.Child = content;
            observationCard = card;
            return card;
        }

        // ============================================================================
        // Small helpers
        // ============================================================================

        private static FrameworkElement BuildLabelValueRow(string labelText, out TextBlock value, string tooltip)
        {
            Grid row = new Grid { Margin = EssencialChartGuardTheme.RowSpacing };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = EssencialChartGuardTheme.CreateLabel(labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.ToolTip = tooltip;
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            value = EssencialChartGuardTheme.CreateValue("-");
            value.VerticalAlignment = VerticalAlignment.Center;
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.ToolTip = tooltip;
            Grid.SetColumn(value, 1);
            row.Children.Add(value);

            return row;
        }

        private static FrameworkElement BuildDotRow(
            string labelText,
            out System.Windows.Shapes.Ellipse dot,
            out TextBlock value,
            string tooltip)
        {
            Grid row = new Grid { Margin = EssencialChartGuardTheme.RowSpacing };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = EssencialChartGuardTheme.CreateLabel(labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.ToolTip = tooltip;
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
            dot.ToolTip = tooltip;
            right.Children.Add(dot);

            value = new TextBlock
            {
                Text = "-",
                Foreground = EssencialChartGuardTheme.TextPrimary,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = tooltip
            };
            right.Children.Add(value);

            Grid.SetColumn(right, 1);
            row.Children.Add(right);

            return row;
        }

        // Two-column row: cells go at columns 0 and 2 (with 6px spacer at column 1).
        private static Grid TwoColumnRow()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }

        // Three-column row: cells go at columns 0, 2 and 4 with 6px spacers between them.
        private static Grid ThreeColumnRow()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }

        // Wraps a labelled field into a 2-row stack and places it at the requested cell of
        // a TwoColumnRow grid (cell 0 -> column 0, cell 1 -> column 2).
        private static FrameworkElement WrapField(string labelText, FrameworkElement editor, int cell)
        {
            StackPanel s = new StackPanel { Orientation = Orientation.Vertical };
            s.Children.Add(EssencialChartGuardTheme.CreateLabel(labelText));
            s.Children.Add(editor);
            Grid.SetColumn(s, cell == 0 ? 0 : 2);
            return s;
        }

        // Builds a horizontally split row of two big placeholder buttons with tooltips.
        private static Grid BuildButtonRow(
            out Button leftBtn, string leftText, Brush leftAccent, string leftTip,
            out Button rightBtn, string rightText, Brush rightAccent, string rightTip)
        {
            Grid g = TwoColumnRow();
            leftBtn = BuildPlaceholderButton(leftText, leftAccent, leftTip);
            Grid.SetColumn(leftBtn, 0);
            g.Children.Add(leftBtn);

            rightBtn = BuildPlaceholderButton(rightText, rightAccent, rightTip);
            Grid.SetColumn(rightBtn, 2);
            g.Children.Add(rightBtn);
            return g;
        }

        // Disabled placeholder button: visible identity, no command wiring. We do NOT
        // attach Click handlers and we keep IsEnabled=false. Tag carries a marker string
        // in case future code wants to confirm the button has not been wired.
        private static Button BuildPlaceholderButton(string text, Brush accent, string tooltip)
        {
            return new Button
            {
                Content = text ?? string.Empty,
                Foreground = accent ?? EssencialChartGuardTheme.TextSecondary,
                Background = EssencialChartGuardTheme.BackgroundInput,
                BorderBrush = EssencialChartGuardTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                FontWeight = FontWeights.SemiBold,
                IsEnabled = false,
                Focusable = false,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.No,
                ToolTip = tooltip,
                Tag = "ecg-placeholder-disabled"
            };
        }

        // Big BUY/SELL/PANIC button. Same disabled contract as BuildPlaceholderButton.
        private static Button BuildBigButton(string text, Brush accent, string tooltip)
        {
            Button b = BuildPlaceholderButton(text, accent, tooltip);
            b.Padding = new Thickness(10, 10, 10, 10);
            b.FontSize = EssencialChartGuardTheme.FontSizeValueLarge;
            return b;
        }

        // Small icon button used for strategy/take/etc. action affordances.
        private static Button BuildIconButton(string text, Brush accent, string tooltip)
        {
            Button b = BuildPlaceholderButton(text, accent, tooltip);
            b.MinWidth = 28;
            b.Padding = new Thickness(6, 4, 6, 4);
            b.Margin = new Thickness(4, 0, 0, 0);
            b.FontSize = EssencialChartGuardTheme.FontSizeLabel;
            return b;
        }

        // Disabled placeholder ComboBox. We do NOT attach SelectionChanged. We add a single
        // string item so the visible label matches a real ComboBox style, and disable it.
        private static ComboBox BuildPlaceholderCombo(string text, string tooltip)
        {
            ComboBox c = new ComboBox
            {
                IsEnabled = false,
                Focusable = false,
                IsTabStop = false,
                Foreground = EssencialChartGuardTheme.TextPrimary,
                Background = EssencialChartGuardTheme.BackgroundInput,
                BorderBrush = EssencialChartGuardTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = System.Windows.Input.Cursors.No,
                ToolTip = tooltip,
                Tag = "ecg-placeholder-disabled"
            };
            SetComboPlaceholder(c, text);
            return c;
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

        // Disabled placeholder TextBox. We do NOT attach TextChanged.
        private static TextBox BuildPlaceholderTextBox(string text, string tooltip)
        {
            return new TextBox
            {
                Text = text ?? string.Empty,
                IsEnabled = false,
                Focusable = false,
                IsTabStop = false,
                IsReadOnly = true,
                Foreground = EssencialChartGuardTheme.TextPrimary,
                Background = EssencialChartGuardTheme.BackgroundInput,
                BorderBrush = EssencialChartGuardTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                FontFamily = EssencialChartGuardTheme.FontMono,
                FontSize = EssencialChartGuardTheme.FontSizeLabel,
                Padding = new Thickness(8, 4, 8, 4),
                ToolTip = tooltip,
                Tag = "ecg-placeholder-disabled"
            };
        }

        private static TextBlock BuildPlaceholderFootnote(string text)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = EssencialChartGuardTheme.TextMuted,
                FontFamily = EssencialChartGuardTheme.FontUi,
                FontSize = EssencialChartGuardTheme.FontSizeFootnote,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                IsEnabled = false
            };
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

        private static string QuantityToText(int quantity)
        {
            return quantity <= 0 ? "-" : quantity.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildTargetsSummary(TakeTargetDraft[] targets)
        {
            if (targets == null || targets.Length == 0) return string.Empty;

            string summary = string.Empty;
            int visibleCount = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                TakeTargetDraft target = targets[i];
                if (target == null) continue;

                string item = FormatTarget(target, visibleCount + 1);
                if (string.IsNullOrEmpty(item)) continue;

                if (summary.Length > 0) summary += " | ";
                summary += item;
                visibleCount++;
            }

            return summary;
        }

        private static string FormatTarget(TakeTargetDraft target, int index)
        {
            string label = string.IsNullOrEmpty(target.Label)
                ? "T" + index.ToString(CultureInfo.InvariantCulture)
                : target.Label;
            string qty = target.Quantity > 0
                ? " x" + target.Quantity.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            string value = string.IsNullOrEmpty(target.Value) ? "-" : target.Value;
            string unit = string.IsNullOrEmpty(target.Unit) ? string.Empty : " " + target.Unit;
            return label + qty + " @ " + value + unit;
        }

        private static string BuildProtectionSummary(ProtectionDraft draft)
        {
            if (draft == null) return string.Empty;
            if (!string.IsNullOrEmpty(draft.Summary)) return draft.Summary;

            string summary = string.Empty;
            AppendProtection(ref summary, draft.BreakevenEnabled, "BE");
            AppendProtection(ref summary, draft.Lock1REnabled, "Lock 1R");
            AppendProtection(ref summary, draft.Lock2REnabled, "Lock 2R");
            AppendProtection(ref summary, draft.Lock3REnabled, "Lock 3R");
            AppendProtection(ref summary, draft.TrailEnabled, "Trail");
            return summary;
        }

        private static void AppendProtection(ref string summary, bool enabled, string label)
        {
            if (!enabled) return;
            if (summary.Length > 0) summary += " | ";
            summary += label;
        }

        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (Dispatcher == null || Dispatcher.CheckAccess())
            {
                try { action(); } catch { /* never let UI updates surface as crashes */ }
                return;
            }
            try
            {
                Dispatcher.BeginInvoke(action);
            }
            catch
            {
                // Dispatcher may already be shutting down during chart teardown -- ignore.
            }
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
                case ObservedPosition.Long: return EssencialChartGuardTheme.AccentGreen;
                case ObservedPosition.Short: return EssencialChartGuardTheme.AccentRed;
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

    // Lightweight visibility config the host can pass to SetSectionsVisibility. No
    // persistence in this build. Use All() for "show every section".
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

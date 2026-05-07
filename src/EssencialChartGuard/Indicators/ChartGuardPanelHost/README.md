# Essencial ChartGuard - Panel Host

Read-only NinjaScript indicator that hosts the first **Essencial ChartGuard** side panel inside a NinjaTrader chart.

In the Indicators dialog it appears as **"Essencial ChartGuard - Panel Host"** (class `ChartGuardPanelHost`, namespace `NinjaTrader.NinjaScript.Indicators.EssencialChartGuard`).

## What this host is

- A NinjaScript indicator that, when added to a chart:
  1. Builds an `OutputTabSafeCoreLogger`, an `ObservedAccountState`, and an `OrderEventRouter`.
  2. Resolves the configured `Account` (`<Auto>` or manual) the same way the **Event Bridge Probe** does.
  3. Reads an initial position snapshot via `NinjaTraderPositionSnapshotReader` and seeds `ObservedAccountState`.
  4. Subscribes a `NinjaTraderAccountEventBridge` to the resolved account (filtered by the chart instrument when configured).
  5. Injects `EssencialChartGuardPanel` into a new right-side column of the chart's host `Grid` with a `GridSplitter` for horizontal resize.
  6. Refreshes the panel from `ObservedAccountState` whenever its summary changes.
- All UI mutations run through the `ChartControl` dispatcher.
- Lifecycle is logged with the `[EssencialUI]` and `[EssencialOrder]` prefixes.

## What this host is **not**

- It does **not** send, cancel, or modify any order.
- It does **not** flatten any position.
- It does **not** create `TradeCommandService`, `NinjaTraderOrderSubmitter`, or `NinjaTraderAccountAdapter`.
- It does **not** call `EnableForControlledTest(...)`. The submit-side bridge stays disabled by default.
- It does **not** install hotkeys, chart-click handlers, or context-menu overrides. `OnBarUpdate` is a no-op.
- It does **not** block native NinjaTrader Chart Trader, drawing tools, or any built-in interaction.

If you ever see this host send/cancel/modify any order, that is a bug — stop the session and report it.

## Dependencies

The host depends on these existing files (already present in the project under `AddOns/`):

- `SafeCore/Services/OrderEventRouter.cs`, `ISafeCoreLogger.cs` (and the rest of `SafeCore/`).
- `SafeCore/State/ObservedAccountState.cs`, `ObservedAccountSnapshotDto.cs`, `ObservedPositionSnapshot.cs`.
- `NinjaTraderBridge/INinjaTraderEventSource.cs`, `NinjaTraderAccountEventBridge.cs`, `OutputTabSafeCoreLogger.cs`, `NinjaTraderPositionSnapshotReader.cs`.
- `Panel/EssencialChartGuardTheme.cs`, `Panel/EssencialChartGuardPanel.cs`.

It does **not** depend on the historical CunhaTrader / CunhaScalper / Gold panels: no class, namespace, file, or visible string from those is reused. The visual direction is inspired only by `docs/panel-visual-audit-cunha.md`.

## How to compile in the NinjaScript Editor

1. Open NinjaTrader 8.
2. Open `New → NinjaScript Editor`.
3. Make sure the SafeCore + bridge + panel files have been copied into the user folder under `bin/Custom/AddOns/EssencialChartGuard/...` and that this `ChartGuardPanelHost.cs` lives under `bin/Custom/Indicators/EssencialChartGuard/`.
4. Press `F5` (Compile). All ChartGuard files compile together. There must be **0 errors**.
5. The host now appears in the Indicators dialog as **`Essencial ChartGuard - Panel Host`**.

If compilation fails, the host will not be available in the Indicators dialog — fix the underlying SafeCore/bridge/panel errors first, then recompile.

## How to add the host to a chart

1. Start a Replay or Sim/Playback connection on the account you want to observe.
2. Open the chart for the instrument you want to observe.
3. Open the Output Window (View → Output Window) and select **Tab 1**. Filter by `[Essencial` to keep only ChartGuard log lines visible.
4. Right-click the chart → `Indicators…`.
5. From the indicator list select **`Essencial ChartGuard - Panel Host`** and click `New`.
6. Set parameters in the right panel:
   - **Account**: pick `<Auto>` (default) or a specific account (`Name` / `DisplayName`).
   - **Filter events by chart instrument**: leave **true** (default) to scope the panel to the chart's instrument.
7. Click `OK`.

You should see, on the right side of the chart, a vertical Essencial ChartGuard panel with the **Phase 1/2 — Operational Read-Only Layout + Draft Preview Model** (`docs/chartguard-product-map.md` / `docs/plano-integrado-chartguard-pt.md`):

- a brand line `Essencial ChartGuard`, a status dot with mode `Observer / Playback` (or `/ Sim`, `/ non-sim`), and a small disabled gear (`⚙`) for the future Settings UI,
- an `<account> / <instrument>` line,
- a one-line summary: `<Position> · qty <n> · avg <price> · wo <n>`,
- a `STRATEGY` card with a strategy combobox and `+` / `✎` / `❏` / `✕` icon buttons,
- an `ENTRY` card with `Type` / `Qty` / `Sizing` / `Unit` / `Stop` / `Target` fields and big `BUY` / `SELL` / `PANIC` buttons,
- an `ACTIVE POSITION` card with `Direction` / `Qty` / `Entry-Avg` / `Last fill` / `PnL ticks` / `PnL points` / `PnL %` / `PnL $` / `Working orders` / `Stop` / `Targets` / `Protection` rows,
- a `TAKES` card with `+ Add target` / `Edit` / `Remove` buttons,
- a `STOP` card with `Current` and `Edit stop`,
- a `PROTECTION` card with `BE` / `Lock 1R` / `Lock 2R` / `Lock 3R` / `Trail` buttons,
- a `RISK` card with `Daily limit` / `Status` / `Block` rows and a `Mode` combobox (`Alert` / `Block` / `Off`),
- a `SESSION` card with `Trades` / `PnL` / `Time` rows,
- an `OBSERVATION` card with `Snapshot` and `Event bridge` status dots.

Every actionable control above is **disabled by contract** and has no `Click`, `SelectionChanged`, `TextChanged`, `MouseDown`, `MouseUp`, `PreviewMouse*`, `ContextMenu`, or `KeyBinding` handler attached. Each control carries a `ToolTip` explaining its future role and the `(preview / disabled)` qualifier. The panel renders observed state and disabled previews — it does not run any command.

Phase 2 adds pure draft models under `AddOns/Panel/Models/` and safe panel mutators (`SetStrategyDraft`, `SetEntryPlanDraft`, `SetTakeTargetsDraft`, `SetStopDraft`, `SetProtectionDraft`, `SetRiskModeDraft`). These methods only update disabled controls or read-only labels. They do not enable editing, persist settings, create commands, or interact with an account.

A vertical resize grip on the panel's left edge lets you resize the panel horizontally (initial width ~370px, minimum ~280px, max ~640px).

### Removing the host

When you are done, remove the host:

1. Right-click the chart → `Indicators…`.
2. Select **`Essencial ChartGuard - Panel Host`** in the right panel.
3. Click `Remove`, then `OK`.

This triggers `State.Terminated`. The host:

- stops the refresh timer,
- calls `Stop()` and `Dispose()` on the event bridge,
- removes the panel and the resize grip from the chart `Grid`,
- removes the two columns it added (by reference, never by index),
- clears its references.

After this, no further `EventBridge` or `PanelHost` lines should appear for new manual orders.

## Logs to expect

```
[EssencialUI] PanelHost account mode=<<Auto>|manual> requested=<<Auto>|<account>>
[EssencialUI] PanelHost attaching account=... chartInstrument=... filterByChartInstrument=...
[EssencialUI] PanelHost selected account=<Name (DisplayName)> mode=... sim=...
[EssencialUI] PanelHost observing non-sim account read-only account=...   (only when sim=false)
[EssencialUI] PanelHost panel injected initialWidth=370px minWidth=280px
[EssencialOrder] PositionSnapshot ... account=... instrument=... ...
[EssencialOrder] ObservedState account=... instrument=... position=... qty=... lastPrice=... workingOrders=...
[EssencialOrder] EventBridge subscribed account=... instrumentFilter=...
[EssencialUI] PanelHost attached account=... instrumentFilter=...
...
[EssencialUI] PanelHost panel removed
[EssencialOrder] EventBridge unsubscribed account=...
[EssencialUI] PanelHost detached account=...
```

What you should **never** see while the host is on the chart:

```
[EssencialOrder] Bridge submitted ...
[EssencialCommand] DryRun SubmitProtectedEntry ...
```

Either of those would indicate that something is sending orders — which the host is explicitly designed not to do.

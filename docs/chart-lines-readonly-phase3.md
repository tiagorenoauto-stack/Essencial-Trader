# Phase 3 — Read-Only Chart Lines (Specification)

> **Status:** Phase 3.1 (the first implementation step) shipped in
> commit `e0d2cdb` with subsequent build/visibility fixes (`d0fa1ca`,
> `a114cec`, `964727c`). Phase 3.2 (visual polish: native-style price
> markers, multi-line labels with side/quantity badge, user
> customization) is specified in
> `docs/chart-lines-readonly-phase3.2-visual.md` and not yet
> implemented.

This document specifies how Essencial ChartGuard will eventually render
informative lines on the chart for position, entry/avg, last fill, draft
stop, and draft targets. **The lines are read-only.** They never accept
mouse interaction, never modify orders, and never create commands.

For the broader plan, see:

- `docs/plano-integrado-chartguard-pt.md` — **Fase 3 — Linhas Read-Only
  No Gráfico** (this document is its English technical companion).
- `docs/chartguard-product-map.md` — *Phase 2 — Chart Lines Read-Only*
  (older numbering; see the Numbering note at the end of this file).
- `docs/chart-lines-readonly-phase3.2-visual.md` — visual polish
  specification (Phase 3.2): label legibility, side/quantity badge,
  native price marker, user customization.
- `docs/safe-core-contract.md` — Safe Core rules the implementation must
  obey.
- `docs/panel-command-map.md` — UI ↔ command mapping.
- `docs/project-working-rules.md` — operational standard for every
  task touching the chart line code.

## 1. Goal of Phase 3

The first iteration of chart lines must be **purely read-only**:

- Draw a small, well-labelled set of lines that mirror state already
  visible on the side panel (Phase 1/2/2.3).
- Lines exist on the chart so the operator can read price levels at a
  glance without looking at the panel for every observation.
- Lines must not introduce any new way to act on orders. Specifically:
  no drag, no click, no double-click, no context menu, no hotkey, no
  drag-to-modify, no chart-cursor capture.

When this phase is enabled, the chart and the panel must remain mutually
consistent: the chart shows the same numbers the panel shows, sourced
from the same observed snapshot or the same draft.

## 2. Lines supported in this phase

The first read-only line set is intentionally small. Each line has a
single source of truth and a single rendering rule.

| Line | Source | Visual hint | Drawn when |
| --- | --- | --- | --- |
| **Entry / Avg** | `ObservedAccountSnapshotDto.LastPrice` while `AbsoluteQuantity > 0` and `Position` is `Long` or `Short` | thin solid horizontal line at the average price; label `Entry/Avg <price>` | observed position is open |
| **Last fill** | `ObservedAccountSnapshotDto.LastPrice` | thin dashed horizontal line at the last execution price; label `Last fill <price>` | `LastPrice` has a value (regardless of position) |
| **Draft stop** | `StopDraft.Current` (when numerically resolvable to a price for the chart instrument) | dashed line in the panel's stop accent color; label `Draft stop <value> <unit>` | `EnableDraftPreview=true` and `StopDraft.Current` resolves |
| **Draft target T1** | first item of `TakeTargetDraft[]` | dashed line in the panel's target accent color; label `Draft T1 x<qty> @ <value> <unit>` | `EnableDraftPreview=true` and at least one `TakeTargetDraft` exists |
| **Draft target T2** | second item of `TakeTargetDraft[]` | same style as T1; label `Draft T2 x<qty> @ <value> <unit>` | `EnableDraftPreview=true` and a second `TakeTargetDraft` exists |
| *(optional, deferred)* **Active stop / active targets** | a future read-only protection state source — e.g. an `ObservedProtectionSnapshotDto` reading from `Account.Orders` of the active stop/target — once that source exists | distinct line style from drafts (e.g. solid vs. dashed) so it is visually obvious whether the line came from a draft or from real working orders | when the future source returns a value |

The active-stop / active-target lines are **not** part of the first
implementation step. They are listed so the renderer's API does not have
to change later when their source ships.

PnL, drawdown, daily-limit, and session lines are **out of scope** for
Phase 3.

## 3. Data sources

The renderer is fed by neutral, value-typed inputs. It must **not** read
NinjaTrader account, order, or position objects directly.

Allowed inputs:

- `ObservedAccountSnapshotDto` (Safe Core) — already used by the panel;
  carries `Position`, `AbsoluteQuantity`, `LastPrice`, `WorkingOrdersCount`.
  This stays the only source for "Entry/Avg" and "Last fill" lines.
- `StrategyDraft` and its components from `AddOns/Panel/Models/`
  (`EntryPlanDraft`, `StopDraft`, `TakeTargetDraft[]`, `ProtectionDraft`,
  `RiskModeDraft`) — already used by the panel for the draft preview;
  the same draft is the only source for the "Draft stop" and "Draft T1/T2"
  lines.
- An optional, future `ObservedProtectionSnapshotDto` (not yet defined) —
  the only allowed source for the deferred active-stop / active-target
  lines. It must carry primitive prices and labels only, like the current
  `ObservedAccountSnapshotDto`.

Forbidden inputs in the renderer:

- `Account.*`, `Order.*`, `Position.*`, `Instrument.MarketData.*`, or any
  other `NinjaTrader.Cbi` / `NinjaTrader.Data` trading type.
- `TradeCommandService`, `IOrderSubmitter`, `NinjaTraderAccountAdapter`.
- Direct event subscriptions to `OrderUpdate` / `ExecutionUpdate`. The
  renderer reads **only** the structured snapshot the host hands it, never
  raw events.

## 4. Suggested architecture

The split mirrors the existing panel design: a pure data type + a thin
renderer + a host-driven feed.

```
ChartGuardPanelHost            (Indicator, NT-aware)
   |
   |  builds from observed/draft inputs
   v
ChartGuardLineState            (pure value type, AddOns/Panel/State or
                                AddOns/Panel/Lines/State, no NT.Cbi)
   |
   |  passed to
   v
ChartGuardReadOnlyLineRenderer (WPF/Chart-aware, lives outside SafeCore)
   |
   |  draws / updates / removes lines on the chart
   v
NinjaTrader chart canvas
```

### 4.1 `ChartGuardLineState`

Pure value type, no NinjaTrader trading reference. Concrete shape (proposed):

```csharp
public struct ChartGuardLineState
{
    public bool   ShowEntryAvg;     public double EntryAvgPrice;
    public bool   ShowLastFill;     public double LastFillPrice;

    public bool   ShowDraftStop;    public double DraftStopPrice;
    public string DraftStopLabel;   // "40 Ticks", "100.50", etc.

    public bool   ShowDraftT1;      public double DraftT1Price;
    public string DraftT1Label;     // "T1 x1 @ 40 Ticks"

    public bool   ShowDraftT2;      public double DraftT2Price;
    public string DraftT2Label;     // "T2 x1 @ 80 Ticks"

    // Reserved for the deferred active stop / active target lines.
    public bool   ShowActiveStop;   public double ActiveStopPrice;
    public bool   ShowActiveT1;     public double ActiveT1Price;
    public bool   ShowActiveT2;     public double ActiveT2Price;
}
```

Rules:

- All fields are primitives or strings.
- A `Show*` flag of `false` means the corresponding line is hidden, even
  if a price is set. The renderer must respect that flag without trying
  to be clever.
- Prices that the host cannot compute (e.g. ticks-only draft stop without
  a reference price) leave `Show* = false` and `*Price = 0`. The renderer
  must not synthesize a price from any other field.

### 4.2 `ChartGuardReadOnlyLineRenderer`

Lives next to `EssencialChartGuardTheme` / `EssencialChartGuardPanel`
(suggested folder: `AddOns/Panel/Lines/`). It is the only place that
talks to the chart canvas.

Public surface (proposed):

```csharp
void Attach(ChartControl chartControl);
void ApplyState(ChartGuardLineState state);   // idempotent: missing => remove
void Detach();                                // removes every line it added
```

Internal rules:

- The renderer **must not** subscribe to mouse, keyboard, drag, drop,
  or context-menu events.
- The renderer **must not** call any trading API.
- Every chart artifact the renderer creates must carry a `Tag` that
  identifies it as a ChartGuard read-only line (e.g.
  `"ecg-readonly-line:entry-avg"`), so cleanup is by reference, never
  by index.
- `ApplyState` is the only update path. The host calls it from the
  same dispatcher cadence used by the panel refresh timer; the
  renderer compares the new state with the last applied state and
  updates only what changed.
- `Detach` removes every artifact the renderer added, regardless of
  state. The host must call `Detach` from `State.Terminated` and from
  any path that disposes the host instance.
- WPF mutations run through the `ChartControl` dispatcher.

### 4.3 Host responsibilities

`ChartGuardPanelHost` (or a small dedicated `ChartGuardLinesHost`
sibling — to be decided in the implementation step) keeps Phase 0 / 1 /
2.x intact and adds:

- A new `[NinjaScriptProperty]` `EnableReadOnlyLines` (default `false`)
  that mirrors the existing `EnableDraftPreview` style. With the default,
  the host renders no lines and the chart looks exactly like today.
- When `EnableReadOnlyLines = true`, after the bridge is up, build a
  `ChartGuardLineState` from:
  - the latest `ObservedAccountSnapshotDto`, and
  - the same hard-coded `Preview Scalper` `StrategyDraft` Phase 2.3 already
    pushes when `EnableDraftPreview = true`,
  and call `renderer.ApplyState(state)` after each refresh.
- On `State.Terminated`: `renderer.Detach()` before clearing references.

Two flags / one flag is an open question for the implementation step. A
single combined flag (`EnableDraftPreview` already true ⇒ also draws the
draft lines) keeps the toggle space small; a second flag adds clarity at
the cost of one more property. The decision lives in the
implementation-step PR description.

## 5. Safety contract

These lines must be enforced when the renderer / host code is written.
Each line maps to a grep that the implementation PR must run clean.

- Lines have **no** event handlers. No `MouseDown`, `MouseUp`,
  `MouseMove`, `MouseLeftButtonDown`, `MouseLeftButtonUp`,
  `MouseRightButtonDown`, `PreviewMouse*`, `MouseEnter`, `MouseLeave`,
  `Click`, `DoubleClick`, `Drag*`, `Drop*`, `KeyDown`, `KeyUp`,
  `KeyBinding`, `InputBindings`, or `ContextMenu`.
- Lines do not modify orders. No `Account.Submit`, `Account.CreateOrder`,
  `Account.Cancel`, `Account.Modify`, `AtmStrategyCreate`, `AtmStrategy*`,
  `Flatten*`, `CancelAllOrders`. None of these may appear under
  `AddOns/Panel/Lines/` or in any new code added for chart lines.
- Lines never call `EnableForControlledTest(...)`. The submit-side bridge
  stays disabled by default and is not touched by line rendering.
- Lines are not draggable. The renderer must use chart artifacts that do
  not expose a movable handle, or must explicitly set the artifact to
  not respond to drag (`IsLocked` / equivalent NT property), and must
  not subscribe to any drag event source.
- The renderer does not import `NinjaTrader.Cbi` or
  `NinjaTrader.Data` trading types. It may import the chart drawing
  surface APIs only.
- The renderer does not persist anything: no `File.*`, no `XmlSerializer`,
  no settings storage.
- Cleanup is by Tag/reference, never by index. The renderer must not
  remove a chart artifact it did not add.

A breach of any of the lines above invalidates the Phase 3 manual test
and must be fixed before any further work.

## 6. Visual rules

- Use the existing `EssencialChartGuardTheme` brushes so chart lines and
  panel rows share the same color identity:
  - `Entry/Avg` line — `TextPrimary` (white) like the panel value text.
  - `Last fill` line — `TextSecondary` (muted gray); slightly transparent
    is acceptable so it sits behind the entry/avg line.
  - `Draft stop` — `AccentRed` to match the panel's stop semantics.
  - `Draft T1 / T2` — `AccentGreen` to match the panel's target semantics.
  - *(deferred)* `Active stop / active targets` — distinct from drafts:
    e.g. solid vs. dashed, brighter vs. dimmer, so the operator can
    immediately tell whether a line is a draft or a real working order.
- Labels are short and positioned at the right edge of the chart so they
  do not overlap candles. Format examples:
  - `Entry/Avg 17875.50`
  - `Last fill 17875.25`
  - `Draft stop 40 Ticks`
  - `Draft T1 x1 @ 40 Ticks`
  - `Draft T2 x1 @ 80 Ticks`
- Lines are 1px thick by default. They must not produce flicker on every
  refresh tick — see the renderer rule that only changed fields rewrite
  artifacts.
- Lines respect the chart instrument: the host only emits a non-zero
  state when the snapshot/draft applies to the chart's `Instrument`. If
  the host is instrument-agnostic (`FilterByChartInstrument=false`) and
  the snapshot has no instrument scope, lines are not drawn.
- Lines do not pollute the chart: at most six lines visible at once
  (Entry/Avg, Last fill, Draft stop, Draft T1, Draft T2, plus one
  reserved future active line). Anything beyond that needs a separate
  spec.

## 7. Manual checklist for Phase 3.1 (implementation step)

These items will be added to `docs/manual-test-checklist.md` in the
implementation PR. **No box is marked off until the implementation
exists and is observed in NinjaTrader.**

### Default behavior (`EnableReadOnlyLines = false`, or single-flag default)

- [ ] After recompile and reload, with the new property at its default,
  the chart looks exactly like Phase 2.3: no ChartGuard lines are drawn.
- [ ] Output Tab 1 shows no `[EssencialUI] PanelHost lines applied ...`
  (or equivalent) lines while the default value is in effect.

### Lines appear when enabled

- [ ] Add the host with `EnableDraftPreview=true` and the lines toggle
  enabled. Within ~500ms of attach, the chart shows:
  - `Entry/Avg <price>` line iff a position is open;
  - `Last fill <price>` line iff `LastPrice` is set;
  - `Draft stop` line at the value derived from the `Preview Scalper`
    `StopDraft`;
  - `Draft T1` and `Draft T2` lines at the values derived from
    `TakeTargetDraft[0]` and `TakeTargetDraft[1]`.
- [ ] Each line has the expected label text, color, and dashing as in
  Section 6.

### Lines update from observed state

- [ ] A manual buy market via Chart Trader/SuperDOM (1 contract) updates
  `Entry/Avg` to the fill price and updates `Last fill` to the same
  price.
- [ ] A manual sell that closes the position removes the `Entry/Avg`
  line; `Last fill` may continue to show the last execution price (same
  rule as the panel's `ACTIVE POSITION → Last fill`).

### Read-only contract preserved

- [ ] No line is draggable. Hovering a line does not change the cursor
  to a drag affordance and dragging it does not move it.
- [ ] No click on a line emits a `[EssencialCommand]` line.
- [ ] No right-click on a line opens a ChartGuard-specific context
  menu. The native NinjaTrader context menu still opens normally.
- [ ] The chart remains clickable and scrollable: cursor crosshair,
  drawing tools, and chart trader continue to work as native NT
  expects.
- [ ] No `[EssencialOrder] Bridge submitted ...`, no
  `[EssencialCommand] DryRun SubmitProtectedEntry ...`, no
  `[EssencialOrder] Bridge enabled for controlled test ...` line
  appears in Output Tab 1 during the entire Phase 3 session.
- [ ] The NinjaTrader Orders tab shows zero orders created by ChartGuard.

### Cleanup

- [ ] Removing the indicator removes every ChartGuard line from the
  chart. No leftover `Entry/Avg`, `Last fill`, `Draft stop`, `Draft T1`,
  `Draft T2` artifact remains.
- [ ] Recompile NinjaScript while the host is attached, then re-add the
  indicator: exactly one set of lines is visible (no duplicates from a
  stale runtime instance).
- [ ] Restart NinjaTrader after a session: no stale ChartGuard lines
  remain on any chart.

### Safety greps (run at the end of the implementation task)

- [ ] No `Account.Submit`, `Account.CreateOrder`, `AtmStrategyCreate`,
  `EnableForControlledTest`, or order Cancel/Modify/Flatten introduced
  by the implementation outside the gated `NinjaTraderAccountAdapter`.
- [ ] No `Click`, `MouseDown`, `MouseUp`, `MouseMove`, `MouseLeftButtonDown`,
  `MouseRightButtonDown`, `PreviewMouse*`, `MouseEnter`, `MouseLeave`,
  `DoubleClick`, `Drag*`, `Drop*`, `KeyDown`, `KeyUp`, `KeyBinding`,
  `InputBindings`, `ContextMenu`, `SelectionChanged`, or `TextChanged`
  handler attached anywhere under `AddOns/Panel/` or the new line code.
- [ ] No `using NinjaTrader.Cbi` or `using NinjaTrader.Data` introduced
  in `AddOns/Panel/`, `AddOns/Panel/Lines/`, or `AddOns/SafeCore/`.
- [ ] No persistence APIs (`File.*`, `XmlSerializer`, `JsonSerializer`,
  settings storage) introduced by the implementation.

## 8. Numbering note

`docs/chartguard-product-map.md` (English) was written earlier and lists
chart lines as **Phase 2 — Chart Lines Read-Only** and dry-run commands
as Phase 3. The newer `docs/plano-integrado-chartguard-pt.md` and
`docs/mapa-funcional-chartguard-pt.md` move chart lines to **Fase 3** and
dry-run commands to **Fase 4**. This document follows the newer Portuguese
plan because it reflects the validated build order in 2026-05.

A future docs cleanup may renumber `chartguard-product-map.md` to match.
Until then, both numberings refer to the same goal: read-only chart
lines fed by the existing observed snapshot and the existing draft model,
implemented after the panel shell (Phase 1/2/2.x) is validated and
**before** any dry-run command path opens.

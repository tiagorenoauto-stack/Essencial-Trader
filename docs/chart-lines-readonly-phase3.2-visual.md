# Phase 3.2 — Read-Only Chart Lines Visual Polish (Specification)

> **Status:** specification only. No code is written in this phase yet.
> Phase 3.1 ships the lines themselves; Phase 3.2 polishes their visual
> until they match (or beat) the native NinjaTrader Chart Trader
> horizontal-line look.

This document captures user-facing visual feedback collected during
Phase 3.1 manual validation on 2026-05-07 and turns it into a spec for
the next iteration. It is the companion of
`docs/chart-lines-readonly-phase3.md`.

For the parent context, see:

- `docs/chart-lines-readonly-phase3.md` — Phase 3 / 3.1 architecture.
- `docs/plano-integrado-chartguard-pt.md` — *Fase 3 — Linhas Read-Only
  No Gráfico* + chart interaction goals.
- `docs/safe-core-contract.md` — read-only contract that still applies.

## 1. Why this phase exists

Phase 3.1 successfully drew the five read-only lines (Entry/Avg, Last
fill, Draft Stop, Draft T1, Draft T2). The renderer compiled and the
draws executed without errors. **The visual quality, however, did not
match the bar set by the operator's reference workflow.**

User feedback (2026-05-07, MNQ 06-26 Playback):

- Lines are thin and easy to miss against candles.
- Labels are tiny, transparent, and hard to read.
- No visible "VENDIDO 1 @ 26.678,25" / "COMPRADO 1 @ ..." badge stating
  the side and quantity at the entry line.
- Reference is the native NinjaTrader horizontal-line look (price
  marker on the right axis, clearly visible) **or** the richer style of
  the third-party "Trade Safe" panel screenshot the operator shared,
  which uses bold colored bars with descriptive multi-line labels such
  as `LIMITE DE PERDA DIÁRIA DE $100 (200 ticks | 50 pts) 1 @ 26.728,25`
  and `META DIÁRIA DE $300 (600 ticks | 150 pts) 1 @ 26.528,25`.

Phase 3.2 closes that gap **without breaking the read-only contract**.

## 2. Goals

A line drawn by ChartGuard should:

1. Be at least as readable as a native NinjaTrader horizontal line:
   - Price marker visible on the right axis showing the price.
   - Solid 2 px (or thicker) main line.
   - Stop / target / drawdown distinction by color and dashing.
2. Carry a label that:
   - Shows the meaning ("ECG Entry", "ECG T1 Draft", etc.).
   - Shows the price.
   - Shows the side / quantity for the Entry line ("Vendido 1",
     "Comprado 2", etc.) so the operator never has to look at the panel
     to know which way the trade is going.
   - Has a filled background and reasonable font size, not transparent
     overlay text.
3. Allow user customization (color, dash style, line width, label font
   size, label background opacity, on/off per line type) so each
   trader can match their preferred chart style.

## 3. Reference behaviors observed

The operator validated against two visual references:

### Native NinjaTrader Chart Trader horizontal line

- Price marker (price tag) on the right axis.
- Solid line in a single color, usually 2 px.
- No floating label on the line itself; the price marker is the label.
- Updates live without flicker.

### "Trade Safe" third-party panel (screenshot)

- Bold colored horizontal bars at preset levels (daily limit, daily
  goal, breakeven, fixed stops/targets).
- Labels at the left edge of the chart with **multi-line text**:
  - Title (e.g. `LIMITE DE PERDA DIÁRIA DE $100 (200 ticks | 50 pts)`).
  - Trade summary at the line price (e.g. `1 @ 26.728,25`).
- Filled label background that contrasts with candles.
- Side badge over the entry line: e.g. `VENDIDO 1 @ 26.678,25` or
  `COMPRADO 1 @ ...`.

Phase 3.2 borrows the visual *feel* of both. It does **not** copy code
from any third-party tool.

## 4. Lines covered by Phase 3.2

Same set Phase 3.1 already supports. Phase 3.2 only changes how each
line is rendered:

- Entry / Avg
- Last fill
- Draft Stop
- Draft T1
- Draft T2
- *(reserved)* Active stop / Active T1 / Active T2

A future Phase 3.x may add daily-limit / daily-goal / drawdown lines
inspired by the Trade Safe screenshot. Those are **out of scope** for
3.2.

## 5. Visual rules (target)

### Lines

- Width: 2 px default; user-adjustable (`1` … `4`).
- Dash style:
  - **Entry / Avg** — solid.
  - **Last fill** — dotted.
  - **Draft Stop** — dashed.
  - **Draft T1 / T2** — dashed.
  - **Active stop / targets** — solid (distinct from drafts when both
    are visible).
- Color (default tokens; user-overridable in settings):
  - Entry / Avg — `#D4A24C` (gold).
  - Last fill — `#9099A8` (muted gray).
  - Draft Stop — `#D14B4B` (red).
  - Draft T1 / T2 — `#3FB35E` (green).
- Native price marker on the right axis must remain on (this is what
  Phase 3.1's full `Draw.HorizontalLine` overload restored).
- The line never accepts mouse interaction (`Drawing.IsLocked = true`
  equivalent).

### Labels

- Anchored at the left edge of the chart (Trade Safe style) **or**
  near the rightmost bar (native style). Pick *one* default and let
  the user toggle in settings.
- Multi-line text is allowed:
  - Line 1 — descriptive label: `ECG Entry`, `ECG Stop Draft`,
    `ECG T1 Draft`, etc.
  - Line 2 — side and quantity, only for the Entry line:
    `VENDIDO 1` / `COMPRADO 2` (Portuguese matching the operator's NT
    locale; English fallback `SHORT 1` / `LONG 2`).
  - Line 3 — price formatted with the instrument's tick precision,
    e.g. `26.678,25` (NT's locale-aware format).
- Background: filled, with ≥80% opacity, color matching the line.
- Outline: subtle, theme `BorderSubtle`-equivalent.
- Font: Segoe UI, 11–12 pt, bold for the side/quantity line, regular
  for the rest.
- Z-order: above candles, below cursor crosshair.

### Side / quantity badge for the Entry line

- Required by the operator's workflow.
- Renders only when `ObservedAccountSnapshotDto.Position` is `Long` or
  `Short` and `AbsoluteQuantity > 0`.
- Text format options (configurable):
  - Portuguese (default): `VENDIDO <qty>` / `COMPRADO <qty>`.
  - English: `SHORT <qty>` / `LONG <qty>`.
- Color follows the position direction (red for Short, green for
  Long), independent of the Entry/Avg gold line color.

## 6. User customization (out of safety scope; in product scope)

Each setting below is a future toggle. None of them adds a command,
order, or trading-API call.

- Line color per type (Entry, Last, Draft Stop, Draft T, Active *).
- Line width (1–4 px).
- Dash style per type (Solid / Dot / Dash).
- Label position (left-edge / right-edge).
- Label font family + size (10–14 pt).
- Label background opacity (0–100 %).
- Show / hide each line type independently.
- Side/quantity badge language (PT / EN).
- "Match native NinjaTrader chart line" preset (one click, sets every
  field above to the values that mirror NT's native horizontal-line
  look).

These settings live in the existing **disabled** Settings card of the
side panel. Wiring the controls and persisting the values is a
separate, larger phase (Settings persistence — Fase 7 in the
Portuguese plan, Phase 6 in the English map) that is **not** part of
Phase 3.2.

## 7. Architecture

The renderer split established in Phase 3.1 is unchanged:

- `ChartGuardLineState` (pure value type) keeps carrying primitives.
  It will gain optional fields for the side/quantity badge:
  `SideBadgeText`, `SideBadgeBrush` (or an enum so the renderer picks
  the brush). Phase 3.2 must keep the struct free of NinjaTrader
  trading types.
- `ChartGuardReadOnlyLineRenderer` keeps owning every chart artifact
  by stable `ECG-ReadOnlyLine-*` tag. Phase 3.2 swaps the current
  `Draw.Text` call for either:
  1. A multi-line `Draw.Text` with bigger font, opaque background, and
     left-edge anchor; or
  2. A `Draw.TextFixed` (chart-corner anchored) for the side/quantity
     badge plus a per-line `Draw.Text` for the line label.
  The renderer must **not** subscribe to mouse/keyboard/drag/drop or
  context-menu events.
- The host (`ChartGuardPanelHost`) keeps converting observed snapshot
  + draft into `ChartGuardLineState`. Phase 3.2 adds the side/qty text
  resolution there (e.g. `LONG <qty>` / `SHORT <qty>` based on
  `dto.Position` and `dto.AbsoluteQuantity`), and the host stays the
  only place that reads the snapshot.

A small visual-options DTO (`ChartGuardLineVisualOptions` or similar)
is suggested as a place to keep tunable parameters once user
customization lands. Until then the renderer can use hard-coded
defaults that mirror the rules in Section 5.

## 8. Safety contract (unchanged from Phase 3.1)

Every line below must hold throughout Phase 3.2 and be re-greppable at
the end of the implementation task:

- No `Account.Submit`, `Account.CreateOrder`, `AtmStrategyCreate`,
  order Cancel/Modify/Flatten, or `EnableForControlledTest` introduced
  outside the gated `NinjaTraderAccountAdapter`.
- No `Click`, `MouseDown`, `MouseUp`, `MouseMove`, `MouseLeftButton*`,
  `MouseRightButton*`, `PreviewMouse*`, `MouseEnter`, `MouseLeave`,
  `DoubleClick`, `Drag*`, `Drop*`, `KeyDown`, `KeyUp`, `KeyBinding`,
  `InputBindings`, `ContextMenu`, `SelectionChanged`, or `TextChanged`
  handler attached anywhere under `AddOns/Panel/`,
  `AddOns/Panel/ChartLines/`, or
  `Indicators/ChartGuardPanelHost/`.
- No `using NinjaTrader.Cbi` or `using NinjaTrader.Data` introduced in
  `AddOns/SafeCore/`, `AddOns/Panel/`, or `AddOns/Panel/ChartLines/`.
- No persistence APIs (`File.*`, `XmlSerializer`, `JsonSerializer`,
  settings storage) introduced by Phase 3.2 itself. Settings
  persistence comes in its own dedicated phase.
- Cleanup is by stable Tag, never by index.

## 9. Manual checklist for Phase 3.2 (implementation step)

These items will be added to `docs/manual-test-checklist.md` when
Phase 3.2 implementation lands. **No box is marked off until the
implementation exists and is observed in NinjaTrader.**

### Visual

- [ ] Each line shows the native price marker on the right axis at
  the expected price.
- [ ] Entry / Avg line is solid 2 px gold and carries a multi-line
  label whose first line is `ECG Entry`, second line is the side+qty
  badge (`VENDIDO 1` / `COMPRADO 1` for the Portuguese default), and
  third line is the formatted price.
- [ ] Last fill line is dotted gray with a `ECG Last <price>` label.
- [ ] Draft Stop is dashed red with a `ECG Stop Draft` label that
  includes the price.
- [ ] Draft T1 / T2 are dashed green with `ECG T1 Draft` / `ECG T2
  Draft` labels including price.
- [ ] Labels have a filled background, are not transparent, and are
  legible over both green and red candles.
- [ ] No flicker during refresh; subsequent ApplyState calls update
  in place via stable tags.

### Side / quantity badge behavior

- [ ] On long 1 contract, the Entry label reads `COMPRADO 1` (or
  `LONG 1` depending on the configured language).
- [ ] On short 1 contract, the Entry label reads `VENDIDO 1` (or
  `SHORT 1`).
- [ ] On flat / unknown, the badge disappears together with the rest
  of the Entry line.
- [ ] Badge color matches the position: green for long, red for
  short. The Entry/Avg main line color stays gold regardless.

### Read-only contract

- [ ] No line or label accepts drag / click / hover-action. Right
  click on a line opens the **native** NinjaTrader context menu, not
  any ChartGuard-specific menu.
- [ ] No `[EssencialCommand] ...`, `[EssencialOrder] Bridge submitted
  ...`, `[EssencialCommand] DryRun SubmitProtectedEntry ...`, or
  `[EssencialOrder] Bridge enabled for controlled test ...` line
  appears during the entire Phase 3.2 session.
- [ ] The NinjaTrader Orders tab shows zero orders created by
  ChartGuard.

### Cleanup

- [ ] Removing the indicator removes every line **and** every label
  artifact. No leftover side/quantity badge remains on the chart.
- [ ] Recompile NinjaScript: exactly one set of lines/labels visible
  afterwards.

### Safety greps

- [ ] All four greps in Section 8 return clean.

## 10. Order of operations

1. Land the small fix that unblocks the Phase 3.1 compile (already
   shipped: `964727c fix(chart): add NinjaTrader.Gui using for
   DashStyleHelper`).
2. Recompile in NinjaScript Editor and validate Phase 3.1 visually
   end-to-end. The labels are still small/transparent in 3.1; that is
   accepted because 3.2 fixes it.
3. Implement Phase 3.2 in its own commit `feat(chart): improve
   read-only line labels and side badge`. Update
   `docs/manual-test-checklist.md` with Phase 3.2 items at that point.
4. After manual validation, ship a `docs: mark phase 3.2 visual
   validation` commit.
5. Open the user-customization phase as a separate, larger spec only
   after 3.2 is validated. Settings persistence is its own contract.

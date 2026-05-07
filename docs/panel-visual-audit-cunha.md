# Cunha Panel Visual Audit

This document records how the historical CunhaTrader Gold panel may influence Essencial ChartGuard.

The historical project is a visual and ergonomic reference only. It is not a source of trading logic, order routing, handlers, risk code, hotkeys, or execution behavior.

## Source Reviewed

Historical local repo:

```text
C:\Users\tiago\ninjatrader-cunhagen
```

Relevant files:

- `src/CunhaScalper/AddOns/CunhaScalperChartPanel.cs`
- `src/CunhaScalper/AddOns/CunhaV2Theme.cs`
- `src/CunhaScalper/Indicators/CunhaScalperChartIndicator.cs`
- `src/CunhaScalper/AddOns/CunhaStatsBar.cs`
- `src/CunhaScalper/AddOns/CunhaHeatmapControl.cs`

## What We May Preserve

The following may be preserved as design direction:

- Right-side embedded panel, like NinjaTrader Chart Trader.
- Full-height vertical panel.
- Horizontal resize behavior by changing panel width.
- Dark premium surface with gold section identity.
- Compact header with connection/status dot, title, subtitle, mode/status, and settings position.
- Dense card sections with strong visual hierarchy.
- Gold section headers with thin underline.
- Large green buy and red sell button shapes as future visual language.
- Strong red emergency button area as future visual language.
- Active position section with compact protection controls.
- Account risk section with metrics and progress bar style.
- Session section near the bottom.
- Small monospace numeric values for price/PnL/risk.
- Compact field labels above inputs.
- Chip-like take/stop visual treatment.
- Overall proportions and spacing rhythm.

These are visual/UX ideas, not code contracts.

## What Must Not Be Preserved

Do not copy or port the following from the historical implementation:

- Any `Account.Submit`, `Account.CreateOrder`, `AtmStrategyCreate`, cancel, modify, flatten, or ATM route.
- Buy/sell/panic handlers.
- Right-click chart order placement.
- Context-menu order actions.
- Hotkey manager or hotkey handlers.
- Bracket manager behavior.
- Risk engine behavior.
- Trail behavior.
- Breakeven or R-lock execution logic.
- Parsing logic from UI controls.
- Settings persistence logic.
- Adoption of manual orders.
- Any direct dependency where UI owns trading state or submits actions.

All future behavior must be rebuilt through:

```text
ChartGuard UI
  -> command
  -> TradeCommandService
  -> RiskGuard / ProtectionService
  -> IOrderSubmitter
  -> OrderEventRouter / ObservedAccountState
```

## Safe Design Extraction

When implementing the Essencial panel, use new ChartGuard classes and new names:

- `EssencialChartGuardTheme`
- `EssencialChartGuardPanel`
- `ChartGuardPanelHost`

Do not paste the old `CunhaScalperChartPanel` as a starting point. Rebuild only the visual structure from scratch, using the screenshots and this audit as the guide.

Allowed visual tokens to recreate in our own code:

- Backgrounds:
  - near-black root
  - dark cards
  - slightly lighter inputs
- Text:
  - white primary
  - muted gray secondary
  - gold labels/section titles
- Accents:
  - green for buy/positive
  - red for sell/danger
  - blue for breakeven/info
  - pink/red for risk pressure
- Shape:
  - compact 5-6px card radius
  - dense margins
  - full-width section cards
  - large CTA buttons only when behavior exists

## First New Panel Scope

The first Essencial panel must be read-only.

It may display:

- Product title: `Essencial ChartGuard`
- Mode: `Read-only`
- Account selected by probe/host
- Instrument from chart
- Observed position
- Observed quantity
- Average/last observed price
- Working order count
- Initial snapshot status
- Event bridge status

It must not display active trading controls unless they are disabled or hidden with a clear reason. Prefer hiding unsupported controls in the first version.

Suggested first layout:

```text
Essencial ChartGuard
● Read-only
account / instrument

STATUS
Position
Quantity
Avg/Last
Working orders

OBSERVATION
Snapshot
Event bridge
Last event

RISK
Daily guard status placeholder only if real state exists

SESSION
Read-only session notes / observed stats only if real state exists
```

## Lateral Injection Rules

The host may use the historical idea of adding a right-side column to the chart host grid, but it must be implemented in new code.

Requirements:

- Run WPF mutations through the chart dispatcher.
- Add a dedicated right-side column.
- Add a `GridSplitter` or equivalent resize affordance.
- Height spans the chart host.
- Width is horizontally resizable.
- Minimum width: around 280px.
- Initial width: around 360-380px.
- Remove panel, splitter, and column in `State.Terminated`.
- Do not install chart context-menu order handlers.
- Do not intercept chart clicks for trading.
- Do not block native Chart Trader/drawing tool behavior.
- Log attach/detach with `[EssencialUI]`.

## Redimensioning Target

Desired behavior:

- Vertical size follows chart height.
- User resizes horizontally by dragging the panel divider.
- Panel content scrolls vertically if needed.
- No vertical manual resizing.
- Layout remains usable at narrow width.

This improves on the old panel without copying its operational internals.

## Implementation Plan

1. Create `EssencialChartGuardTheme` with visual tokens only.
2. Create `EssencialChartGuardPanel` as a read-only WPF `UserControl`.
3. Create a host indicator separate from the temporary probe.
4. Reuse the existing observation flow:
   - auto/manual account selection
   - initial position snapshot
   - `NinjaTraderAccountEventBridge`
   - `ObservedAccountState`
5. Inject the panel on the right side of the chart with horizontal resize.
6. Update panel text from observed state.
7. Verify:
   - compile clean
   - attach/detach clean
   - no duplicate panel after recompile
   - no order APIs called
   - manual Playback orders update status

## Acceptance Gate

The first panel is acceptable only when:

- It visually resembles the old right-side premium panel direction.
- It is clearly branded as Essencial ChartGuard.
- It is read-only.
- It sends no orders.
- It modifies no orders.
- It cancels no orders.
- It flattens nothing.
- Removing the indicator removes the panel completely.
- Re-adding does not duplicate handlers or panels.


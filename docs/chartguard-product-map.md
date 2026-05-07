# Essencial ChartGuard Product Map

This document is the target map for the chart-side product. It describes what
the trader should eventually see and control from the chart, while preserving
the Safe Core rule: no UI path submits, cancels, modifies, or flattens directly.

Portuguese product-facing companion: `docs/mapa-funcional-chartguard-pt.md`.

Source notes:

- `D:\Programacao\Apps\CunhaTrader-Gold\CunhaTrader Gold\Principais Funções Essencial Chart.md`
- Current read-only panel validation in NinjaTrader Playback.
- Existing safety contracts in `docs/safe-core-contract.md` and `docs/panel-command-map.md`.

## Product Goal

Essencial ChartGuard should become a chart-native safety and execution panel:

- clear position information;
- protected entry workflow;
- flexible stops and targets;
- risk/session awareness;
- chart lines with labels;
- personalization for what the trader wants to see or hide;
- one canonical command route through Safe Core.

The panel can be visually inspired by prior work and by observed tools, but the
implementation and command behavior must remain new, audited, and safe.

## Current Status

Validated in Playback:

- Right-side panel host works.
- Horizontal resize works.
- Chart remains usable.
- Account/instrument are detected.
- Initial position snapshot works.
- Order/execution event bridge updates observed state.
- Position, quantity, entry/average price, and working orders can be displayed.
- The panel is read-only and does not send orders.

Still intentionally incomplete:

- Risk section has no real data.
- Session section has no real data.
- Entry/protection controls are not live.
- Chart drawing is not implemented yet.
- Settings/personalization is not implemented yet.

## Information To Show

High-priority operational information:

- Account.
- Instrument.
- Position direction: Flat / Long / Short / Unknown.
- Position quantity.
- Entry or average price.
- Open PnL, ideally configurable as ticks, points, percent, and currency.
- Working order count.
- Active stops and targets.
- Risk status: allowed, blocked, warning.
- Session status: trades, realized PnL, time/session state.

Naming rule:

- Avoid unclear labels such as `Avg/Last` when the field is not a comparison.
- Prefer explicit labels like `Entry/Avg`, `Avg Price`, `Last Fill`, or split
  the values when both are available.

## Panel Areas

### Header

Purpose: fastest possible read while trading.

Target content:

- Brand: `Essencial ChartGuard`.
- Account / instrument.
- Position + quantity.
- Entry/average price.
- Open PnL when available.
- Compact status dot for observer/sim/live/block state.

Avoid duplicating the same information again immediately below unless the lower
section adds useful detail.

### Position

Purpose: detailed view of the active trade.

Target content:

- Direction.
- Quantity.
- Entry/average price.
- Last fill price.
- Open PnL in selected unit(s): ticks, points, percent, currency.
- Active stop and target summary.

### Entry Plan

Purpose: prepare protected entries from the chart.

Target controls:

- Entry type: Market, Limit, StopMarket, StopLimit.
- Quantity.
- Stop required by default.
- Optional one or multiple targets.
- Buy / Sell actions.
- Optional sizing mode.

Safety:

- The visible controls may be designed before they are active, but must be
  clearly disabled/read-only until their command route exists.
- Every eventual action routes through `TradeCommandService`.
- No indicator or WPF control may call NinjaTrader order APIs directly.

### Protection

Purpose: manage an already-open protected position.

Target controls:

- Breakeven.
- Lock 1R.
- Lock 2R.
- Lock 3R.
- Trail mode.
- Stop edit.
- Target add/remove.

Safety:

- Protection actions route through `ProtectionService` and `IOrderSubmitter`.
- R calculations are based on initial risk, not on a moved stop.
- Repeated clicks must be idempotent or blocked.

### Risk

Purpose: keep the trader aware of account/session limits.

Target content:

- Daily loss limit.
- Remaining loss room.
- Drawdown / trailing drawdown when configured.
- Block status for new entries.
- Apex/prop-style account rule indicators if enabled.

Safety:

- Risk blocks new entries.
- Risk must not block flatten or protective actions.

### Session

Purpose: show operating context.

Target content:

- Trades in session.
- Realized PnL.
- Open PnL.
- Session time.
- Historical trade availability state.

### Settings

Purpose: make the panel personal without making it unsafe.

Target settings:

- Show/hide sections.
- Compact/full display mode.
- Preferred PnL units: ticks, points, percent, currency.
- Default stop unit: ticks/points/price.
- Default target behavior.
- Default quantity/sizing mode.
- Account/instrument display preferences.
- Chart-line visibility toggles.

Initial settings button may exist disabled/read-only until a safe settings
surface is implemented.

## Chart Interaction

Target future behavior:

- Add orders from the chart with an intentional gesture such as Ctrl + right
  click.
- Offer organized choices for buy/sell/stop/limit/market behavior.
- Draw entry, stop, target, and drawdown lines.
- Every line has a clear label matching panel terminology.

Safety:

- Chart clicks must not submit directly.
- Chart interaction must create a command and route through Safe Core.
- Native NinjaTrader chart behavior must not be blocked accidentally.
- Unsupported chart actions stay absent or disabled.

## Chart Lines

Target line types:

- Buy limit order.
- Sell limit order.
- Stop.
- Target.
- Entry.
- Drawdown/risk boundary.

Line requirements:

- Clear label.
- Account/instrument scoped.
- Color and style consistent with the panel.
- Updates from observed order/position state.
- Does not guess if the source state is unknown.

## Implementation Phases

### Phase 0 — Observation Shell

Status: in progress / mostly validated.

- Read-only side panel.
- Event bridge observation.
- Initial position snapshot.
- Resize.
- No order actions.

### Phase 1 — Operational Read-Only Layout

Goal:

- Rework the panel into the final area structure: Header, Position, Entry Plan,
  Protection, Risk, Session, Settings.
- Controls may be visible but disabled.
- Improve labels and reduce duplicate information.
- Keep zero trading actions.

### Phase 2 — Chart Lines Read-Only

Goal:

- Draw observed entry/position/order lines.
- Add labels.
- No click interaction yet.

> **Numbering note:** the newer `docs/plano-integrado-chartguard-pt.md`
> moves chart lines to *Fase 3* and dry-run commands to *Fase 4*. The
> technical specification for chart lines lives in
> `docs/chart-lines-readonly-phase3.md` regardless of which phase number
> the parent plan uses. Both phases describe the same read-only chart
> line goal.

### Phase 3 — Dry-Run Commands From Panel

Goal:

- Wire disabled controls to dry-run command flow only.
- Logs prove command creation, validation, risk decision, and submitter result.
- No NinjaTrader order API calls.

### Phase 4 — First Real Protected Entry In Replay

Goal:

- Enable one narrow route after checklist: protected Market entry in Replay/Sim.
- Stop required.
- Target still blocked until separately implemented.
- Adapter remains gated and manually validated.

### Phase 5 — Protection Actions

Goal:

- Breakeven and Lock 1R first.
- Lock 2R/3R and trail after replay validation.

### Phase 6 — Settings And Personalization

Goal:

- Persist show/hide sections and display units.
- Keep safe defaults.
- Make the panel configurable without hiding critical risk warnings by default.

### Phase 7 — Chart Click Workflow

Goal:

- Intentional chart gesture opens a safe choice surface.
- Commands route through Safe Core.
- No direct chart-click submit route.

## Non-Negotiables

- One canonical command route.
- No parallel direct-submit path.
- No hidden live trading behavior.
- No feature is considered done without manual Replay/Sim checklist coverage.
- If visible and enabled, it must work.
- If not implemented, it must be hidden or clearly disabled.

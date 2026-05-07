# Panel Command Map

This document maps the first Essencial ChartGuard panel layout to safe internal commands.

For the broader chart-side product target, see `docs/chartguard-product-map.md`.
For the Portuguese product-facing version, see `docs/mapa-funcional-chartguard-pt.md`.

The panel may be inspired by observable behavior and ergonomic layout from tools such as NTB_TradeSafe, NinjaTrader Chart Trader, and the historical CunhaScalper work. Do not copy protected code, decompile, or reproduce private implementation details from third-party tools.

## Core Principle

The panel is a control surface only.

No button, menu item, chart click, hotkey, or indicator handler may submit, cancel, modify, or flatten orders directly. Every critical action must create a command and route through the safe core.

```text
User action
  -> ChartGuardPanel or ChartGuardIndicator creates a command
  -> TradeCommandService validates and routes
  -> RiskGuard may block new entries
  -> ProtectionService decides stop/target changes (price + reason)
  -> IOrderSubmitter executes or simulates execution
  -> OrderEventRouter receives NinjaTrader order/execution events
  -> Output Tab logs describe the decision and result
```

The default `IOrderSubmitter` is the dry-run implementation: it only logs what *would* have been sent to NinjaTrader. A real NinjaTrader-backed submitter must live behind the same interface and only become the active one after the manual checklist passes.

## Initial Panel Layout

The first panel should be compact, practical, and chart-focused. It should preserve the useful idea of a trader-facing execution panel without making the UI responsible for trading logic.

Suggested first layout:

```text
[Account] [Instrument] [Mode/Status]

[Qty] [Entry Type] [Stop] [Target]

[BUY] [SELL]

[BE] [LOCK 1R]

[CANCEL] [FLATTEN]

[Risk Status / Position Status]
```

Optional later areas:

- Lock 2R and Lock 3R.
- Simple trail.
- Presets/modes.
- Drawdown/profit target status.
- Hotkey status.
- Chart lines for entry, stop, target, and risk.

Unsupported controls must stay hidden or disabled until their command route and manual checklist exist.

## Current Layout — Phase 2 Draft Preview

The panel layout shipped by `ChartGuardPanelHost` is still the final-shape area structure rendered read-only. Phase 2 adds pure draft models and panel mutators so a strategy/entry/stop/target/protection/risk preview can be shown without making any control operational. Every actionable control lives in code as a disabled placeholder; **no `Click`, `SelectionChanged`, `TextChanged`, `MouseDown`, `MouseUp`, `PreviewMouse*`, `ContextMenu`, or `KeyBinding` handler is attached** to any control on this panel, and there is no command path through it today.

```text
HEADER
  Essencial ChartGuard          ● Observer / <Playback|Sim|non-sim>   ⚙
  <account> / <instrument>
  <Position> · qty <n> · avg <price> · wo <n>

STRATEGY                 (disabled preview)
  [ default          ▼ ]   [+] [✎] [❏] [✕]
  Can be updated by SetStrategyName(...) or SetStrategyDraft(...).
  Read-only preview. Strategy persistence/editing is not wired in this build.

ENTRY                    (disabled preview)
  Type:    [ Market   ▼ ]   Qty:     [ 1 ]
  Sizing:  [ Fixed    ▼ ]   Unit:    [ Ticks ▼ ]
  Stop:    [ - ]            Target:  [ - ]
  [          BUY          ]    [          SELL          ]
  [                    PANIC                             ]
  Can be updated by SetEntryPlanPlaceholders(...) or SetEntryPlanDraft(...).
  Read-only preview. Entry / panic routes are not wired in this build.

ACTIVE POSITION          (read-only)
  Direction        <Long|Short|Flat|Unknown>
  Qty              <n>
  Entry / Avg      <price>
  Last fill        <price>
  PnL ticks        -
  PnL points       -
  PnL %            -
  PnL $            -
  Working orders   <n>
  Stop             -
  Targets          -
  Protection       -

TAKES                    (disabled preview)
  (no targets defined)
  [ + Add target ]   [ Edit ]   [ Remove ]
  Can be updated by SetTakeTargetsDraft(...).
  Read-only preview. Takes route is not wired in this build.

STOP                     (disabled preview)
  Current   -
  [ Edit stop ]
  Can be updated by SetStopDraft(...).
  Read-only preview. Stop edit route is not wired in this build.

PROTECTION               (disabled preview)
  [ BE ]      [ Lock 1R ]
  [ Lock 2R ] [ Lock 3R ]
  [ Trail ]
  Summary can be updated by SetProtectionDraft(...).
  Read-only preview. Protection route is not wired in this build.

RISK
  Daily limit  -
  Status       no risk data yet
  Block        -
  Mode         [ Alert  ▼ ]                       (disabled preview)
  Can be updated by SetRiskMetrics(...) or SetRiskModeDraft(...).
  Read-only preview. Risk mode is not wired in this build.

SESSION
  Trades  -
  PnL     -
  Time    no session data yet

OBSERVATION
  Snapshot     ● <state>
  Event bridge ● <state>
```

Phase 2 contract:

- The header summary line is the single source of truth for `Position`, `Quantity`, `Avg/Entry price`, and `Working orders`. `ACTIVE POSITION` mirrors the same fields and adds reserved rows for `Last fill`, `PnL ticks/points/%/$`, `Stop`, `Targets`, `Protection` — those rows display `-` until a real source is wired by the host.
- Strategy / Entry / Takes / Stop / Protection / Settings (gear) controls are visible to communicate the final shape but are kept `IsEnabled=false` and have no event handlers in code. Each one carries a `ToolTip` describing its future role and the `(preview / disabled)` qualifier.
- Draft models live under `AddOns/Panel/Models` and must stay pure preview/configuration types: no NinjaTrader account/order references, no service creation, no command routing.
- `SetStrategyDraft`, `SetEntryPlanDraft`, `SetTakeTargetsDraft`, `SetStopDraft`, `SetProtectionDraft`, and `SetRiskModeDraft` only update disabled controls or read-only labels.
- The Phase 1 host MUST NOT create `TradeCommandService`, `IOrderSubmitter`, `NinjaTraderOrderSubmitter`, or `NinjaTraderAccountAdapter`, and MUST NOT call `EnableForControlledTest(...)`.
- Section visibility is exposed through `EssencialChartGuardPanel.SetSectionsVisibility(EssencialChartGuardPanelSections)` (with `ShowStrategy`, `ShowEntry`, `ShowActivePosition`, `ShowTakes`, `ShowStop`, `ShowProtection`, `ShowRisk`, `ShowSession`, `ShowObservation`). Use `EssencialChartGuardPanelSections.All()` to show every section. Persistence is intentionally not implemented in this phase.

Wiring of any control above to a real command must follow the matching phase in `docs/chartguard-product-map.md` / `docs/plano-integrado-chartguard-pt.md` and only after its manual-test items in `docs/manual-test-checklist.md` pass.

## Command Mapping

| Panel control | User expectation | Internal command | Primary service |
| --- | --- | --- | --- |
| Account selector | Choose the account to affect | Command input | `TradeCommandService` |
| Instrument display | Confirm the chart instrument | Command input | `TradeCommandService` |
| Quantity | Define order size | Command input | `TradeCommandService` |
| Entry type | Market/limit behavior | Command input | `TradeCommandService` |
| Stop field | Define required protection | Command input | `TradeCommandService` / `ProtectionService` |
| Target field | Define optional target | Command input | `TradeCommandService` / `ProtectionService` |
| Buy | Enter long with protection | `ProtectedEntryCommand` | `TradeCommandService` |
| Sell | Enter short with protection | `ProtectedEntryCommand` | `TradeCommandService` |
| Breakeven | Move known stop to entry | `BreakevenCommand` | `ProtectionService` |
| Lock 1R | Move known stop to plus/minus 1R | `LockRCommand` | `ProtectionService` |
| Cancel | Cancel live orders for selected account/instrument | `CancelOrdersCommand` | `TradeCommandService` |
| Flatten | Cancel live orders and close selected position | `FlattenCommand` | `TradeCommandService` |
| Risk status | Show whether new entries are allowed | State display | `RiskGuard` |
| Position status | Show known position/protection state | State display | `OrderEventRouter` / `ChartGuardState` |

## Safety Rules By Command

### Protected Entry

- Reject missing account.
- Reject missing instrument.
- Reject quantity less than 1.
- Reject protected entry without stop.
- Ask `RiskGuard` before attempting any new entry.
- Use one canonical submit path only.
- Log every accept, reject, and submit attempt with `[EssencialCommand]`.

### Cancel Orders

- Affect only the selected account and selected instrument.
- Log detected live orders and cancel result.
- Never cancel orders from a different account or instrument silently.

### Flatten

- Affect only the selected account and selected instrument.
- Cancel live orders for that scope.
- Close the open position for that scope.
- Log requested quantity, detected position, and result.
- Stay available even when new entries are blocked by risk rules.

### Breakeven

- Require an open position.
- Require an active or known initial stop.
- Compute breakeven from entry price and side.
- Route stop modification through `ProtectionService`.
- Log stop source, computed price, and result with `[EssencialProtect]`.

### Lock R

- Require an open position.
- Require known initial risk from entry and initial stop.
- Compute R from the original risk, not from a moved stop.
- Route stop modification through `ProtectionService`.
- Start with Lock 1R only; add 2R/3R after replay validation.

### Risk Guard

- Block new entries when configured daily loss or drawdown rules are reached.
- Allow flatten and protective actions while blocked.
- Log the reason for each block with `[EssencialRisk]`.

## NTB_TradeSafe Study Boundaries

NTB_TradeSafe may be studied only during normal use by observing visible behavior.

Allowed:

- Layout observations.
- Button grouping.
- User flow.
- Visible risk/protection behavior.
- Error/warning behavior.
- How blocked actions are communicated.
- Which features feel useful or unnecessary.

Not allowed:

- Copying code.
- Decompiling.
- Extracting protected internals.
- Recreating private implementation details.
- Treating NTB behavior as automatically safe without our own validation.

## Historical CunhaScalper Lessons

The historical CunhaScalper work may inform the new project, but ChartGuard must not inherit unsafe architecture.

Useful ideas to consider:

- Practical side panel layout.
- Basic buy/sell/flatten workflow.
- Breakeven and R-lock concepts.
- Position sizing helpers.
- Unit conversion helpers.
- Session clock concepts.
- Risk engine concepts, after validation.
- Presets and modes.

Risks to avoid:

- Chart indicator click handlers submitting orders directly.
- UI class owning submit, risk, hotkeys, trail, parsing, and state all at once.
- Parallel order routes that perform the same trading action.
- Reverse-position behavior that submits before confirming the position is flat.
- Protection creation that is not idempotent across duplicate execution events.
- Visible controls that do not have implemented behavior.
- Documentation drifting away from code.

## First Safe Core Target

The first usable nucleus should prove this route:

```text
Protected Buy/Sell
  -> required stop validation
  -> one command route
  -> order/execution events routed
  -> known initial protection state
  -> flatten for selected account/instrument
  -> auditable Output Tab logs
```

Only after this passes manual NinjaTrader replay should the panel expand to trail, Lock 2R/3R, presets, hotkeys, or more advanced risk modes.

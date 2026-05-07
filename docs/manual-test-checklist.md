# Manual Test Checklist

Manual NinjaTrader validation is required for every critical feature.

Before adding a new visible panel feature, confirm it fits the target map in
`docs/chartguard-product-map.md` and has a safe command/test path here.

## Environment

- NinjaTrader 8 restarted before critical validation.
- One clean chart.
- One selected playback/sim account.
- One selected instrument.
- Output Window Tab 1 open.
- Modo treino/simulation state clearly known.

## Pre-Trade Safety

- [ ] Account missing blocks entry and logs `[EssencialCommand]`.
- [ ] Instrument missing blocks entry and logs `[EssencialCommand]`.
- [ ] Quantity less than 1 is rejected or normalized.
- [ ] Protected entry without stop is rejected.
- [ ] Daily loss guard blocks new entries when threshold is reached.
- [ ] Daily loss guard still allows flatten/protective actions.

## Protected Entry

- [ ] Buy market with stop creates exactly one intended entry route.
- [ ] Sell short market with stop creates exactly one intended entry route.
- [ ] Buy limit with stop creates exactly one intended entry route.
- [ ] Sell short limit with stop creates exactly one intended entry route.
- [ ] Filled entry creates/associates protection once.
- [ ] Duplicate `ExecutionUpdate` does not duplicate protection.
- [ ] Output logs show command, order event, execution event, and protection state.

## Protection Actions

- [ ] Breakeven moves stop once.
- [ ] Lock 1R computes from initial stop, not current moved stop.
- [ ] Repeated BE/Lock clicks do not create duplicate or conflicting stop changes.
- [ ] Stop change rejection is visible and logged.

## Flatten

- [ ] Flatten cancels live orders for selected account/instrument.
- [ ] Flatten closes open position for selected account/instrument.
- [ ] Flatten does not affect other instruments.
- [ ] Flatten does not affect other accounts.
- [ ] Flatten remains available when new entries are blocked by risk guard.

## Stress

- [ ] 10 sequential replay entries complete without duplicate protection.
- [ ] 10 sequential flatten operations complete without stale state.
- [ ] Restart NinjaTrader after a session and confirm no duplicate handler behavior.

## Visual Truthfulness

- [ ] Every visible button/control has a real implemented behavior.
- [ ] Unsupported features are hidden or clearly unavailable.
- [ ] The visible position state matches NinjaTrader account position.

## First Real Market Protected Entry Replay Test

This section gates the very first real-submit route: `ProtectedEntry` with `EntryType == Market` going through `NinjaTraderOrderSubmitter`. Run with the dry-run submitter swapped out for `NinjaTraderOrderSubmitter` only inside Replay/Sim, with the Output Window Tab 1 visible.

- [ ] Buy market with `StopPrice` set: produces exactly one entry and one stop on the resolved account/instrument; log shows `Result ProtectedEntry outcome=Accepted submitter=NinjaTrader mode=submitted ref=...`.
- [ ] Sell market with `StopPrice` set: produces exactly one entry and one stop on the resolved account/instrument; log shows `Submitted`.
- [ ] Buy market with `StopDistanceTicks` set: stop is anchored to the adapter's reference price and rounded to tick; log shows the computed stop price.
- [ ] Sell market with `StopDistanceTicks` set: stop is anchored to the adapter's reference price and rounded to tick; log shows the computed stop price.
- [ ] Market entry without any stop is blocked before reaching the submitter (rejected by `TradeCommandService`).
- [ ] Market entry with both `StopPrice` and `StopDistanceTicks` is rejected (ambiguous stop).
- [ ] `EntryType == Limit` returns `Skipped` with reason `entry type not implemented for real submit yet` and no NinjaTrader order is created.
- [ ] `EntryType == StopMarket` returns `Skipped` with the same reason and no NinjaTrader order is created.
- [ ] `EntryType == StopLimit` returns `Skipped` with the same reason and no NinjaTrader order is created.
- [ ] When the command targets account A and the active resolver scope is account B, the submitter aborts (`Skipped` or `Error`); no order on either account.
- [ ] When the command targets instrument X and the resolver scope is instrument Y, the submitter aborts; no order on either instrument.
- [ ] When the adapter cannot provide a reliable reference price, `StopDistanceTicks` returns `Skipped` and no NinjaTrader order is created.
- [ ] When a target (`TargetPrice` or `TargetDistanceTicks`) is supplied alongside a Market entry, the submitter returns `Skipped` with reason `target not implemented for first real submit route` and **no** entry is created.
- [ ] When the adapter reports `IsSimulationOrPlayback == false` (live account), the submitter returns `Skipped` with reason `first real submit route is limited to simulation/playback` and no NinjaTrader order is created.
- [ ] On a sim/playback account, a Market entry with a valid stop and no target is the only path that reaches `Submitted`.
- [ ] No parallel route: only `NinjaTraderOrderSubmitter.SubmitProtectedEntry` produced the order; UI/Indicator did not call `Account.Submit` directly.
- [ ] `Flatten` button still logs `Skipped` with reason `not implemented for real submit yet` and no NinjaTrader cancel/close happens.
- [ ] `Cancel` button still logs `Skipped` with the same reason and no NinjaTrader cancel happens.
- [ ] `Breakeven` button still logs `Skipped` from the submitter; no stop is moved on NinjaTrader.
- [ ] `Lock 1R` button still logs `Skipped` from the submitter; no stop is moved on NinjaTrader.
- [ ] `Submitted` appears in the final log only when the adapter actually called NinjaTrader. `Simulated` never appears for these market entries when `NinjaTraderOrderSubmitter` is wired.

## NinjaTrader Bridge Replay Test

This section gates `NinjaTraderAccountAdapter` + `NinjaTraderAccountResolver` (the real bridge under `src/EssencialChartGuard/AddOns/NinjaTraderBridge/`). Run only on Replay/Sim. The Output Window Tab 1 must be visible with the `[Essencial*]` prefixes.

- [ ] **BLOCKER — Do not run real bridge until entry-stop lifecycle behavior is validated.** No item below may be exercised until the entry-stop lifecycle has been verified end-to-end against real `OrderUpdate`/`ExecutionUpdate` events through `OrderEventRouter`, or until a safer bracket/ATM mechanism replaces the current entry+stop submission. Until that is done, leave the adapter disabled (default state) and confirm a default-state submit attempt logs `[EssencialOrder] Bridge refused submit: real bridge disabled pending entry-stop lifecycle validation` and no NinjaTrader order is created.
- [ ] Default-disabled check: with no `EnableForControlledTest(...)` call, any `SubmitMarketEntryWithStop` attempt returns `Fail` with reason `real bridge disabled pending entry-stop lifecycle validation`. No `Bridge submitted ...` log appears. No order appears in the Orders tab.
- [ ] Acknowledgement gate: `EnableForControlledTest("wrong string")` is refused and logged as `Bridge enable refused: acknowledgement mismatch`. The adapter remains disabled.
- [ ] Once the lifecycle blocker above is cleared in a separate iteration, `EnableForControlledTest(NinjaTraderAccountAdapter.EnableAcknowledgement)` is the only way to unblock the bridge for one Replay session, and `Disable()` must be called at the end of that session.
- [ ] Bridge wired: `NinjaTraderOrderSubmitter` is built with a `NinjaTraderAccountResolver` that holds a `NinjaTraderAccountAdapter`. `DryRunOrderSubmitter` is **not** in the path during the test.
- [ ] Buy market with stop on a Sim/Playback account: log shows `[EssencialOrder] Bridge submitted ... entryName=ECG-Entry-... entryOrderId=... stopName=ECG-Stop-... stopOrderId=... oco=ECG-OCO-...`.
- [ ] Sell market with stop on a Sim/Playback account: same log shape with the correct sides (`SellShort` entry, `BuyToCover` stop).
- [ ] Exactly one entry order and exactly one stop order are visible in the NinjaTrader Orders tab for the resolved account/instrument; nothing on any other account/instrument.
- [ ] No target order is visible in the Orders tab. The bridge must never create a target.
- [ ] Resolver mismatch: when the command targets account A and the bound adapter is account B, log shows `Resolver refused: account mismatch ...` and no NinjaTrader order is created.
- [ ] Resolver mismatch: when the command targets instrument X and the bound adapter is instrument Y, log shows `Resolver refused: instrument mismatch ...` and no NinjaTrader order is created.
- [ ] Adapter scope mismatch: even if a caller bypassed the resolver and handed a request with a different account/instrument to `SubmitMarketEntryWithStop`, the adapter returns `Fail` with reason `account mismatch:` / `instrument mismatch:` and no order is created.
- [ ] Live (non-sim/non-playback) account refused: when the active connection is not `Provider.Simulator` / `Provider.Replay` and its name does not contain `Sim` or `Playback`, the adapter returns `Fail` with reason `account is not simulation/playback` and no order is created.
- [ ] Disconnected account refused: when the connection is not `Connected`, the adapter returns `Fail` with reason `account not connected` and no order is created.
- [ ] No reference price: when `Instrument.MarketData.Last` is not usable (just subscribed, not yet ticking), `GetReferencePriceForMarketEntry()` returns null, the submitter Skips, and the adapter is **not** called.
- [ ] `CreateOrder` failure path: if either entry or stop comes back null from `CreateOrder`, no `Submit` happens and the log shows `Bridge CreateOrder returned null entry=... stop=... -- nothing submitted`.
- [ ] `Submit` exception path: if `Submit` throws, the log shows `Bridge Submit failed ... ex=...` and `SubmitResult` is `Error` (no silent partial state expected, but verify in the Orders tab that no orphan order survived).
- [ ] Orphan stop check (known risk): if the entry is Rejected by NinjaTrader, verify in the Orders tab that no working stop remains. If a working stop remains, **stop the rollout** and report — the next iteration must wire `OrderEventRouter` to the bridge to cancel the stop on entry rejection.
- [ ] Restart-safety: after the session, NinjaTrader is restarted; on the next launch no stale ChartGuard handler attempts to use a disposed adapter.

## Event Bridge Observation Test

This section gates `NinjaTraderAccountEventBridge` (read-only). It must be exercised before the submit path is unblocked. The bridge submit path (`NinjaTraderAccountAdapter`) must stay disabled for the entirety of this test — `EnableForControlledTest` must **not** be called.

### Probe steps (using **Essencial ChartGuard - Event Bridge Probe** indicator, class `ChartGuardEventBridgeProbe`)

- [ ] 1. Open NinjaTrader and start a Replay/Playback connection on a Sim/Playback account; open one chart for the test instrument; open Output Window Tab 1 with `[Essencial*]` filters visible.
- [ ] 2. Right-click the chart → Indicators → add **Essencial ChartGuard - Event Bridge Probe**. Leave `Account` on `<Auto>` (default) to auto-detect a Sim/Playback account, or pick a specific account from the dropdown / type one matching `Name`/`DisplayName`. Set `Filter events by chart instrument` as desired (true is the default and recommended). Click OK.
- [ ] 3. Confirm logs `[EssencialUI] Probe account mode=<<Auto>|manual> requested=...`, `[EssencialUI] Probe attaching ...`, `[EssencialUI] Probe selected account=... mode=... sim=...`, optional `[EssencialUI] Probe observing non-sim account read-only ...` (only when `sim=false`), `[EssencialUI] Probe attached account=... instrumentFilter=...`, and `[EssencialOrder] EventBridge subscribed account=... instrumentFilter=...`.
- [ ] 4. From Chart Trader / SuperDOM (NinjaTrader's own controls — never from ChartGuard), create a manual working order on the same account+instrument.
- [ ] 5. Confirm a `[EssencialOrder] EventBridge OrderUpdate received ... state=Working` (or equivalent) followed by `EventBridge OrderUpdate routed ...` line for the order.
- [ ] 6. Either let the order fill or cancel it manually from Chart Trader / SuperDOM.
- [ ] 7. On fill, confirm `[EssencialOrder] EventBridge ExecutionUpdate received ...` followed by `EventBridge ExecutionUpdate routed ...`. On cancel, confirm `[EssencialOrder] EventBridge OrderUpdate received ... state=Cancelled` and the matching `routed` line.
- [ ] 8. Remove the indicator: Indicators dialog → select **Essencial ChartGuard - Event Bridge Probe** → Remove.
- [ ] 9. Confirm logs `[EssencialUI] Probe detached account=...` and `[EssencialOrder] EventBridge unsubscribed account=...`.
- [ ] 10. Create another manual order in Chart Trader and confirm **no** `EventBridge` lines (received, routed, ignored) are written by ChartGuard for this new order — the bridge no longer routes anything.

- [ ] Submit path verified disabled: `NinjaTraderAccountAdapter` is in its default state and `EnableForControlledTest(...)` was not called during this test session.
- [ ] Bridge connected: instantiate `NinjaTraderAccountEventBridge(logger, account, router, instrumentFullName)` for the active sim/playback `Account` and call `Start()`. Log shows `[EssencialOrder] EventBridge subscribed account=... instrumentFilter=...`. Calling `Start()` a second time logs `EventBridge Start ignored: already running ...` and does not double-subscribe.
- [ ] Manual order created in NinjaTrader (e.g. via Chart Trader/SuperDOM, on the same account+instrument): log shows `EventBridge OrderUpdate received ... state=Working` (or equivalent state) followed by `EventBridge OrderUpdate routed ...` for at least the first stable transition. Subsequent identical events are de-duplicated by the router and logged as `duplicate-or-rejected`.
- [ ] Manual order fills: log shows `EventBridge ExecutionUpdate received ... executionId=... orderId=... qty=... price=...` and then `EventBridge ExecutionUpdate routed ...`. Re-emitted execution events for the same `ExecutionId` are de-duplicated and logged as `duplicate-or-rejected`.
- [ ] Manual order cancelled in NinjaTrader: log shows `EventBridge OrderUpdate received ... state=Cancelled` (or equivalent) and the corresponding `routed` line.
- [ ] Account filter respected: a manual order on a different account does not produce any `routed` log; instead the bridge logs `EventBridge OrderUpdate ignored: account mismatch ...` (or the bridge does not see it at all because it is bound to a single account; either is acceptable, but no spurious routing must occur).
- [ ] Instrument filter respected: with `instrumentFullName` set, a manual order on the same account but on a different instrument logs `EventBridge OrderUpdate ignored: instrument mismatch ...` and is not routed.
- [ ] `Dispose()` detaches handlers: call `Dispose()` on the bridge; log shows `EventBridge unsubscribed account=...`. After this, create another manual order in NinjaTrader and verify **no** `EventBridge OrderUpdate received` line appears for it.
- [ ] `Stop()` is idempotent: calling `Stop()` (or `Dispose()`) again logs `EventBridge Stop ignored: not running ...` and does not throw.
- [ ] No-duplicate-on-reconnect: stop the Replay/Playback connection, restart it, and start a new bridge instance with `Start()`. Each subsequent NinjaTrader event produces exactly one `routed` log line, not two — confirms the previous instance's handlers were fully detached.
- [ ] No order created by ChartGuard during the entire session: the NinjaTrader Orders tab shows only orders the operator created manually. `[EssencialOrder] Bridge submitted ...` does **not** appear. `[EssencialOrder] Bridge refused submit: real bridge disabled pending entry-stop lifecycle validation` appears if any submit was attempted (it should not be, but the disabled state is itself part of the test).

### Observed state checks (approximate, not authoritative)

The `ObservedAccountState` is built only from routed events; treat it as observed/approximate until reconciled against `Account.Positions` in a later iteration.

- [ ] After a manual buy market is filled (1 contract): log shows `[EssencialOrder] ObservedState account=... instrument=... position=Long qty=1 lastPrice=... workingOrders=0`. `qty` is the absolute net size; `lastPrice` matches the execution price.
- [ ] After a manual sell market is filled while flat (1 contract): log shows `position=Short qty=1`. After a manual buy that closes a 1-lot short: log shows `position=Flat qty=0`.
- [ ] Reducing/inverting fills: a sell that exceeds an open long flips `position` to `Short` with the residual quantity (e.g. long 1 → sell 2 → `position=Short qty=1`). The log line is emitted only on the resulting net change.
- [ ] Cancelling a working manual order without a fill: `ObservedState` reflects `workingOrders` decreasing by one; `position` and `qty` are unchanged.
- [ ] Duplicate `ExecutionUpdate` (same `ExecutionId`) does not move position twice. The router logs `Execution update duplicate ignored ...`; no second `ObservedState` line is emitted for the duplicate.
- [ ] Instrument filter respected: while `FilterByChartInstrument=true`, manual events on a different instrument do not change `position`/`qty`/`workingOrders` and emit no `ObservedState` line.
- [ ] Unknown/empty `ActionText` falls back to `position=Unknown` rather than guessing a side. Confirm by inspecting an event whose `action=-` (no recognised text).

### Initial position snapshot

The probe calls `NinjaTraderPositionSnapshotReader` on attach and seeds `ObservedAccountState` from `Account.Positions` before subscribing to events. This is read-only.

- [ ] Attach probe while the account is already **Long 1** on the chart instrument: log shows `[EssencialOrder] PositionSnapshot long account=... instrument=... qty=1 avg=...` followed by `[EssencialOrder] ObservedState ... position=Long qty=1 ...` before any `EventBridge subscribed` line.
- [ ] Attach probe while the account is already **Short 1** on the chart instrument: log shows `PositionSnapshot short ... qty=1 ...` followed by `ObservedState ... position=Short qty=1 ...`.
- [ ] Attach probe while flat on the chart instrument: log shows `PositionSnapshot read ... result=Flat qty=0 (no matching position)` (or `PositionSnapshot flat ...` when a `Flat` Position object is present) and `ObservedState` reports `account=<account> instrument=<instrument> position=Flat qty=0` — `account` and `instrument` come from the snapshot and must not be `?` even with no execution yet.
- [ ] During the entire attach/snapshot phase, no `Bridge submitted ...`, no `[EssencialOrder] EventBridge OrderUpdate ...`, no NinjaTrader Order is created. The reader does not place or modify any order.
- [ ] After the snapshot is applied, a manual fill on the same instrument continues to update the position (e.g. snapshot `Long 1` → manual sell 1 → `ObservedState position=Flat qty=0`).
- [ ] Snapshot read failure path: if the reader returns null (or throws), the log shows `Probe initial snapshot unavailable; state starts Unknown` (or `Probe snapshot error ex=... ; state starts Unknown`) and observation still proceeds — `ObservedState` reports `position=Unknown` until the next routed execution.

## Read-only Side Panel Test (Panel Host) — Phase 1/2 Operational Layout

This section gates the **Phase 1/2** read-only operational layout served by **`Essencial ChartGuard - Panel Host`** (class `ChartGuardPanelHost`, namespace `NinjaTrader.NinjaScript.Indicators.EssencialChartGuard`). It supersedes the earlier "Phase 0" / observation-shell layout. The submit-side bridge (`NinjaTraderAccountAdapter`) must remain disabled for the entirety of this test — `EnableForControlledTest(...)` must **not** be called. No checklist box should be marked off here without the matching observation in NinjaTrader.

This phase is read-only by contract: the panel renders a final-shape area structure (Header, Strategy, Entry, Active Position, Takes, Stop, Protection, Risk, Session, Observation) but every actionable control inside it stays disabled. Phase 2 adds model/draft mutators only; the controls remain preview/disabled. See `docs/chartguard-product-map.md` Phase 1/2 for the full goal.

### Phase 2.2 validation note — 2026-05-07

Manually validated in NinjaTrader on a clean chart by the user. Confirmed observations:

- NinjaScript compile succeeded with **0 errors**; the host appears in the Indicators dialog.
- Panel host attached on a clean chart with `Account=<Auto>` and `Filter events by chart instrument=true`.
- Layout matches the Phase 1/2 final-shape order: HEADER, STRATEGY, ENTRY, ACTIVE POSITION, TAKES, STOP, PROTECTION, RISK, SESSION, OBSERVATION.
- All actionable controls in Strategy, Entry (Type / Qty / Sizing / Unit / Stop / Target / BUY / SELL / PANIC), Takes, Stop, Protection, Risk Mode, and the header gear remain visibly disabled and unresponsive.
- Snapshot row showed `Flat` correctly when attached against a flat account, with the header summary populated (no `?`).
- EventBridge updated the header summary and `ACTIVE POSITION` rows on a manual buy market via Chart Trader/SuperDOM (`Long 1`), then back to `Flat` on the closing manual sell.
- During the retest a duplicate panel was observed; root cause was **two host instances on the same chart** (operator error). Removing the duplicate restored the single-panel behavior. No code change required.
- Horizontal resize via the `Thumb` grip continued to work; chart remained usable.
- No ChartGuard-issued order was observed — no `[EssencialOrder] Bridge submitted ...` and no `[EssencialCommand] DryRun SubmitProtectedEntry ...` lines.
- Detach (Indicators → Remove) cleared the panel, the resize grip, and the added Grid columns, and the matching `PanelHost panel removed` / `EventBridge unsubscribed` / `PanelHost detached` lines were emitted. Subsequent manual orders produced no further `EventBridge` lines.

The boxes marked off below in this section reflect exactly what was observed in this Phase 2.2 session. Items requiring scenarios not exercised in this session — long/short snapshot at attach, manual sell short, working limit order, recompile-with-instance-on-chart, and draft-mutator calls — remain unchecked and must be validated separately when their scenario is run.

### Compilation and discovery

- [x] The full ChartGuard tree (SafeCore + NinjaTraderBridge + Panel + Indicators) compiles in the NinjaScript Editor with **0 errors**. *(Phase 2.2 — 2026-05-07)*
- [x] The Indicators dialog lists **`Essencial ChartGuard - Panel Host`** under the **EssencialChartGuard** group (namespace `NinjaTrader.NinjaScript.Indicators.EssencialChartGuard`). *(Phase 2.2 — 2026-05-07)*

### Panel injection / removal

- [x] Add the host indicator to a clean chart with `Account=<Auto>` and `Filter events by chart instrument=true`. The right-side panel appears, full chart height. *(Phase 2.2 — 2026-05-07)*
- [x] Header shows: brand `Essencial ChartGuard`, a status dot with a mode line (`Observer`, `Observer / Playback`, `Observer / Sim`, or `Observer / non-sim`), a small disabled gear (`⚙`) on the right of the mode, an `<account> / <instrument>` line, and a one-line summary `<position> · qty <n> · avg <price> · wo <n>`. *(Phase 2.2 — 2026-05-07)*
- [x] Below the header, in this order: `STRATEGY`, `ENTRY`, `ACTIVE POSITION`, `TAKES`, `STOP`, `PROTECTION`, `RISK`, `SESSION`, `OBSERVATION`. *(Phase 2.2 — 2026-05-07)*
- [ ] Header position chip is colored: green for Long, red for Short, neutral white for Flat, muted gray for Unknown.
- [x] `ACTIVE POSITION` shows `Direction`, `Qty`, `Entry/Avg`, `Last fill`, `PnL ticks`, `PnL points`, `PnL %`, `PnL $`, `Working orders`, `Stop`, `Targets`, `Protection`. Direction/Qty/Entry-Avg/Last fill/Working orders mirror the header summary; PnL/Stop/Targets/Protection rows display `-` until a real source is wired. *(Phase 2.2 — 2026-05-07)*
- [x] `OBSERVATION` card lists `Snapshot` and `Event bridge` rows, each with its own status dot. *(Phase 2.2 — 2026-05-07)*
- [x] The vertical resize grip on the left edge of the panel resizes the panel horizontally. Initial width ~370px; the panel does not shrink below ~280px and does not grow past ~640px. *(Phase 2.2 — 2026-05-07)*
- [x] Vertical resize is not exposed; the panel always occupies the full chart height. *(Phase 2.2 — 2026-05-07)*
- [ ] At `State.DataLoaded`, the log shows `[EssencialUI] PanelHost panel injected initialWidth=370px minWidth=280px`.
- [ ] No native NinjaTrader Chart Trader / drawing-tool / context-menu behavior is blocked or replaced. Right-clicking the chart still opens the standard NinjaTrader context menu.

### Snapshot states

- [x] Attach the host while flat on the chart instrument: the `OBSERVATION` snapshot row shows a green/Ok dot with text like `applied flat qty=0`, the header summary chip reads `Flat · qty 0 · avg - · wo 0`, and the `<account> / <instrument>` line is fully populated (no `?`). *(Phase 2.2 — 2026-05-07)*
- [ ] Attach the host while already **Long 1** on the chart instrument: snapshot row shows `applied long qty=1`; header chip reads `Long · qty 1 · avg <price> · wo 0` (green Long); `ACTIVE POSITION → Direction` is `Long` (green), `Qty=1`, `Entry/Avg=<price>`, `Last fill=<price>`, `Working orders=0`.
- [ ] Attach the host while already **Short 1** on the chart instrument: snapshot row shows `applied short qty=1`; header chip and `ACTIVE POSITION` rows mirror it (`Short` red).

### Live updates from manual orders

- [x] With the host attached, place a manual buy market via Chart Trader/SuperDOM (1 contract): within ~500ms the header chip flips to `Long · qty 1 · avg <fill price> · wo 0` and `ACTIVE POSITION → Entry/Avg` and `Last fill` both show the same fill price. Output Tab 1 shows the matching `[EssencialOrder] EventBridge OrderUpdate`/`ExecutionUpdate` and `ObservedState ...` lines. *(Phase 2.2 — 2026-05-07)*
- [x] Manual sell that closes the long: header chip flips to `Flat · qty 0 · avg - · wo 0` and `ACTIVE POSITION → Direction` becomes `Flat`. *(Phase 2.2 — 2026-05-07)*
- [ ] Manual sell short: header chip flips to `Short · qty 1 · avg <price> · wo 0` (red); `ACTIVE POSITION → Direction` is `Short` (red).
- [ ] Place a working manual limit order (no fill): the `wo` count in the header summary and `ACTIVE POSITION → Working orders` both increment by 1; position chip and qty are unchanged. Cancel it: both decrement by 1.

### Disabled placeholders (Phase 1 contract)

- [x] `STRATEGY` shows a combobox previewing the current strategy name and four small action buttons (`+`, `✎`, `❏`, `✕`). All five controls are visibly disabled and unresponsive to clicks. Each control carries a tooltip explaining its future role. *(Phase 2.2 — 2026-05-07)*
- [x] `ENTRY` shows fields `Type` (combobox, default `Market`), `Qty` (text, default `1`), `Sizing` (combobox, default `Fixed`), `Unit` (combobox, default `Ticks`), `Stop` (text), `Target` (text). All are disabled. Below them, `BUY` (green), `SELL` (red), and `PANIC` (red) buttons are visible but disabled and unresponsive. Each control has a tooltip describing its future behavior and the `(preview / disabled)` qualifier. *(Phase 2.2 — 2026-05-07)*
- [x] `TAKES` shows an italic `(no targets defined)` line and three disabled buttons `+ Add target`, `Edit`, `Remove`, each with a tooltip. *(Phase 2.2 — 2026-05-07)*
- [x] `STOP` shows a `Current` row (value `-`) and a single disabled `Edit stop` button with a tooltip. *(Phase 2.2 — 2026-05-07)*
- [x] `PROTECTION` shows disabled `BE`, `Lock 1R`, `Lock 2R`, `Lock 3R`, `Trail` buttons. Each has a tooltip explaining its future role. *(Phase 2.2 — 2026-05-07)*
- [x] `RISK` shows `Daily limit`, `Status`, `Block` rows plus a disabled `Mode` combobox (default `Alert`). Tooltips on label/value/select describe the future Alert/Block/Off behavior. *(Phase 2.2 — 2026-05-07)*
- [x] `SESSION` shows `Trades`, `PnL`, `Time` rows. With no session source wired yet, values default to `-` / `-` / `no session data yet`. *(Phase 2.2 — 2026-05-07)*
- [x] The header gear `⚙` is visible but disabled. Clicking it does nothing and opens no window. No persistence happens during the session. *(Phase 2.2 — 2026-05-07)*
- [x] Every disabled section has a small italic footnote making it clear the section is read-only / not wired in this build. *(Phase 2.2 — 2026-05-07)*
- [x] Hovering any disabled button, combobox, textbox or label shows a tooltip describing the control's purpose and that it is in preview / read-only. *(Phase 2.2 — 2026-05-07)*

### Detach / recompile / restart

- [x] Remove the host indicator from the Indicators dialog. The panel, resize grip, and added Grid columns disappear. The chart returns to its original layout (no leftover thin column on the right). *(Phase 2.2 — 2026-05-07)*
- [ ] After removal, log shows `[EssencialUI] PanelHost panel removed`, `[EssencialOrder] EventBridge unsubscribed account=...`, and `[EssencialUI] PanelHost detached account=...`. New manual orders after this produce **no** further `EventBridge` or `PanelHost` lines.
- [ ] Recompile NinjaScript while a host instance is on the chart, then re-add the indicator. Exactly one panel is visible on the chart (no duplicate panel, no duplicate splitter, no extra Grid column). *(Note: a duplicate-panel observation in 2026-05-07 was traced to two host instances added on the same chart — operator error, not a recompile/duplicate-handler regression. This recompile-while-attached scenario itself was not exercised in 2026-05-07 and remains pending.)*
- [ ] Restart NinjaTrader after a host session and confirm no stale handler/panel from the previous instance remains.

### Safety (no orders, Phase 1)

- [x] During the entire panel session: no `[EssencialOrder] Bridge submitted ...` line appears. *(Phase 2.2 — 2026-05-07)*
- [x] During the entire panel session: no `[EssencialCommand] DryRun SubmitProtectedEntry ...` line appears. *(Phase 2.2 — 2026-05-07)*
- [x] The NinjaTrader Orders tab shows only orders the operator created manually — never any order created by ChartGuard. *(Phase 2.2 — 2026-05-07)*
- [x] All actionable controls in the panel — strategy combobox + `+`/`✎`/`❏`/`✕` buttons, entry `Type`/`Sizing`/`Unit` comboboxes, entry `Qty`/`Stop`/`Target` textboxes, `BUY`/`SELL`/`PANIC` buttons, `+ Add target`/`Edit`/`Remove` takes buttons, `Edit stop` button, `BE`/`Lock 1R`/`Lock 2R`/`Lock 3R`/`Trail` protection buttons, risk `Mode` combobox, header `⚙` gear button — are visible **but disabled**. None of them carries a `Click`, `SelectionChanged`, `TextChanged`, `MouseDown`, `MouseUp`, `PreviewMouse*`, `ContextMenu`, or `KeyBinding` handler in code; none changes any ChartGuard or NinjaTrader state; none prints any `[EssencialCommand] ...` log line. *(Phase 2.2 — 2026-05-07)*
- [x] No keyboard shortcut on the chart or panel triggers any ChartGuard order action. The host does not register any global hotkey. *(Phase 2.2 — 2026-05-07)*
- [x] No chart click is intercepted by the host. Clicks/drags on the chart area continue to behave as native NinjaTrader expects. *(Phase 2.2 — 2026-05-07)*

### Draft model preview (Phase 2 contract)

- [x] Files under `AddOns/Panel/Models/` compile and contain only pure preview/configuration data. They do not reference NinjaTrader account/order types and do not create services. *(Phase 2.2 — 2026-05-07: NinjaScript compile OK, and a repo-wide grep at validation time confirmed no `using NinjaTrader.Cbi`/`NinjaTrader.Data`, no `Account.Submit`/`Account.CreateOrder`/`AtmStrategyCreate`/`EnableForControlledTest`, and no event-handler attachments under `AddOns/Panel/`.)*
- [ ] Calling `SetStrategyDraft(...)` updates the disabled Strategy combobox text and may propagate default Entry/Stop/Takes/Protection/Risk previews; it does not enable any control.
- [ ] Calling `SetEntryPlanDraft(...)` updates only disabled Entry fields (`Type`, `Qty`, `Sizing`, `Unit`, `Stop`, `Target`).
- [ ] Calling `SetTakeTargetsDraft(...)` updates the Takes text summary and the read-only `ACTIVE POSITION -> Targets` summary. Empty/null targets return to `(no targets defined)` / `-`.
- [ ] Calling `SetStopDraft(...)` updates the Stop `Current` row, Entry `Stop` preview, and read-only active stop summary.
- [ ] Calling `SetProtectionDraft(...)` updates only the read-only protection summary.
- [ ] Calling `SetRiskModeDraft(...)` updates the disabled Risk `Mode` combobox and risk preview rows.
- [ ] After every draft mutator call, all actionable controls remain `IsEnabled=false`, `Focusable=false`, `IsTabStop=false`; textboxes remain `IsReadOnly=true`.
- [ ] Safety grep remains clean for new panel code: no `Account.Submit`, no `Account.CreateOrder`, no `AtmStrategyCreate`, no `EnableForControlledTest`, and no newly attached `Click` / `SelectionChanged` / `TextChanged` / mouse / context-menu / keybinding handlers.

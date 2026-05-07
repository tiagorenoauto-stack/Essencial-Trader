# NinjaTrader Bridge Notes

This document describes the boundary between the Safe Core and the real NinjaTrader API.

## Status: experimental / blocked

The real submit path through `NinjaTraderAccountAdapter` is **disabled by default and blocked from Replay** until the entry-stop lifecycle is validated. `SubmitMarketEntryWithStop` returns `Fail` with reason `real bridge disabled pending entry-stop lifecycle validation` unless someone explicitly calls `EnableForControlledTest(...)` with the exact acknowledgement string defined in `NinjaTraderAccountAdapter.EnableAcknowledgement`.

The acknowledgement is verbose on purpose: enabling the bridge means accepting that we have not yet observed how the entry/stop pair behaves on real `OrderUpdate`/`ExecutionUpdate` events. Until that observation is done, do not run the bridge in Replay or anywhere else.

Concretely, before any Replay session:

1. Wire `OrderEventRouter` to a NinjaTrader event source (separate iteration), so we can observe `OrderUpdate`/`ExecutionUpdate` for both entry and stop. Or:
2. Replace the current entry+stop submission with a safer bracket/ATM mechanism whose lifecycle we can rely on.

Until either (1) or (2) lands and is checklisted, the bridge stays disabled.

## Where the real NinjaTrader API lives

All real NinjaTrader trading API calls live in `src/EssencialChartGuard/AddOns/NinjaTraderBridge/`. The current contents are:

- `NinjaTraderAccountAdapter.cs` — concrete `INinjaTraderAccountAdapter`. Calls `Account.CreateOrder(...)` and `Account.Submit(...)`, reads `Instrument.MarketData.Last`, uses `MasterInstrument.RoundToTickSize`. Disabled by default.
- `NinjaTraderAccountResolver.cs` — concrete `INinjaTraderAccountResolver`. Holds a single bound adapter and refuses any other scope.
- `INinjaTraderEventSource.cs` — port for a read-only event source (`Start` / `Stop` / `Dispose`).
- `NinjaTraderAccountEventBridge.cs` — concrete `INinjaTraderEventSource`. Subscribes to `Account.OrderUpdate` / `Account.ExecutionUpdate`, converts events to neutral records, and forwards them to the Safe Core's `OrderEventRouter`. **Never sends, modifies, or cancels orders.**
- `OutputTabSafeCoreLogger.cs` — `ISafeCoreLogger` that writes to the NinjaTrader Output Window (Tab 1 by default) using `[Essencial*]` prefixes. Lives here because it depends on `NinjaTrader.Code.Output`; the Safe Core stays free of NinjaTrader types.
- `NinjaTraderPositionSnapshotReader.cs` — read-only reader of `Account.Positions` for a single account+instrument, used by the probe to seed `ObservedAccountState` at attach time. Never sends, cancels, or modifies any order. Returns a neutral `ObservedPositionSnapshot`.

These are the **only** files in the project allowed to import `NinjaTrader.Cbi`, `NinjaTrader.Data` trading types, `OrderAction`, `OrderType`, `TimeInForce`, etc. If a future change needs another NinjaTrader API, it goes here, not into the Safe Core.

## SafeCore must not import NinjaTrader.Cbi

The Safe Core (`src/EssencialChartGuard/AddOns/SafeCore/`) only depends on the abstractions it defines: `IOrderSubmitter`, `INinjaTraderAccountAdapter`, `INinjaTraderAccountResolver`, plus its own commands/state types. It must never `using NinjaTrader.Cbi;` or reference real `Account`/`Instrument`/`Order` types. This keeps the core testable and the trading boundary auditable.

If a search across `src/EssencialChartGuard/AddOns/SafeCore/` finds a `NinjaTrader.Cbi` import, that is a regression and must be reverted.

## First real route — intended scope (currently blocked)

When unblocked, the bridge will support exactly one real action:

- `ProtectedEntry` with `EntryType == Market`, required stop, no target.
- Account must be Simulation or Playback (`Provider.Simulator` or `Provider.Replay`, with a defensive name-based fallback for connections containing "Sim" or "Playback").
- Account must be `Connected`.
- Reference price (Last) must be usable; otherwise the submitter Skips before the bridge is called.
- The adapter must have been explicitly enabled via `EnableForControlledTest(EnableAcknowledgement)` for this single Replay session.

Concretely, when enabled, the adapter:

1. Re-validates scope, quantity, stop usability, connection, and sim/playback flag.
2. Re-checks stop/side coherence against the current Last as a boundary safety.
3. Builds two detached `Order` objects via `Account.CreateOrder`: an entry `Market` and a protective `StopMarket`, both tagged with a shared OCO id and stable `Name` strings (`ECG-Entry-<id>`, `ECG-Stop-<id>`).
4. Sends them in a single `Account.Submit(IList<Order>)` call.
5. Logs `[EssencialOrder]` lines with `entryName`, `entryOrderId`, `stopName`, `stopOrderId`, `oco`.

If `CreateOrder` returns null or throws, **nothing is submitted**. If `Submit` throws, the result is `Fail`.

## Known risks

1. **OCO is not a bracket parent/child relationship.** We submit the entry and the stop in the same `Account.Submit` call and tag them with a shared `ocoId`. We do **not** assume the stop is parented to the entry. Two failure modes are plausible and must be observed in Replay before the bridge can be enabled outside a single controlled session:
   - The stop may be cancelled by OCO when the entry fills, leaving the resulting position with no protection. This would invert the safety guarantee and is a hard stop for the rollout.
   - If the entry is `Rejected`, the stop may remain working as an orphan in the book.
   The bridge does not yet listen to `OrderUpdate`/`ExecutionUpdate` to react to either case. Until that wiring exists, do not run this code path.
2. **Sim/playback classification heuristic.** `Provider.Simulator` and `Provider.Replay` are the primary signal. The connection-name fallback ("Sim", "Playback") exists only as a defensive measure; if you create an unusual connection name, the adapter may fail closed (treated as live, refused). That is the safe failure mode but worth being aware of.
3. **Reference price source.** The adapter only uses `Instrument.MarketData.Last`. If Last is not populated (e.g. instrument just subscribed, market closed), `GetReferencePriceForMarketEntry` returns null and the submitter Skips. This is intentional — we never substitute Bid/Ask/Close on the first real route.
4. **Recompile lifetime.** NinjaScript recompiles can leave old runtime instances alive. Whoever wires the adapter must ensure it is built once per active session and not cached across recompiles. Restarting NinjaTrader before each test session is the safest choice.
5. **No event handlers in the bridge yet.** The bridge submits orders but does not subscribe to `OrderUpdate`/`ExecutionUpdate`. That work is part of unblocking item (1) above: a separate `INinjaTraderEventSource` port must feed `OrderEventRouter` so the entry-stop lifecycle is observable from ChartGuard logs.

## Event bridge (read-only observation)

`NinjaTraderAccountEventBridge` is a separate, read-only path. It exists so we can observe real `OrderUpdate` and `ExecutionUpdate` events flowing through `OrderEventRouter` **without sending any order**. The submit path (`NinjaTraderAccountAdapter`) remains disabled.

What it does:

- Receives a real `Account` and an `OrderEventRouter` in the constructor, plus the target `accountName` (taken from the `Account`) and an optional `instrumentFullName` filter.
- On `Start()`, attaches handlers to `account.OrderUpdate` and `account.ExecutionUpdate`. Idempotent — repeated `Start` is a logged no-op.
- For each event, converts the NinjaTrader payload into the neutral `OrderEventRecord` / `ExecutionEventRecord` already consumed by the router. Mapped fields:
  - `OrderEventRecord`: `OrderId` (preferring `Order.OrderId`, fallback `Order.Id`), `AccountName` (`order.Account.Name`), `InstrumentFullName` (`order.Instrument.FullName`), `State` (`order.OrderState.ToString()`, fallback to `args.OrderState`), `ActionText` (`order.OrderAction.ToString()` — `Buy` / `Sell` / `SellShort` / `BuyToCover`), `TimeUtc` (`order.Time.ToUniversalTime()`).
  - `ExecutionEventRecord`: `ExecutionId` (`execution.ExecutionId`), `OrderId` (preferring `execution.Order.OrderId`), `AccountName`, `InstrumentFullName`, `Quantity` (`execution.Quantity`), `Price` (`execution.Price`), `ActionText` (`execution.MarketPosition.ToString()` — `Long` / `Short` / `Flat`, falling back to the owning `Order.OrderAction`), `TimeUtc` (`execution.Time.ToUniversalTime()`).
- Filters out events whose account does not match, and (when configured) whose instrument does not match. Mismatches are logged with `[EssencialOrder] EventBridge ... ignored: ... mismatch ...` and not routed.
- Routes via `OrderEventRouter.TryHandleOrderUpdate` / `TryHandleExecutionUpdate`. Logs `routed` for accepted events and `duplicate-or-rejected` for events the router de-duplicated.
- On `Stop()` or `Dispose()`, detaches both handlers. Both are idempotent and safe to call more than once. After `Dispose`, no NinjaTrader handler remains attached and `Start` will not re-attach. Handler exceptions never propagate back into NinjaTrader's event pipeline.
- Any event delivered after `Stop()` or `Dispose()` (NinjaTrader can dispatch an in-flight event after detach completes) is dropped at the top of the handler via a thread-safe `CanRouteEvents()` check. Such events log `[EssencialOrder] EventBridge <event> ignored: bridge not running` and never reach `OrderEventRouter`.

What it does **not** do:

- Does not call `Account.Submit`, `Account.CreateOrder`, `Cancel`, `Modify`, or any ATM API.
- Does not enable `NinjaTraderAccountAdapter`.
- Does not affect the order book in any way.

This event bridge is the prerequisite (1) listed in **Status: experimental / blocked**. It must be observed to behave correctly — handlers attached/detached cleanly, no duplicate routing, scope filter respected — before the submit path can be considered for enabling.

### Observed account state

`OrderEventRouter` accepts an optional `ObservedAccountState` (Safe Core, `SafeCore/State/`). When supplied, every routed event also feeds this approximate mirror of the account:

- `ApplyOrder(OrderEventRecord)` upserts an `ObservedOrderState` entry per `OrderId`, recording the latest `LastState` (`Working`, `Filled`, `Cancelled`, etc.) and `ActionText`.
- `ApplyExecution(ExecutionEventRecord)` records the last `ObservedExecutionState`, last execution price, and updates a signed `PositionQuantity` based on `ActionText`. The resulting `Position` is one of `Unknown` / `Flat` / `Long` / `Short`. If `ActionText` is empty or unrecognised, position transitions to `Unknown` (we never silently guess a sign).
- `WorkingOrdersCount` is derived live from order states.
- After each accepted order/execution, the router calls `state.TryLogSummaryIfChanged(logger)`, which emits `[EssencialOrder] ObservedState account=... instrument=... position=... qty=... lastPrice=... workingOrders=...` only when the summary actually changed.

This view is **observed/approximate**, not authoritative. The truth source remains NinjaTrader's `Account.Orders` and `Account.Positions`. A later iteration must reconcile this view with those — until then, any discrepancy must be assumed to favour the NinjaTrader side.

### Initial position snapshot

To avoid starting in `Unknown` when a probe is attached on top of an already-open position, the bridge ships `NinjaTraderPositionSnapshotReader`. It does a one-shot read against `Account.Positions` for a specific `(account, instrumentFullName)` and returns an `ObservedPositionSnapshot` (`PositionText` = `Long` / `Short` / `Flat`, absolute `Quantity`, optional `AveragePrice`). The probe then calls `ObservedAccountState.ApplyPositionSnapshot(...)` *before* starting the event bridge.

`Account.Positions` is treated as the authoritative source for this initial seed: if the snapshot says `Long qty=1`, the observed state is `Long qty=1` immediately, and the next routed `ExecutionUpdate` continues to adjust from that baseline. If no `Position` is found for the requested instrument, the snapshot is `Flat qty=0`. If the read throws or the probe is attached without an instrument scope, the probe logs an explicit notice and the state starts `Unknown` — never silently guessed.

The snapshot also seeds `ObservedAccountState`'s `account`/`instrument` identity so that the very first `ObservedState` summary reports the real account and instrument names (e.g. `account=Playback101 instrument=MNQ 06-26 position=Flat qty=0 ...`) rather than `?`, even when there is no execution and no working order yet. Routed `OrderUpdate` and `ExecutionUpdate` records keep refreshing those fields whenever they carry non-empty values.

The reader is read-only by construction: it iterates `Account.Positions` and reads `MarketPosition`, `Quantity`, and `AveragePrice` only. It does not call `Account.Submit`, `Account.CreateOrder`, `Cancel`, `Modify`, or any ATM API.

## Observation harness — `ChartGuardEventBridgeProbe`

`src/EssencialChartGuard/Indicators/ChartGuardEventBridgeProbe/ChartGuardEventBridgeProbe.cs` (with a colocated `README.md`) is a temporary observation indicator used to exercise the event bridge in Replay/Sim. It is the only ChartGuard piece on the chart during the **Event Bridge Observation Test** in `manual-test-checklist.md`. In the NinjaTrader Indicators dialog it appears as **"Essencial ChartGuard - Event Bridge Probe"**.

What it does:

- At `State.Configure`, builds an `OutputTabSafeCoreLogger` and an `OrderEventRouter`.
- At `State.DataLoaded`, resolves the configured `Account` against `Account.All`. The visible `Account` parameter defaults to `<Auto>`: in that mode the probe picks the first connected account that looks like Playback/Replay/Sim, falling back to the first connected account; otherwise it does an exact match against `Name` or `DisplayName`. If `FilterByChartInstrument` is true it takes the chart's `Instrument.FullName` as the instrument filter. It then constructs a `NinjaTraderAccountEventBridge` and calls `Start()`.
- At `State.Terminated`, calls `Stop()` and `Dispose()` on the bridge and clears references. Idempotent across recompiles/restarts thanks to the bridge's `CanRouteEvents()` gate.
- Logs lifecycle as `[EssencialUI] Probe account mode=...`, `Probe attaching ...`, `Probe selected account=... mode=... sim=...`, optional `Probe observing non-sim account read-only ...`, `Probe attached ...`, `Probe detached ...`. The bridge itself logs `[EssencialOrder] EventBridge subscribed/unsubscribed/...` lines.

What it does NOT do:

- No `TradeCommandService`. No `NinjaTraderOrderSubmitter`. No `NinjaTraderAccountAdapter`. No call to `EnableForControlledTest(...)`.
- No order is submitted, cancelled, or modified by ChartGuard while this probe is on the chart.
- No buttons, no hotkeys, no chart-trader controls. `OnBarUpdate` is a no-op.

How to use:

1. Confirm the Replay/Playback connection is running on a Sim/Playback account.
2. Open Output Window Tab 1 with `[Essencial*]` filters visible.
3. Right-click the chart → Indicators → add **Essencial ChartGuard - Event Bridge Probe**.
4. Set the `Account` parameter. The dropdown lists `<Auto>` first (default), followed by the connected accounts; pick `<Auto>` to auto-detect a Sim/Playback account, or pick a specific one. Manual values must match `Name` or `DisplayName` exactly.
5. Set `Filter events by chart instrument` to true to only see events for the chart's instrument; set false to observe everything on the account.
6. Click OK. You should see `[EssencialUI] Probe account mode=...`, `Probe selected account=...`, and `Probe attached account=... instrumentFilter=...` followed by `[EssencialOrder] EventBridge subscribed ...`. If the chosen account is not Sim/Playback, an extra `Probe observing non-sim account read-only ...` line precedes the attached line.
7. Create a manual order via Chart Trader / SuperDOM and observe the routed/duplicate-or-rejected log lines.
8. Remove the indicator (Indicators dialog → Remove) and confirm `[EssencialUI] Probe detached ...` and `[EssencialOrder] EventBridge unsubscribed ...`. New manual orders after this must produce no further `EventBridge` log lines.

The probe is meant only for this observation step. It should be removed from the chart once the Event Bridge Observation Test items are validated. It must not ship as part of any panel or workflow.

## Read-only side panel — `ChartGuardPanelHost` (Phase 1 layout)

`src/EssencialChartGuard/Indicators/ChartGuardPanelHost/ChartGuardPanelHost.cs` (with a colocated `README.md`) is the first user-facing ChartGuard piece. It is a NinjaScript indicator that hosts the side panel `EssencialChartGuardPanel` (under `src/EssencialChartGuard/AddOns/Panel/`) on the right edge of a chart. In the NinjaTrader Indicators dialog it appears as **"Essencial ChartGuard - Panel Host"**. The panel ships the **Phase 1 — Operational Read-Only Layout** described in `docs/chartguard-product-map.md`: the final-shape area structure rendered fully read-only.

What it does:

- At `State.Configure`, builds an `OutputTabSafeCoreLogger`, an `ObservedAccountState`, and an `OrderEventRouter` (same shape as the probe).
- At `State.DataLoaded`, resolves the configured `Account` (`<Auto>` or manual), reads an initial position snapshot via `NinjaTraderPositionSnapshotReader`, subscribes a `NinjaTraderAccountEventBridge`, and injects `EssencialChartGuardPanel` into a new right-side column of the chart's host `Grid`. A `Thumb`-based resize grip on the splitter column gives horizontal resize via `DragDelta`. Initial width ~370px, minimum ~280px, capped at ~640px. UI mutations run through `ChartControl.Dispatcher`. The host only injects into a layout-level `Grid` ancestor that already has at least two columns; otherwise it aborts and logs `PanelHost injection failed: no host Grid found` rather than touching an unsafe inner grid.
- A short `DispatcherTimer` (500ms) pulls `ObservedAccountState.BuildSummary()` as a cheap change detector and, when the summary changes, pushes a fresh `ObservedAccountSnapshotDto` into the panel. The panel's header summary line and the `STATUS` card both update from the same DTO.
- The header summary line owns `Position` / `Quantity` / `Avg price` / `Working orders` for fast operational reading. The `STATUS` card carries the same `Avg price` and `Working orders` plus a `Last update` timestamp — never duplicating fields above it.
- The `ENTRY PLAN`, `PROTECTION`, `SETTINGS` cards render **disabled placeholders** (Buy/Sell, BE, Lock 1R/2R/3R, Trail, Settings…). These buttons are kept `IsEnabled=false`, have no `Click` handler attached in code, and emit no log line. They communicate the final shape without enabling any action.
- The `RISK` and `SESSION` cards are read-only labels (`Daily limit` / `Status` / `Block` and `Trades` / `PnL` / `Time`) waiting for a real source.
- Section visibility is exposed by `EssencialChartGuardPanel.SetSectionsVisibility(EssencialChartGuardPanelSections)`, a small struct of `ShowEntryPlan` / `ShowProtection` / `ShowRisk` / `ShowSession` / `ShowSettings` flags. Settings persistence is intentionally not implemented in this phase.
- The header status dot uses an `Observer` mode label, qualified as `Observer / Playback`, `Observer / Sim`, or `Observer / non-sim` based on the resolved account/connection. The qualifier is informational; the panel is read-only regardless of account type.
- At `State.Terminated`, stops the timer, calls `Stop()` and `Dispose()` on the bridge, removes the panel and resize grip from the host grid, and removes the added columns by reference. Lifecycle is logged with `[EssencialUI] PanelHost ...` lines; observation lines remain `[EssencialOrder] EventBridge ...` and `[EssencialOrder] ObservedState ...` from the existing routes.

What it does NOT do:

- No `TradeCommandService`. No `NinjaTraderOrderSubmitter`. No `NinjaTraderAccountAdapter`. No call to `EnableForControlledTest(...)`.
- No order is submitted, cancelled, or modified by ChartGuard while this host is on the chart.
- No active buttons. No hotkeys. No chart-click handler. No `ContextMenu` override. No `MouseDown` / `MouseUp` / `PreviewMouse*` interception. No native NinjaTrader tool blocking. `OnBarUpdate` is a no-op.
- No persistence. No settings file.

`EssencialChartGuardPanel` and `EssencialChartGuardTheme` (the visual tokens) live in `src/EssencialChartGuard/AddOns/Panel/`. They are new code; no class, namespace, file, or visible string is inherited from CunhaTrader / CunhaScalper / Gold. The visual direction is inspired only by `docs/panel-visual-audit-cunha.md`.

The Safe Core remains free of NinjaTrader trading types. The host depends on one Safe Core helper introduced earlier: `ObservedAccountState.BuildSnapshotDto()` returning a value-typed `ObservedAccountSnapshotDto` (under `SafeCore/State/`) the panel can render directly.

## How to validate in Replay (after unblocking)

Replay only after the entry-stop lifecycle is validated — either by wiring `OrderEventRouter` to real NinjaTrader events, or by replacing the manual entry+stop submission with a safer bracket/ATM mechanism. Without that, the adapter must stay disabled.

When the prerequisite is in place:

1. Start NinjaTrader fresh. Open Output Window Tab 1 with the `[Essencial*]` filters visible.
2. Start a Replay/Playback connection on a Sim/Playback account; load the chart instrument the test will use.
3. Wire `NinjaTraderAccountAdapter` (with the live `Account` and `Instrument`) into a `NinjaTraderAccountResolver` and pass that resolver to `NinjaTraderOrderSubmitter`. The submitter goes into `TradeCommandService` in place of `DryRunOrderSubmitter` for the test only.
4. Call `EnableForControlledTest(NinjaTraderAccountAdapter.EnableAcknowledgement)` on the adapter. Confirm the `[EssencialOrder] Bridge enabled for controlled test ...` log line. If you do not see it, the bridge is still blocked and must not run.
5. Run the entries and assertions in `docs/manual-test-checklist.md`, sections **First Real Market Protected Entry Replay Test** and **NinjaTrader Bridge Replay Test**. The blocking row at the top of the bridge section must explicitly be cleared before proceeding.
6. After the session, call `Disable()` on the adapter and restart NinjaTrader before any next round.

The bridge is **not** considered production-ready until every relevant box is ticked manually for at least two consecutive Replay sessions and the entry-stop lifecycle behavior is documented as expected.

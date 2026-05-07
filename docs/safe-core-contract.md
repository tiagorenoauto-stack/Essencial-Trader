# Safe Core Contract

This document defines the behavior Essencial ChartGuard must obey before UI polish or advanced features are added.

## Architecture Contract

All critical paths must pass through these conceptual services:

- `TradeCommandService`: receives user commands and decides whether a trading action may be attempted.
- `ProtectionService`: owns stop/target decisions (computed price and reason) for protection actions.
- `IOrderSubmitter`: execution port. Receives the validated command/decision and either submits it through NinjaTrader or simulates execution. The Safe Core depends on the interface, never on a concrete submitter.
- `OrderEventRouter`: receives NinjaTrader order/execution events and updates internal state idempotently.
- `RiskGuard`: blocks or warns based on account risk rules.
- `ChartGuardPanel`: UI only. It creates commands, displays state, and never sends orders directly.
- `ChartGuardIndicator`: chart host/rendering only. It never sends orders directly.

### Execution port

`IOrderSubmitter` separates *deciding* from *executing*. `TradeCommandService` runs scope, payload, idempotency, and risk checks; if a command is accepted, the submitter is asked to perform `SubmitProtectedEntry`, `Flatten`, `CancelOrders`, or `ModifyStop`. The submitter returns a `SubmitResult` that `TradeCommandService` maps back to a `CommandResult`. `ProtectionService` does not call the submitter itself: it produces a `StopChangeDecision` (computed stop + reason) and `TradeCommandService` is the only caller of `IOrderSubmitter.ModifyStop`.

`SubmitResult.Outcome` has four values:

- `Submitted` — a real NinjaTrader-backed submitter actually placed/cancelled/modified an order. Mapped to `CommandResult.Accepted` with reason including `submitter=<name> mode=submitted`.
- `Simulated` — the core accepted the action but no NinjaTrader order was sent (e.g. `DryRunOrderSubmitter`). Mapped to `CommandResult.Accepted` with reason `simulated by <name>; no NinjaTrader order submitted`.
- `Skipped` — the submitter intentionally did not act. Mapped to `CommandResult.Blocked`.
- `Error` — the submitter could not act because of an internal failure. Mapped to `CommandResult.Error`.

The default implementation is `DryRunOrderSubmitter`. It logs the intended action under the auditable prefixes with the suffix `(dry run; no NinjaTrader order submitted)`, returns `Simulated`, and never calls any NinjaTrader API. A NinjaTrader-backed submitter must live behind the same interface, return `Submitted`, and only become the wired implementation after passing the manual checklist.

### First real submit route

`NinjaTraderOrderSubmitter` is the first real-submit implementation. **First real submit route supports only `ProtectedEntry` Market without target on simulation/playback accounts after manual Replay validation.** Concrete scope:

- `SubmitProtectedEntry` only when `EntryType == Market` with a required stop and no target. `Limit`, `StopMarket`, and `StopLimit` return `Skipped` with reason `entry type not implemented for real submit yet`.
- Target supplied (either `TargetPrice` or `TargetDistanceTicks`) returns `Skipped` with reason `target not implemented for first real submit route`. The entry is **not** sent.
- Stop is computed from `StopPrice` (used directly) or from `StopDistanceTicks` anchored to a reference price obtained from the account adapter. The reference price is captured once per call and reused for stop-from-ticks computation and stop/side coherence. If no reliable reference price is available, the submitter returns `Skipped` instead of guessing.
- The adapter must report `IsSimulationOrPlayback == true`; otherwise the submitter returns `Skipped` with reason `first real submit route is limited to simulation/playback`. Live accounts are not allowed on this route until a dedicated checklist exists.
- `Flatten`, `CancelOrders`, and `ModifyStop` (used by Breakeven and LockR) all return `Skipped` with reason `not implemented for real submit yet`.

The submitter never resolves accounts itself. It depends on `INinjaTraderAccountResolver`, which returns an `INinjaTraderAccountAdapter` already targeting the requested account+instrument. The adapter is the only place in the project that imports NinjaTrader trading types (`NinjaTrader.Cbi.*`, `Instrument`, `OrderAction`, etc.). If the resolver returns null or returns an adapter whose scope does not match the command, the submitter aborts (`Skipped`/`Error`); it never falls back silently to a different account or instrument.

## Initial Commands

### Protected Entry

Input:

- Account
- Instrument
- Side
- Quantity
- Entry type
- Optional entry price
- Stop distance or stop price
- Optional target distance or target price

Required behavior:

- Reject if account or instrument is missing.
- Reject if `ChartGuardState` already has an account or instrument and the command targets a different one (scope mismatch).
- Reject if quantity is less than 1.
- Reject if a protected-entry request has no stop.
- Reject if both `StopPrice` and `StopDistanceTicks` are supplied (ambiguous; precedence is not defined yet).
- Reject if both `TargetPrice` and `TargetDistanceTicks` are supplied (same reason).
- Reject if `StopDistanceTicks` or `TargetDistanceTicks` is supplied with a non-positive value.
- Reject if `EntryType` is `Limit` or `StopLimit` and no `EntryPrice` is supplied.
- Route through `TradeCommandService`.
- Log every decision.

### Flatten

Required behavior:

- Cancel live orders for the selected account/instrument.
- Close the open position for the selected account/instrument.
- Log requested quantity, detected position, and result.
- Never silently affect a different account or instrument.

### Breakeven

Required behavior:

- Require an open position.
- Require an active or known initial stop.
- Compute breakeven from entry price and side.
- Route stop change through `ProtectionService`.

### Lock 1R

Required behavior:

- Require an open position.
- Require known direction (long or short) on `ProtectionState`.
- Require known initial risk.
- Compute 1R from entry and initial stop, never from a moved stop.
- For long, target stop = entry + (initialRisk * R). For short, target stop = entry - (initialRisk * R).
- Route stop change through `ProtectionService`.

### Daily Loss Guard

Required behavior:

- Read account state.
- Block new entries when the configured loss threshold is reached.
- Allow flatten/protective actions even when entry is blocked.
- Log block reason clearly.

## Idempotency Rules

- `OrderEventRouter` must ignore duplicate execution events using a stable execution/order key when available.
- Protection orders must not be created twice for the same fill.
- Repeated button/menu/hotkey commands within a short window must not create duplicate submissions.
- `TradeCommandService` keeps a short-window fingerprint of the last accepted command per (command, account, instrument, side, qty, entry, stop, target). The default window is 750ms and is configurable via `SetIdempotencyWindow`. A repeat within the window is reported as `Blocked` with reason `duplicate within Nms`.
- The fingerprint is registered **only after** the submitter returns `Submitted` or `Simulated`. Rejected payloads, risk blocks, invalid protection decisions, and submitter errors/skips do not register a fingerprint, so a follow-up valid attempt is not blocked by a previous failed attempt within the window.

## Done Means

A feature is done only when:

- The code route obeys this contract.
- Manual NinjaTrader replay test passes.
- Logs are sufficient to reconstruct what happened.
- The UI does not imply unsupported behavior.

# Essencial ChartGuard

Essencial ChartGuard is a NinjaTrader 8 project focused on safe chart-based execution, protection, and risk control.

## Mission

Build a reliable replacement/extension for the native NinjaTrader Chart Trader workflow, with our own identity and safety rules.

The first version must be boring, safe, and testable before it is beautiful or feature-rich.

## Non-Negotiable Rules

1. No UI component sends orders directly.
2. No Indicator sends orders directly.
3. Every trading action starts as a command and passes through one command route.
4. Every order submit passes through `TradeCommandService`.
5. Every stop/target modification passes through `ProtectionService`.
6. Every `OrderUpdate` and `ExecutionUpdate` passes through `OrderEventRouter`.
7. Every critical action writes an auditable Output Tab log.
8. Every critical feature needs a manual NinjaTrader test checklist before it is considered done.
9. External products may be studied only by observable behavior. Do not copy, decompile, or reproduce protected code.
10. If two code paths perform the same trading action, one path must be removed or routed through the shared service.

## Scope Discipline

- Build the safe core first: account/instrument resolution, protected entry, stop management, flatten, breakeven, lock 1R, daily loss guard.
- Do not add advanced UI polish before the safe core passes replay tests.
- Do not add Smart Money, signals, bots, automation, or strategy logic to this repo until the execution/protection core is stable.
- Do not keep visible controls that do nothing.
- Do not call a feature complete until it is validated manually inside NinjaTrader.

## NinjaTrader Constraints

- Use C# syntax compatible with NinjaTrader 8.
- Indicators belong in `NinjaTrader.NinjaScript.Indicators`.
- AddOns belong in `NinjaTrader.NinjaScript.AddOns`.
- Strategies belong in `NinjaTrader.NinjaScript.Strategies`.
- WPF UI mutations must run through the UI dispatcher.
- Be careful with NinjaScript recompiles: old runtime instances and event handlers may remain alive until NinjaTrader is restarted.

## Logging

Use Output Tab 1 with stable prefixes:

- `[EssencialCommand]` for user commands and submit decisions.
- `[EssencialOrder]` for order/execution events.
- `[EssencialProtect]` for stop/target changes.
- `[EssencialRisk]` for risk guard decisions.
- `[EssencialUI]` for UI lifecycle and recoverable UI issues.

## Working Method

Before implementing a feature:

1. Define the behavior in `docs/safe-core-contract.md`.
2. Add or update the manual test in `docs/manual-test-checklist.md`.
3. Implement the smallest route that satisfies the behavior.
4. Verify in NinjaTrader replay before expanding the feature.

The day-to-day operational standard — start/end-of-task checks, Definition
of Done, commit pattern, sync rules, and safety greps — lives in
`docs/project-working-rules.md` and applies to every change in this repo.

## Project Layout

```text
src/EssencialChartGuard/
  AddOns/
  Indicators/
  Strategies/
docs/
  behavior-benchmark.md
  safe-core-contract.md
  manual-test-checklist.md
```

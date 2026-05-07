# Essencial ChartGuard

Essencial ChartGuard is the first NinjaTrader 8 tool under the Essencial Trader brand.

Its purpose is to provide a safer chart-based trading workflow inspired by the native NinjaTrader Chart Trader category, but with our own protection-first architecture.

## Initial Goal

Build a stable safe core before building a polished product:

- Resolve account and chart instrument reliably.
- Submit entries through one command route.
- Require/track protective stop behavior.
- Move stops to breakeven and lock 1R.
- Flatten safely.
- Enforce basic risk guard rules.
- Produce clear logs for every critical action.

## Current Status

Scaffold only. No trading code has been implemented yet.

## Important Documents

- `CLAUDE.md`: project rules and architecture constraints.
- `docs/behavior-benchmark.md`: observable behavior we want to study from other tools.
- `docs/safe-core-contract.md`: the contract the core must obey.
- `docs/manual-test-checklist.md`: manual NinjaTrader validation checklist.

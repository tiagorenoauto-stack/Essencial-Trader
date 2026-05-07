# Behavior Benchmark

This document maps observable behavior from tools such as NinjaTrader native Chart Trader, NTB_TradeSafe, and other trading utilities.

Do not copy code. Do not decompile. Do not reproduce protected internals. Only document visible behavior, user flows, and safety outcomes.

## Study Method

For each tool or workflow, record:

- What the user does.
- What the tool displays.
- What order/action is expected.
- What safety checks appear to happen.
- What happens when something is missing or invalid.
- What logs, warnings, confirmations, or blocked states are visible.

## Native NinjaTrader Chart Trader

To be mapped:

- Market buy/sell.
- Limit and stop order placement from the chart.
- Drag-to-modify order lines.
- Cancel individual order.
- Flatten behavior.
- ATM template selection.
- Behavior after partial fills.
- Behavior after reconnect/restart.

## NTB_TradeSafe

To be mapped by observable behavior only:

- Daily loss guard.
- Profit target guard.
- Auto stop behavior.
- Breakeven behavior.
- Trailing behavior.
- Flatten/panic behavior.
- Account/instrument selection.
- Hotkeys or command shortcuts.
- How it communicates blocked actions to the trader.

## Desired Essencial ChartGuard Direction

The goal is not to be identical. The goal is to match or exceed the basic safety behavior with our own architecture and visual identity.

When in doubt, prioritize:

1. No accidental orders.
2. No unprotected exposure without explicit warning.
3. One route for each critical action.
4. Clear visible feedback.
5. Clear Output Tab logs.

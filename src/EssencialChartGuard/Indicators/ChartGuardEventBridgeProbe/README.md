# Essencial ChartGuard — Event Bridge Probe

Read-only NinjaScript indicator used to observe real NinjaTrader account events flowing through the ChartGuard event bridge. **It is a temporary tool, used only during the Event Bridge Observation Test.** It does not ship as part of any user workflow.

## What this probe is

- A NinjaScript indicator (class `ChartGuardEventBridgeProbe`) that, when added to a chart, builds an `OutputTabSafeCoreLogger`, an `OrderEventRouter`, and a single `NinjaTraderAccountEventBridge` bound to the configured account and (optionally) the chart instrument.
- It calls `Start()` on the bridge at `State.DataLoaded` and `Stop()` + `Dispose()` at `State.Terminated`.
- It writes lifecycle and event lines to the NinjaTrader Output Window (Tab 1) under the `[EssencialUI]` and `[EssencialOrder]` prefixes.

In the NinjaTrader Indicators dialog the probe shows up as **"Essencial ChartGuard - Event Bridge Probe"**.

## What this probe is **not**

- It does **not** send, cancel, or modify orders.
- It does **not** create `TradeCommandService`, `NinjaTraderOrderSubmitter`, or `NinjaTraderAccountAdapter`.
- It does **not** call `EnableForControlledTest(...)`. The submit-side bridge stays disabled by default.
- It has no buttons, no hotkeys, no chart-trader controls. `OnBarUpdate` is a no-op.

If you ever see this probe send/cancel/modify any order, that is a bug — stop the session and report it.

## How to compile in the NinjaScript Editor

1. Open NinjaTrader 8.
2. Open `New → NinjaScript Editor`.
3. Make sure the following bridge files already exist under your NinjaTrader user folder, mirroring the project structure (these are normally added together when you import the bridge):
   - `AddOns/EssencialChartGuard/SafeCore/Services/OrderEventRouter.cs` (and the rest of `SafeCore/`).
   - `AddOns/EssencialChartGuard/NinjaTraderBridge/INinjaTraderEventSource.cs`.
   - `AddOns/EssencialChartGuard/NinjaTraderBridge/NinjaTraderAccountEventBridge.cs`.
   - `AddOns/EssencialChartGuard/NinjaTraderBridge/OutputTabSafeCoreLogger.cs`.
4. Right-click `Indicators` → `New Indicator…` (or use `File → New → Indicator`) to create a placeholder; cancel the wizard, then drag/copy `ChartGuardEventBridgeProbe.cs` from this folder into your NinjaTrader user folder under `bin/Custom/Indicators/`.
5. Press `F5` (Compile). All ChartGuard files (SafeCore + bridge + this probe) compile together. There must be **0 errors**. Warnings about unused fields or missing XML docs are acceptable.
6. The probe now appears in the Indicators dialog as `Essencial ChartGuard - Event Bridge Probe`.

If compilation fails, the probe will not be available in the Indicators dialog — fix the underlying bridge or SafeCore errors first, then recompile.

## How to add the probe to a chart

1. Start a Replay or Sim/Playback connection on the account you want to observe (e.g. `Sim101`, `Playback101`).
2. Open the chart for the instrument you want to observe.
3. Open the Output Window (View → Output Window) and select **Tab 1**. Filter by `[Essencial` to keep only ChartGuard log lines visible.
4. Right-click the chart → `Indicators…`.
5. From the indicator list select **`Essencial ChartGuard - Event Bridge Probe`** and click `New`.
6. Set parameters in the right panel:
   - **Account**: the dropdown lists `<Auto>` first (the default), followed by the connected accounts by `DisplayName` and `Name`. Pick `<Auto>` to let the probe auto-detect the account, or pick/type a specific account to bind to it.
     - `<Auto>` mode resolves in this order: (a) first connected account that looks like Playback/Replay/Sim by provider name or account name; (b) otherwise, the first connected account; (c) otherwise the probe aborts and logs an attach abort.
     - Manual mode requires an exact match against `Account.Name` or `Account.DisplayName`. No fuzzy matching, no fallback to a different account.
     - Blank/whitespace is treated as `<Auto>` for backwards compatibility.
   - **Filter events by chart instrument**: leave **true** (default) to only observe events on the chart's instrument. Set **false** to observe every event on the account.
7. Click `OK`.

When the probe attaches you should see, in addition to the standard subscribed lines:

```
[EssencialUI] Probe account mode=<Auto|manual> requested=<value>
[EssencialUI] Probe selected account=<Name (DisplayName)> mode=<Auto|manual> sim=<true|false>
```

If the auto-detected (or manually selected) account is not Sim/Playback, the probe also logs:

```
[EssencialUI] Probe observing non-sim account read-only account=<Name (DisplayName)>
```

This line is informational only — the probe is read-only regardless of account type. It exists so you cannot miss that you are watching a live account.

### Removing the probe

When the observation test is finished, remove the probe:

1. Right-click the chart → `Indicators…`.
2. Select **`Essencial ChartGuard - Event Bridge Probe`** in the right panel.
3. Click `Remove`, then `OK`.

This triggers `State.Terminated`, the bridge unsubscribes its handlers, and the probe disposes its references. After this, no further `EventBridge` lines should appear for new manual orders.

## Logs to expect

Lifecycle (the probe itself):

```
[EssencialUI] Probe account mode=<<Auto>|manual> requested=<<Auto>|<account>>
[EssencialUI] Probe attaching account=<<Auto>|<account>> chartInstrument=<instrument> filterByChartInstrument=<true|false>
[EssencialUI] Probe selected account=<Name (DisplayName)> mode=<<Auto>|manual> sim=<true|false>
[EssencialUI] Probe observing non-sim account read-only account=<Name (DisplayName)>     # only if sim=false
[EssencialUI] Probe attached account=<Name (DisplayName)> instrumentFilter=<*|<instrument>>
...
[EssencialUI] Probe detached account=<<Auto>|<account>>
```

Bridge subscribe/unsubscribe (raised by `NinjaTraderAccountEventBridge`):

```
[EssencialOrder] EventBridge subscribed account=<account> instrumentFilter=<*|<instrument>>
[EssencialOrder] EventBridge unsubscribed account=<account>
```

Order lifecycle (when you create/fill/cancel a manual order via Chart Trader or SuperDOM):

```
[EssencialOrder] EventBridge OrderUpdate received account=<account> instrument=<inst> orderId=<id> state=<state>
[EssencialOrder] EventBridge OrderUpdate routed orderId=<id> state=<state>
[EssencialOrder] EventBridge ExecutionUpdate received account=<account> instrument=<inst> executionId=<exec> orderId=<id> qty=<n> price=<p>
[EssencialOrder] EventBridge ExecutionUpdate routed executionId=<exec> orderId=<id>
```

Filter / lifecycle drops (acceptable, expected when the probe is detaching or events are out of scope):

```
[EssencialOrder] EventBridge OrderUpdate ignored: account mismatch event=<other> bridge=<account> id=<id>
[EssencialOrder] EventBridge OrderUpdate ignored: instrument mismatch event=<other> bridge=<inst> id=<id>
[EssencialOrder] EventBridge OrderUpdate ignored: bridge not running
```

What you should **never** see while the probe is on the chart:

```
[EssencialOrder] Bridge submitted ...
[EssencialCommand] DryRun SubmitProtectedEntry ...
```

Either of those would indicate that something is sending orders — which the probe is explicitly designed not to do.

## Status and scope reminder

The probe is the prerequisite step for unblocking `NinjaTraderAccountAdapter`. See `docs/ninjatrader-bridge-notes.md` for the full status (experimental / blocked) and `docs/manual-test-checklist.md` for the **Event Bridge Observation Test** items this probe is meant to satisfy. Do not ship the probe as part of any panel or workflow; remove it from the chart after the observation test is complete.

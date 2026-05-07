using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge
{
    // Port for an event source that translates real NinjaTrader OrderUpdate/ExecutionUpdate
    // events into neutral records and pushes them into the Safe Core's OrderEventRouter.
    //
    // This interface lives in the bridge namespace (not Safe Core) because subscribing to
    // real Cbi.Account events requires NinjaTrader.Cbi types -- which the Safe Core must
    // never reference. The Safe Core only consumes the resulting OrderEventRecord /
    // ExecutionEventRecord through OrderEventRouter.
    //
    // Implementations must be safe under recompiles and restarts: Start/Stop idempotent,
    // Dispose detaches all handlers, no handler may survive past Dispose.
    public interface INinjaTraderEventSource : IDisposable
    {
        bool IsRunning { get; }

        // Subscribe to the underlying event source. Idempotent: calling Start when already
        // running must not double-subscribe, must not throw, and must log the no-op.
        void Start();

        // Unsubscribe from the underlying event source. Idempotent: calling Stop when not
        // running must be a safe no-op. After Stop, no further events may be routed.
        void Stop();
    }
}

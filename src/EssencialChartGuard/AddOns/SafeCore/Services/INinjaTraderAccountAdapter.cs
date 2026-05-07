using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    // Small isolation layer between Safe Core and the real NinjaTrader API. The Safe Core
    // only depends on this interface; the concrete implementation that calls
    // NinjaTrader.Cbi.Account / Instrument / OrderAction lives outside the core and is the
    // only place that imports NinjaTrader trading types. This keeps the core testable and
    // prevents NinjaTrader types from leaking into commands, services, or state.
    public interface INinjaTraderAccountAdapter
    {
        // Resolved scope. Must match the command being executed; the submitter rejects on mismatch.
        string AccountName { get; }
        string InstrumentFullName { get; }

        bool IsConnected { get; }
        bool IsSimulationOrPlayback { get; }

        // Tick metadata for converting StopDistanceTicks to a stop price.
        double TickSize { get; }

        // Reference price used to anchor a tick-based stop for a Market entry.
        // Implementations should return the most recent reliable last price for the
        // resolved instrument. Return null when no reliable price is available; the
        // submitter must then return Skipped instead of guessing.
        double? GetReferencePriceForMarketEntry();

        // Round a price to the instrument tick.
        double RoundToTick(double price);

        // Submit a real market entry with an attached protective stop.
        // Implementations must place exactly one entry and exactly one stop, scoped to the
        // adapter's account+instrument. They must never affect another account or instrument.
        // The returned referenceId must be stable enough to correlate with subsequent
        // OrderUpdate/ExecutionUpdate events through the OrderEventRouter.
        SubmitMarketEntryResult SubmitMarketEntryWithStop(MarketEntryRequest request);
    }

    // Resolver port. A bootstrap layer (AddOn host) builds the adapter for the active
    // account+instrument and hands it to NinjaTraderOrderSubmitter. The Safe Core never
    // resolves accounts itself.
    public interface INinjaTraderAccountResolver
    {
        // Returns null when the account or instrument cannot be resolved safely.
        // Must never return an adapter that targets a different account/instrument than requested.
        INinjaTraderAccountAdapter Resolve(string accountName, string instrumentFullName);
    }

    public sealed class MarketEntryRequest
    {
        public string AccountName { get; private set; }
        public string InstrumentFullName { get; private set; }
        public bool IsBuy { get; private set; }
        public int Quantity { get; private set; }
        public double StopPrice { get; private set; }
        public DateTime CreatedUtc { get; private set; }

        public MarketEntryRequest(
            string accountName,
            string instrumentFullName,
            bool isBuy,
            int quantity,
            double stopPrice)
        {
            AccountName = accountName ?? string.Empty;
            InstrumentFullName = instrumentFullName ?? string.Empty;
            IsBuy = isBuy;
            Quantity = quantity;
            StopPrice = stopPrice;
            CreatedUtc = DateTime.UtcNow;
        }
    }

    public sealed class SubmitMarketEntryResult
    {
        public bool Success { get; private set; }
        public string ReferenceId { get; private set; }
        public string ErrorReason { get; private set; }

        private SubmitMarketEntryResult(bool success, string referenceId, string errorReason)
        {
            Success = success;
            ReferenceId = referenceId ?? string.Empty;
            ErrorReason = errorReason ?? string.Empty;
        }

        public static SubmitMarketEntryResult Ok(string referenceId)
        {
            return new SubmitMarketEntryResult(true, referenceId, null);
        }

        public static SubmitMarketEntryResult Fail(string errorReason)
        {
            return new SubmitMarketEntryResult(false, null, errorReason);
        }
    }
}

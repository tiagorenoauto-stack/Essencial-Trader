using System;
using System.Globalization;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge
{
    // Read-only reader of NinjaTrader's Account.Positions for a single account+instrument.
    // Used by the probe to seed ObservedAccountState at attach time. This class never sends,
    // cancels, or modifies any order. It does not subscribe to events. It only reads the
    // current Position from Account.Positions and converts it into a neutral
    // ObservedPositionSnapshot the Safe Core can consume.
    //
    // Authoritative source: NinjaTrader's Account.Positions. This snapshot is the truth at
    // the moment it is read; subsequent changes are observed via NinjaTraderAccountEventBridge.
    public sealed class NinjaTraderPositionSnapshotReader
    {
        private readonly ISafeCoreLogger logger;
        private readonly Account account;
        private readonly string instrumentFullName;

        public NinjaTraderPositionSnapshotReader(
            ISafeCoreLogger logger,
            Account account,
            string instrumentFullName)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            if (account == null) throw new ArgumentNullException("account");
            if (string.IsNullOrWhiteSpace(instrumentFullName))
                throw new ArgumentException("instrumentFullName is required", "instrumentFullName");

            this.logger = logger;
            this.account = account;
            this.instrumentFullName = instrumentFullName;
        }

        // Returns the current snapshot for the configured instrument. Returns null only if
        // an unexpected exception happens while accessing NinjaTrader; callers must treat
        // null as "snapshot unavailable, leave state Unknown" and continue without a seed.
        // When no Position is found for the instrument, returns Flat qty=0 (the canonical
        // representation of "not in this instrument").
        public ObservedPositionSnapshot ReadSnapshot()
        {
            string accountName = account.Name ?? string.Empty;

            try
            {
                Position match = null;
                int positionsScanned = 0;

                foreach (Position p in account.Positions)
                {
                    positionsScanned++;
                    if (p == null || p.Instrument == null) continue;

                    string fullName = p.Instrument.FullName ?? string.Empty;
                    if (string.Equals(fullName, instrumentFullName, StringComparison.Ordinal))
                    {
                        match = p;
                        break;
                    }
                    else
                    {
                        logger.Order(
                            "PositionSnapshot ignored: instrument mismatch event=" + fullName +
                            " requested=" + instrumentFullName);
                    }
                }

                if (match == null)
                {
                    logger.Order(
                        "PositionSnapshot read account=" + accountName +
                        " instrument=" + instrumentFullName +
                        " positionsScanned=" + positionsScanned.ToString(CultureInfo.InvariantCulture) +
                        " result=Flat qty=0 (no matching position)");
                    return ObservedPositionSnapshot.Flat(accountName, instrumentFullName);
                }

                string positionText;
                int quantity;
                double? averagePrice;
                ExtractFields(match, out positionText, out quantity, out averagePrice);

                if (quantity == 0 || IsFlat(positionText))
                {
                    logger.Order(
                        "PositionSnapshot flat account=" + accountName +
                        " instrument=" + instrumentFullName +
                        " marketPosition=" + (string.IsNullOrEmpty(positionText) ? "?" : positionText) +
                        " qty=0");
                    return ObservedPositionSnapshot.Flat(accountName, instrumentFullName);
                }

                logger.Order(
                    "PositionSnapshot " + positionText.ToLowerInvariant() +
                    " account=" + accountName +
                    " instrument=" + instrumentFullName +
                    " qty=" + quantity.ToString(CultureInfo.InvariantCulture) +
                    " avg=" + Format(averagePrice));

                return new ObservedPositionSnapshot(
                    accountName,
                    instrumentFullName,
                    positionText,
                    quantity,
                    averagePrice,
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                logger.Order(
                    "PositionSnapshot read error account=" + accountName +
                    " instrument=" + instrumentFullName +
                    " ex=" + ex.GetType().Name + ":" + ex.Message);
                return null;
            }
        }

        private static void ExtractFields(
            Position p,
            out string positionText,
            out int quantity,
            out double? averagePrice)
        {
            positionText = string.Empty;
            quantity = 0;
            averagePrice = null;

            try { positionText = p.MarketPosition.ToString(); }
            catch { positionText = string.Empty; }

            try { quantity = p.Quantity; }
            catch { quantity = 0; }
            if (quantity < 0) quantity = -quantity; // NT may return signed; we store absolute.

            try
            {
                double avg = p.AveragePrice;
                if (!double.IsNaN(avg) && !double.IsInfinity(avg) && avg > 0.0)
                    averagePrice = avg;
            }
            catch
            {
                averagePrice = null;
            }
        }

        private static bool IsFlat(string text)
        {
            return string.Equals(text, "Flat", StringComparison.OrdinalIgnoreCase);
        }

        private static string Format(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.#####", CultureInfo.InvariantCulture) : "-";
        }
    }
}

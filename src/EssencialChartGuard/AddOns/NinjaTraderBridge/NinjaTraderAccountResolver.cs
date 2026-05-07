using System;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge
{
    // Single-scope resolver. The bootstrap layer (e.g. an AddOn host) builds one adapter
    // bound to the active account+instrument and hands it to this resolver. Resolve returns
    // the adapter only when the requested scope matches exactly; otherwise it returns null.
    //
    // The resolver never substitutes accounts or instruments. There is no fallback. There is
    // no fuzzy matching. There is no auto-resolution from a list of accounts. If the requested
    // scope does not match the bound adapter, the upstream submitter must Skip the action.
    public sealed class NinjaTraderAccountResolver : INinjaTraderAccountResolver
    {
        private readonly ISafeCoreLogger logger;
        private readonly INinjaTraderAccountAdapter activeAdapter;

        public NinjaTraderAccountResolver(ISafeCoreLogger logger, INinjaTraderAccountAdapter activeAdapter)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            if (activeAdapter == null) throw new ArgumentNullException("activeAdapter");
            this.logger = logger;
            this.activeAdapter = activeAdapter;
        }

        public string ActiveAccountName
        {
            get { return activeAdapter.AccountName; }
        }

        public string ActiveInstrumentFullName
        {
            get { return activeAdapter.InstrumentFullName; }
        }

        public INinjaTraderAccountAdapter Resolve(string accountName, string instrumentFullName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                logger.Command("Resolver refused: account is missing");
                return null;
            }
            if (string.IsNullOrWhiteSpace(instrumentFullName))
            {
                logger.Command("Resolver refused: instrument is missing");
                return null;
            }

            if (!string.Equals(activeAdapter.AccountName, accountName, StringComparison.Ordinal))
            {
                logger.Command(
                    "Resolver refused: account mismatch requested=" + accountName +
                    " active=" + activeAdapter.AccountName);
                return null;
            }
            if (!string.Equals(activeAdapter.InstrumentFullName, instrumentFullName, StringComparison.Ordinal))
            {
                logger.Command(
                    "Resolver refused: instrument mismatch requested=" + instrumentFullName +
                    " active=" + activeAdapter.InstrumentFullName);
                return null;
            }

            return activeAdapter;
        }
    }
}

using System;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    public sealed class RiskGuard
    {
        private readonly ISafeCoreLogger logger;

        private double dailyRealizedPnL;
        private double dailyLossThreshold;
        private bool manualBlock;
        private string manualBlockReason;

        public RiskGuard(ISafeCoreLogger logger)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            this.logger = logger;
            this.dailyRealizedPnL = 0.0;
            this.dailyLossThreshold = -1.0;
            this.manualBlock = false;
        }

        public double DailyRealizedPnL
        {
            get { return dailyRealizedPnL; }
        }

        public double DailyLossThreshold
        {
            get { return dailyLossThreshold; }
        }

        public void SetDailyLossThreshold(double thresholdAbsoluteCurrency)
        {
            if (thresholdAbsoluteCurrency < 0)
                throw new ArgumentException("Threshold must be non-negative.", "thresholdAbsoluteCurrency");

            dailyLossThreshold = thresholdAbsoluteCurrency;
            logger.Risk("DailyLossThreshold set to " + thresholdAbsoluteCurrency.ToString("0.##"));
        }

        public void UpdateDailyRealizedPnL(double pnl)
        {
            dailyRealizedPnL = pnl;
        }

        public void ManualBlock(string reason)
        {
            manualBlock = true;
            manualBlockReason = reason ?? "manual block";
            logger.Risk("ManualBlock=ON reason=" + manualBlockReason);
        }

        public void ManualUnblock()
        {
            if (manualBlock)
                logger.Risk("ManualBlock=OFF");
            manualBlock = false;
            manualBlockReason = null;
        }

        public RiskDecision EvaluateNewEntry(string accountName)
        {
            if (manualBlock)
                return RiskDecision.Block("manual block: " + (manualBlockReason ?? string.Empty));

            if (dailyLossThreshold > 0 && dailyRealizedPnL <= -dailyLossThreshold)
                return RiskDecision.Block(
                    "daily loss guard: pnl=" + dailyRealizedPnL.ToString("0.##") +
                    " threshold=" + dailyLossThreshold.ToString("0.##"));

            return RiskDecision.Allow();
        }

        public bool AllowsProtectiveActions
        {
            get { return true; }
        }
    }
}

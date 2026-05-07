using System;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.State
{
    public enum ProtectionStage
    {
        None,
        Initial,
        Breakeven,
        Locked1R
    }

    public enum PositionDirection
    {
        None,
        Long,
        Short
    }

    public sealed class ProtectionState
    {
        public ProtectionStage Stage { get; set; }
        public PositionDirection Direction { get; set; }
        public double? EntryPrice { get; set; }
        public double? InitialStopPrice { get; set; }
        public double? CurrentStopPrice { get; set; }
        public double? TargetPrice { get; set; }
        public DateTime LastUpdateUtc { get; set; }

        public ProtectionState()
        {
            Stage = ProtectionStage.None;
            Direction = PositionDirection.None;
            LastUpdateUtc = DateTime.UtcNow;
        }

        public bool HasOpenPosition
        {
            get
            {
                return Stage != ProtectionStage.None
                    && Direction != PositionDirection.None
                    && EntryPrice.HasValue;
            }
        }

        public bool HasKnownInitialRisk
        {
            get
            {
                if (!EntryPrice.HasValue || !InitialStopPrice.HasValue) return false;
                if (Direction == PositionDirection.Long)
                    return InitialStopPrice.Value < EntryPrice.Value;
                if (Direction == PositionDirection.Short)
                    return InitialStopPrice.Value > EntryPrice.Value;
                return false;
            }
        }

        public double? InitialRiskPerUnit
        {
            get
            {
                if (!HasKnownInitialRisk) return null;
                if (Direction == PositionDirection.Long)
                    return EntryPrice.Value - InitialStopPrice.Value;
                if (Direction == PositionDirection.Short)
                    return InitialStopPrice.Value - EntryPrice.Value;
                return null;
            }
        }
    }
}

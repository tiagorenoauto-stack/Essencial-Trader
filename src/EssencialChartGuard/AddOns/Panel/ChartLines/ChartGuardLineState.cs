namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.ChartLines
{
    // Pure value-typed state for the read-only chart line renderer.
    //
    // Strict contract:
    //   * No NinjaTrader.Cbi / NinjaTrader.Data references. Only primitives
    //     and strings live here. The renderer is the only place that knows
    //     how to draw on the chart canvas.
    //   * Each "Show*" flag controls visibility independently from its
    //     companion price. A Show* flag of false means "do not draw, even
    //     if a price was filled" -- the renderer must respect that.
    //   * Active stop / active targets fields are reserved for a future
    //     observed protection snapshot source. They stay zero/false in the
    //     Phase 3.1 implementation; the renderer treats them like any
    //     other draft pair.
    //
    // The host owns the conversion from observed state + draft into this
    // struct. The renderer never reads observed state or drafts directly.
    public struct ChartGuardLineState
    {
        // Entry / Avg of the open observed position.
        public bool ShowEntryAvg;
        public double EntryAvgPrice;
        public string EntryAvgLabel;

        // Last observed execution price.
        public bool ShowLastFill;
        public double LastFillPrice;
        public string LastFillLabel;

        // Draft stop derived from a StopDraft + a safe reference price.
        public bool ShowDraftStop;
        public double DraftStopPrice;
        public string DraftStopLabel;

        // Draft target T1 derived from TakeTargetDraft[0].
        public bool ShowDraftT1;
        public double DraftT1Price;
        public string DraftT1Label;

        // Draft target T2 derived from TakeTargetDraft[1].
        public bool ShowDraftT2;
        public double DraftT2Price;
        public string DraftT2Label;

        // Reserved for a future read-only active-protection snapshot.
        public bool ShowActiveStop;
        public double ActiveStopPrice;
        public string ActiveStopLabel;

        public bool ShowActiveT1;
        public double ActiveT1Price;
        public string ActiveT1Label;

        public bool ShowActiveT2;
        public double ActiveT2Price;
        public string ActiveT2Label;

        // Returns an "all hidden" state -- handy default for callers that
        // want to clear the chart while keeping the renderer attached.
        public static ChartGuardLineState Empty()
        {
            return new ChartGuardLineState();
        }
    }
}

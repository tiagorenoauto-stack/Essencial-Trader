using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel.ChartLines;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Indicators.EssencialChartGuard
{
    // Read-only chart line renderer for ChartGuardPanelHost. Translates a
    // ChartGuardLineState into NinjaScript Draw.HorizontalLine + Draw.Text
    // objects on the host indicator's chart panel.
    //
    // Strict contract (mirrors docs/chart-lines-readonly-phase3.md):
    //   * Lines are informative only. The renderer never subscribes to
    //     mouse, keyboard, drag/drop, or context-menu events.
    //   * The renderer never modifies, cancels, or submits any order. It
    //     never touches Account.Submit / Account.CreateOrder /
    //     AtmStrategyCreate / EnableForControlledTest.
    //   * Lines are not draggable: every Draw call sets isAutoScale=false
    //     and drawOnPricePanel=true with a stable tag, so updates replace
    //     by tag instead of stacking. NinjaScript's horizontal-line draw
    //     object is not given a movable handle by the renderer; the user
    //     locking/unlocking the global "Lock drawings" toggle is the only
    //     way it can ever be moved (we cannot prevent that, but we don't
    //     create movable affordances ourselves).
    //   * Cleanup uses the host's RemoveDrawObject(tag) by stable tag so
    //     we only remove artifacts we created.
    //
    // The renderer is created and owned by ChartGuardPanelHost. It runs on
    // whatever thread the host calls ApplyState from; NinjaScript's Draw
    // helpers are documented as safe to call from the indicator thread.
    public sealed class ChartGuardReadOnlyLineRenderer
    {
        private const string TagPrefix = "ECG-ReadOnlyLine-";
        private const string TagEntryAvg = TagPrefix + "EntryAvg";
        private const string TagEntryAvgLabel = TagPrefix + "EntryAvg-Label";
        private const string TagLastFill = TagPrefix + "LastFill";
        private const string TagLastFillLabel = TagPrefix + "LastFill-Label";
        private const string TagDraftStop = TagPrefix + "DraftStop";
        private const string TagDraftStopLabel = TagPrefix + "DraftStop-Label";
        private const string TagDraftT1 = TagPrefix + "DraftT1";
        private const string TagDraftT1Label = TagPrefix + "DraftT1-Label";
        private const string TagDraftT2 = TagPrefix + "DraftT2";
        private const string TagDraftT2Label = TagPrefix + "DraftT2-Label";
        private const string TagActiveStop = TagPrefix + "ActiveStop";
        private const string TagActiveStopLabel = TagPrefix + "ActiveStop-Label";
        private const string TagActiveT1 = TagPrefix + "ActiveT1";
        private const string TagActiveT1Label = TagPrefix + "ActiveT1-Label";
        private const string TagActiveT2 = TagPrefix + "ActiveT2";
        private const string TagActiveT2Label = TagPrefix + "ActiveT2-Label";

        private static readonly string[] AllTags =
        {
            TagEntryAvg, TagEntryAvgLabel,
            TagLastFill, TagLastFillLabel,
            TagDraftStop, TagDraftStopLabel,
            TagDraftT1, TagDraftT1Label,
            TagDraftT2, TagDraftT2Label,
            TagActiveStop, TagActiveStopLabel,
            TagActiveT1, TagActiveT1Label,
            TagActiveT2, TagActiveT2Label
        };

        private readonly Indicator host;
        private readonly ISafeCoreLogger logger;
        private readonly Dictionary<string, bool> activeTags;
        private ChartGuardLineState lastApplied;
        private bool hasLastApplied;

        // Frozen brushes so the renderer never reallocates per ApplyState.
        private static readonly Brush BrushEntryAvg = Freeze(FromHex("#D4A24C"));   // gold
        private static readonly Brush BrushLastFill = Freeze(FromHex("#9099A8"));   // muted gray
        private static readonly Brush BrushDraftStop = Freeze(FromHex("#D14B4B"));  // red
        private static readonly Brush BrushDraftTarget = Freeze(FromHex("#3FB35E"));// green
        private static readonly Brush BrushActiveStop = Freeze(FromHex("#D14B4B"));
        private static readonly Brush BrushActiveTarget = Freeze(FromHex("#3FB35E"));

        // Label background: dark with ~70% opacity so the small ECG label
        // stays legible over either green or red candles. Outline matches
        // the panel's subtle border color so the chip blends with the panel.
        private static readonly Brush BrushLabelFill = Freeze(FromHex("#B30E0F12"));
        private static readonly Brush BrushLabelOutline = Freeze(FromHex("#262A33"));

        public ChartGuardReadOnlyLineRenderer(Indicator host, ISafeCoreLogger logger)
        {
            if (host == null) throw new ArgumentNullException("host");
            this.host = host;
            this.logger = logger;
            this.activeTags = new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        // Renders the requested state. Idempotent per tag: the same Draw
        // call with the same tag updates the existing object rather than
        // duplicating it. Tags whose Show* flag turned off get removed.
        public void ApplyState(ChartGuardLineState state)
        {
            try
            {
                ApplyLine(TagEntryAvg, TagEntryAvgLabel, state.ShowEntryAvg,
                    state.EntryAvgPrice, state.EntryAvgLabel, BrushEntryAvg, dashed: false);
                ApplyLine(TagLastFill, TagLastFillLabel, state.ShowLastFill,
                    state.LastFillPrice, state.LastFillLabel, BrushLastFill, dashed: true);
                ApplyLine(TagDraftStop, TagDraftStopLabel, state.ShowDraftStop,
                    state.DraftStopPrice, state.DraftStopLabel, BrushDraftStop, dashed: true);
                ApplyLine(TagDraftT1, TagDraftT1Label, state.ShowDraftT1,
                    state.DraftT1Price, state.DraftT1Label, BrushDraftTarget, dashed: true);
                ApplyLine(TagDraftT2, TagDraftT2Label, state.ShowDraftT2,
                    state.DraftT2Price, state.DraftT2Label, BrushDraftTarget, dashed: true);
                ApplyLine(TagActiveStop, TagActiveStopLabel, state.ShowActiveStop,
                    state.ActiveStopPrice, state.ActiveStopLabel, BrushActiveStop, dashed: false);
                ApplyLine(TagActiveT1, TagActiveT1Label, state.ShowActiveT1,
                    state.ActiveT1Price, state.ActiveT1Label, BrushActiveTarget, dashed: false);
                ApplyLine(TagActiveT2, TagActiveT2Label, state.ShowActiveT2,
                    state.ActiveT2Price, state.ActiveT2Label, BrushActiveTarget, dashed: false);

                lastApplied = state;
                hasLastApplied = true;
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.UI("PanelHost line renderer apply error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        public bool HasLastApplied { get { return hasLastApplied; } }
        public ChartGuardLineState LastApplied { get { return lastApplied; } }

        // Removes every artifact this renderer ever created (by Tag) and
        // forgets the cache. Safe to call multiple times. Must be called
        // from State.Terminated of the host.
        public void DetachAll()
        {
            try
            {
                foreach (string tag in AllTags)
                {
                    SafeRemove(tag);
                }
                activeTags.Clear();
                lastApplied = ChartGuardLineState.Empty();
                hasLastApplied = false;
                if (logger != null)
                    logger.UI("PanelHost line renderer detached (all read-only lines removed)");
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.UI("PanelHost line renderer detach error ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        // Single line + optional label rendering helper. When show is false,
        // both the line and its label are removed by tag. When show is true,
        // both are upserted using NinjaScript drawing tools with stable tags.
        //
        // Visual choices made here (and only here):
        //   * Line: full Draw.HorizontalLine overload with explicit width and
        //     dash style. NinjaTrader's HorizontalLine renders a native price
        //     marker on the right axis automatically, matching how a manual
        //     horizontal-line drawing tool looks.
        //   * Label: Draw.Text with a small opaque background so the text
        //     stays legible over candles. Anchored a few bars to the left of
        //     the rightmost bar so it does not overlap the price marker.
        private void ApplyLine(
            string lineTag, string labelTag, bool show, double price, string label, Brush brush, bool dashed)
        {
            if (!show || double.IsNaN(price) || double.IsInfinity(price) || price <= 0.0)
            {
                SafeRemove(lineTag);
                SafeRemove(labelTag);
                return;
            }

            try
            {
                DashStyleHelper dash = dashed ? DashStyleHelper.Dash : DashStyleHelper.Solid;
                Draw.HorizontalLine(host, lineTag, false, price, brush, dash, 2);
                activeTags[lineTag] = true;
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.UI("PanelHost line draw error tag=" + lineTag +
                        " ex=" + ex.GetType().Name + ":" + ex.Message);
            }

            if (string.IsNullOrEmpty(label))
            {
                SafeRemove(labelTag);
                return;
            }

            try
            {
                // Anchor the label a few bars to the left of the rightmost bar
                // so it does not overlap the native price marker the line just
                // produced. The opaque background makes the small text legible
                // over either green or red candles.
                int barsAgo = 6;
                Draw.Text(host, labelTag, false, label, barsAgo, price, 0, brush,
                    new SimpleFont("Segoe UI", 11) { Bold = true }, TextAlignment.Right,
                    BrushLabelOutline, BrushLabelFill, 90);
                activeTags[labelTag] = true;
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.UI("PanelHost label draw error tag=" + labelTag +
                        " ex=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private void SafeRemove(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            try
            {
                host.RemoveDrawObject(tag);
            }
            catch
            {
                // RemoveDrawObject can throw during teardown; never propagate.
            }
            activeTags.Remove(tag);
        }

        // ---- Color helpers (kept private; the panel theme palette is duplicated
        //      here on purpose so the renderer never depends on WPF Theme types
        //      that could pull NinjaTrader-specific assemblies into AddOns/Panel/) ----

        private static SolidColorBrush FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#')
                return new SolidColorBrush(Colors.Magenta);

            string h = hex.Substring(1);
            byte a = 255, r = 0, g = 0, b = 0;
            try
            {
                if (h.Length == 6)
                {
                    r = Convert.ToByte(h.Substring(0, 2), 16);
                    g = Convert.ToByte(h.Substring(2, 2), 16);
                    b = Convert.ToByte(h.Substring(4, 2), 16);
                }
                else if (h.Length == 8)
                {
                    a = Convert.ToByte(h.Substring(0, 2), 16);
                    r = Convert.ToByte(h.Substring(2, 2), 16);
                    g = Convert.ToByte(h.Substring(4, 2), 16);
                    b = Convert.ToByte(h.Substring(6, 2), 16);
                }
            }
            catch
            {
                a = 255; r = 255; g = 0; b = 255;
            }
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            if (brush == null) return null;
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        // Internal helper retained for diagnostic logging in case a future
        // change wants to print the per-tag count to the Output Window.
        public override string ToString()
        {
            return "ChartGuardReadOnlyLineRenderer activeTags=" +
                activeTags.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel
{
    // Visual tokens for the Essencial ChartGuard side panel.
    //
    // This class contains ONLY visual primitives (colors, brushes, sizes, helpers to build
    // typical WPF elements with a consistent look). It must never depend on:
    //   * NinjaTrader.Cbi / NinjaTrader.Data trading types,
    //   * the Safe Core trading services,
    //   * any historical "Cunha" / "CunhaTrader" / "CunhaScalper" / "Gold" code.
    // The palette and proportions are inspired only visually by docs/panel-visual-audit-cunha.md;
    // none of the historical implementation is reused.
    public static class EssencialChartGuardTheme
    {
        // ---- Palette (frozen WPF brushes; safe to reuse from any thread) ----

        // Backgrounds: near-black root, slightly lighter cards, even lighter input rows.
        public static readonly Brush BackgroundRoot = Freeze(FromHex("#0E0F12"));
        public static readonly Brush BackgroundCard = Freeze(FromHex("#15171C"));
        public static readonly Brush BackgroundInput = Freeze(FromHex("#1B1E25"));
        public static readonly Brush BackgroundDisabled = Freeze(FromHex("#202329"));
        public static readonly Brush BorderSubtle = Freeze(FromHex("#262A33"));
        public static readonly Brush BorderStrong = Freeze(FromHex("#383D48"));

        // Text.
        public static readonly Brush TextPrimary = Freeze(FromHex("#E6E8EE"));
        public static readonly Brush TextSecondary = Freeze(FromHex("#9099A8"));
        public static readonly Brush TextMuted = Freeze(FromHex("#5C6373"));

        // Accents.
        public static readonly Brush AccentGold = Freeze(FromHex("#D4A24C"));     // section headers, brand
        public static readonly Brush AccentGoldDim = Freeze(FromHex("#8A6A2E"));  // section underlines
        public static readonly Brush AccentGreen = Freeze(FromHex("#3FB35E"));    // long / positive
        public static readonly Brush AccentRed = Freeze(FromHex("#D14B4B"));      // short / danger
        public static readonly Brush AccentBlue = Freeze(FromHex("#4D8CD9"));     // info / breakeven
        public static readonly Brush AccentOrange = Freeze(FromHex("#D98545"));   // caution / pressure
        public static readonly Brush AccentDotIdle = Freeze(FromHex("#5C6373"));
        public static readonly Brush AccentDotOk = Freeze(FromHex("#3FB35E"));
        public static readonly Brush AccentDotWarn = Freeze(FromHex("#D98545"));
        public static readonly Brush AccentDotError = Freeze(FromHex("#D14B4B"));

        // ---- Typography ----

        public static readonly FontFamily FontUi = new FontFamily("Segoe UI");
        public static readonly FontFamily FontMono = new FontFamily("Consolas");

        public const double FontSizeBrand = 15.0;
        public const double FontSizeSubtitle = 11.0;
        public const double FontSizeSectionHeader = 11.0;
        public const double FontSizeLabel = 11.0;
        public const double FontSizeValue = 13.0;
        public const double FontSizeValueLarge = 16.0;
        public const double FontSizeFootnote = 10.0;

        // ---- Sizing ----

        public const double PanelInitialWidth = 370.0;
        public const double PanelMinWidth = 280.0;
        public const double PanelMaxWidth = 640.0;

        public const double SplitterWidth = 4.0;
        public const double CardCornerRadius = 5.0;
        public const double DotSize = 8.0;

        public static readonly Thickness PanelOuterPadding = new Thickness(10, 10, 10, 10);
        public static readonly Thickness CardPadding = new Thickness(10, 8, 10, 10);
        public static readonly Thickness CardSpacing = new Thickness(0, 0, 0, 8);
        public static readonly Thickness RowSpacing = new Thickness(0, 0, 0, 4);
        public static readonly Thickness SectionHeaderMargin = new Thickness(0, 0, 0, 6);

        // ---- Helpers (no trading logic, no state, only WPF construction) ----

        // Builds a standard "section card" border with the panel's card background and
        // subtle outline. Returns a Border whose Child the caller fills with content.
        public static System.Windows.Controls.Border CreateCard()
        {
            return new System.Windows.Controls.Border
            {
                Background = BackgroundCard,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CardCornerRadius),
                Padding = CardPadding,
                Margin = CardSpacing
            };
        }

        // Section title line (gold, small caps look via uppercase string + letter spacing
        // simulated by tracking on the FontStretch-free Segoe UI). The visual cue is the
        // gold underline drawn underneath, which the panel composes using a separate Border.
        public static System.Windows.Controls.TextBlock CreateSectionTitle(string text)
        {
            return new System.Windows.Controls.TextBlock
            {
                Text = (text ?? string.Empty).ToUpperInvariant(),
                Foreground = AccentGold,
                FontFamily = FontUi,
                FontSize = FontSizeSectionHeader,
                FontWeight = FontWeights.SemiBold,
                Margin = SectionHeaderMargin
            };
        }

        public static System.Windows.Controls.Border CreateSectionUnderline()
        {
            return new System.Windows.Controls.Border
            {
                Background = AccentGoldDim,
                Height = 1,
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        public static System.Windows.Controls.TextBlock CreateLabel(string text)
        {
            return new System.Windows.Controls.TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = TextSecondary,
                FontFamily = FontUi,
                FontSize = FontSizeLabel
            };
        }

        public static System.Windows.Controls.TextBlock CreateValue(string text)
        {
            return new System.Windows.Controls.TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = TextPrimary,
                FontFamily = FontMono,
                FontSize = FontSizeValue,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        public static System.Windows.Shapes.Ellipse CreateStatusDot(Brush fill)
        {
            return new System.Windows.Shapes.Ellipse
            {
                Width = DotSize,
                Height = DotSize,
                Fill = fill ?? AccentDotIdle,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ---- Color helpers ----

        private static SolidColorBrush FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#')
                return new SolidColorBrush(Colors.Magenta); // visible fallback if a token is wrong

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
    }
}

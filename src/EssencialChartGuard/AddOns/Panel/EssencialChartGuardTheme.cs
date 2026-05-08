using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.Panel
{
    // Visual tokens for the Essencial ChartGuard side panel.
    //
    // This class contains ONLY visual primitives (colors, brushes, sizes,
    // typography, and helpers to build typical WPF elements with a consistent
    // look). It must never depend on:
    //   * NinjaTrader.Cbi / NinjaTrader.Data trading types,
    //   * the Safe Core trading services.
    public static class EssencialChartGuardTheme
    {
        // =====================================================================
        // Palette — semantic surfaces, borders, text, accents
        // =====================================================================

        // Surfaces. Near-black root, slightly lighter cards, slightly lighter
        // input rows.
        public static readonly Brush BgRoot = Freeze(FromHex("#06070A"));
        public static readonly Brush BgSurface = Freeze(FromHex("#10121A"));
        public static readonly Brush BgSurface2 = Freeze(FromHex("#181C24"));
        public static readonly Brush BgHover = Freeze(FromHex("#22282E"));

        // Borders + dividers.
        public static readonly Brush Border = Freeze(FromHex("#2C3239"));
        public static readonly Brush BorderStrong = Freeze(FromHex("#404750"));
        public static readonly Brush Divider = Freeze(FromHex("#22272C"));

        // Text.
        public static readonly Brush TextPrimary = Freeze(FromHex("#F5F7FA"));
        public static readonly Brush TextSecondary = Freeze(FromHex("#C0C8D2"));
        public static readonly Brush TextMuted = Freeze(FromHex("#868E99"));
        public static readonly Brush TextDisabled = Freeze(FromHex("#555C65"));

        // Gold. Section headers, brand. GoldSoft for hover/highlight,
        // GoldDim for underlines and dividers.
        public static readonly Brush Gold = Freeze(FromHex("#D4AF37"));
        public static readonly Brush GoldSoft = Freeze(FromHex("#E6C24C"));
        public static readonly Brush GoldDim = Freeze(FromHex("#9A7E26"));

        // Buy / Sell / stop accents. Vivid pure colors so the operator can
        // read them at a glance over candles.
        public static readonly Brush AccentBuy = Freeze(FromHex("#0AE600"));
        public static readonly Brush AccentBuyDim = Freeze(FromHex("#089000"));
        public static readonly Brush AccentBuyHover = Freeze(FromHex("#2AFF20"));
        public static readonly Brush AccentSell = Freeze(FromHex("#FF0000"));
        public static readonly Brush AccentSellDim = Freeze(FromHex("#B00000"));
        public static readonly Brush AccentSellHover = Freeze(FromHex("#FF3333"));
        public static readonly Brush AccentStopBuy = Freeze(FromHex("#F400FF"));
        public static readonly Brush AccentStopSell = Freeze(FromHex("#00FFFF"));
        public static readonly Brush AccentInfo = Freeze(FromHex("#297CFF"));
        public static readonly Brush AccentInfoHover = Freeze(FromHex("#4A96FF"));
        public static readonly Brush AccentWarn = Freeze(FromHex("#E6C24C"));
        public static readonly Brush AccentDanger = Freeze(FromHex("#E61236"));

        // Risk gradient. Drawn into progress bars; the brush is also picked
        // by RiskBrushForPct(...).
        public static readonly Brush RiskGreen = Freeze(FromHex("#00E06B"));
        public static readonly Brush RiskAmber = Freeze(FromHex("#FFB82E"));
        public static readonly Brush RiskRed = Freeze(FromHex("#FF294D"));

        // Status dots (idle / ok / warn / error) used by the panel header,
        // observation card, and read-only line renderer.
        public static readonly Brush AccentDotIdle = Freeze(FromHex("#5C6373"));
        public static readonly Brush AccentDotOk = Freeze(FromHex("#3FB35E"));
        public static readonly Brush AccentDotWarn = Freeze(FromHex("#D98545"));
        public static readonly Brush AccentDotError = Freeze(FromHex("#D14B4B"));

        // =====================================================================
        // Typography
        // =====================================================================

        public static readonly FontFamily FontUi = new FontFamily("Segoe UI");
        public static readonly FontFamily FontMono = new FontFamily("Consolas");

        // New shell tokens.
        public const double FontSizeTitle = 14;
        public const double FontSizeBody = 13;
        public const double FontSizeValue = 14;
        public const double FontSizeSmall = 11;
        public const double FontSizeButton = 14;
        public const double FontSizeButtonBig = 16;

        // Aliases used by the current panel.
        public const double FontSizeBrand = 15.0;
        public const double FontSizeSubtitle = 11.0;
        public const double FontSizeSectionHeader = 11.0;
        public const double FontSizeLabel = 11.0;
        public const double FontSizeValueLarge = 16.0;
        public const double FontSizeFootnote = 10.0;

        public static double FontSizeFor(string role)
        {
            switch (role)
            {
                case "title": return FontSizeTitle;
                case "value": return FontSizeValue;
                case "small": return FontSizeSmall;
                case "button": return FontSizeButton;
                case "body":
                default: return FontSizeBody;
            }
        }

        // =====================================================================
        // Spacing
        // =====================================================================

        public const double SpaceXs = 2;
        public const double SpaceSm = 3;
        public const double SpaceMd = 6;
        public const double SpaceLg = 9;
        public const double SpaceXl = 12;
        public const double SpaceXxl = 18;

        public const double Radius = 5;
        public const double RadiusLg = 10;

        public const double SectionVerticalGap = SpaceMd;

        // ---- Sizing kept for compatibility with the current panel host ----

        public const double PanelInitialWidth = 370.0;
        public const double PanelMinWidth = 280.0;
        public const double PanelMaxWidth = 640.0;

        public const double SplitterWidth = 4.0;
        public const double CardCornerRadius = Radius;
        public const double DotSize = 8.0;

        public static readonly Thickness PanelOuterPadding = new Thickness(10, 10, 10, 10);
        public static readonly Thickness CardPadding = new Thickness(10, 8, 10, 10);
        public static readonly Thickness CardSpacing = new Thickness(0, 0, 0, 8);
        public static readonly Thickness RowSpacing = new Thickness(0, 0, 0, 4);
        public static readonly Thickness SectionHeaderMargin = new Thickness(0, 0, 0, 6);

        // =====================================================================
        // Builders (rich set) — used by the next panel shell
        // =====================================================================

        public static TextBlock MakeLabel(string text, string role = "body")
        {
            Brush fg;
            switch (role)
            {
                case "title": fg = TextPrimary; break;
                case "muted": fg = TextMuted; break;
                case "secondary": fg = TextSecondary; break;
                case "value": fg = TextPrimary; break;
                case "field": fg = Gold; break;
                default: fg = TextSecondary; break;
            }

            return new TextBlock
            {
                Text = text ?? string.Empty,
                FontFamily = role == "value" ? FontMono : FontUi,
                FontSize = FontSizeFor(role == "value" ? "value" : (role == "title" ? "title" : "body")),
                FontWeight = role == "title" || role == "field" ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // Section header: gold uppercase title with a thin gold underline,
        // plus an optional info-icon tooltip for longer help.
        public static Border MakeSectionHeader(string title, string subtitle = null)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock t = new TextBlock
            {
                Text = (title ?? string.Empty).ToUpperInvariant(),
                FontFamily = FontUi,
                FontSize = FontSizeBody,
                FontWeight = FontWeights.Bold,
                Foreground = Gold,
                VerticalAlignment = VerticalAlignment.Center
            };
            t.SetValue(TextBlock.LineHeightProperty, 15.0);
            row.Children.Add(t);

            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock info = new TextBlock
                {
                    Text = "ⓘ",
                    FontFamily = FontUi,
                    FontSize = FontSizeBody,
                    Foreground = GoldDim,
                    Margin = new Thickness(SpaceSm, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Help
                };
                info.ToolTip = WrapTooltip(subtitle);
                ToolTipService.SetInitialShowDelay(info, 250);
                ToolTipService.SetShowDuration(info, 30000);
                info.MouseEnter += (s, e) => info.Foreground = Gold;
                info.MouseLeave += (s, e) => info.Foreground = GoldDim;
                row.Children.Add(info);
            }

            return new Border
            {
                Padding = new Thickness(0, 0, 0, SpaceXs),
                BorderBrush = GoldDim,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, SpaceSm, 0, SpaceSm),
                Child = row
            };
        }

        public static Border MakeDivider()
        {
            return new Border
            {
                Height = 1,
                Background = Divider,
                Margin = new Thickness(0, SpaceSm, 0, SpaceSm)
            };
        }

        public static Border MakeSection(string title, UIElement content, string subtitle = null)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(MakeSectionHeader(title, subtitle));
            if (content != null) sp.Children.Add(content);
            return new Border
            {
                Background = BgSurface,
                CornerRadius = new CornerRadius(Radius),
                Padding = new Thickness(SpaceMd, SpaceSm, SpaceMd, SpaceMd),
                Margin = new Thickness(0, 0, 0, SpaceMd),
                Child = sp
            };
        }

        public static Border MakeCard(UIElement content)
        {
            return new Border
            {
                Background = BgSurface,
                CornerRadius = new CornerRadius(Radius),
                Padding = new Thickness(SpaceLg, SpaceMd, SpaceLg, SpaceMd),
                Child = content
            };
        }

        public static Border MakeBareCard(UIElement content)
        {
            return new Border
            {
                Background = BgSurface,
                CornerRadius = new CornerRadius(Radius),
                Padding = new Thickness(SpaceMd, SpaceMd, SpaceMd, SpaceMd),
                Margin = new Thickness(0, SpaceMd, 0, SpaceMd),
                Child = content
            };
        }

        // CTA button. Tooltip is non-optional so every interactive element is
        // hover-discoverable.
        public static Button MakeButton(string text, ButtonRole role, string tooltip = null)
        {
            Brush bg, hover, fg;
            switch (role)
            {
                case ButtonRole.Buy:
                    bg = AccentBuy; hover = AccentBuyHover; fg = TextPrimary; break;
                case ButtonRole.Sell:
                    bg = AccentSell; hover = AccentSellHover; fg = TextPrimary; break;
                case ButtonRole.Danger:
                    bg = AccentDanger; hover = AccentSellHover; fg = TextPrimary; break;
                case ButtonRole.Info:
                    bg = AccentInfo; hover = AccentInfoHover; fg = TextPrimary; break;
                case ButtonRole.Ghost:
                    bg = System.Windows.Media.Brushes.Transparent; hover = BgHover; fg = TextPrimary; break;
                case ButtonRole.Secondary:
                default:
                    bg = BgSurface2; hover = BgHover; fg = TextPrimary; break;
            }

            Button b = new Button
            {
                Content = text,
                FontFamily = FontUi,
                FontSize = FontSizeButton,
                FontWeight = FontWeights.SemiBold,
                Foreground = fg,
                Background = bg,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(SpaceLg, SpaceMd, SpaceLg, SpaceMd),
                Cursor = Cursors.Hand,
                MinHeight = 36,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
                Style = MakeButtonStyle()
            };

            b.ToolTip = WrapTooltip(string.IsNullOrEmpty(tooltip) ? text : tooltip);
            ToolTipService.SetInitialShowDelay(b, 350);
            ToolTipService.SetShowDuration(b, 30000);

            b.MouseEnter += (s, e) => b.Background = hover;
            b.MouseLeave += (s, e) => b.Background = bg;
            return b;
        }

        // Numeric input with [-]/[+] step buttons. The step buttons here are
        // visual-only stubs in this build: they update the inner TextBox text
        // through an apply lambda but never raise commands or talk to a
        // service. The new panel shell wires its read-only mutators around
        // them.
        public static StackPanel MakeNumericUpDown(
            string defaultText, string tooltip,
            out TextBox inputBox, double inputWidth = 50, double step = 1, double min = 1, double max = 9999)
        {
            StackPanel host = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button minus = MakeStepButton("−", "Diminuir.");
            host.Children.Add(minus);

            TextBox tb = MakeNumberBox(defaultText, tooltip, inputWidth);
            tb.Margin = new Thickness(0, 0, 0, 0);
            host.Children.Add(tb);
            inputBox = tb;

            Button plus = MakeStepButton("+", "Aumentar.");
            host.Children.Add(plus);

            Action<double> apply = delta =>
            {
                double cur;
                if (!double.TryParse((tb.Text ?? string.Empty).Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out cur))
                    cur = min;
                double next = cur + delta;
                if (next < min) next = min;
                if (next > max) next = max;
                bool intStep = step == Math.Floor(step) && step >= 1;
                tb.Text = intStep
                    ? ((int)Math.Round(next)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : next.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            };
            minus.Click += (s, e) => apply(-step);
            plus.Click += (s, e) => apply(+step);

            return host;
        }

        private static Button MakeStepButton(string glyph, string tooltip)
        {
            Button b = new Button
            {
                Content = glyph,
                FontFamily = FontUi,
                FontSize = FontSizeBody,
                FontWeight = FontWeights.Bold,
                Foreground = Gold,
                Background = BgSurface2,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                MinWidth = 22,
                MinHeight = 28,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
                Style = MakeButtonStyle()
            };
            b.ToolTip = WrapTooltip(tooltip);
            ToolTipService.SetInitialShowDelay(b, 350);
            b.MouseEnter += (s, e) => { b.Background = BgHover; b.Foreground = GoldSoft; };
            b.MouseLeave += (s, e) => { b.Background = BgSurface2; b.Foreground = Gold; };
            return b;
        }

        public static TextBox MakeNumberBox(string defaultText = "", string tooltip = null, double width = 60)
        {
            TextBox tb = new TextBox
            {
                Text = defaultText,
                FontFamily = FontMono,
                FontSize = FontSizeValue,
                Foreground = TextPrimary,
                Background = BgSurface2,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(SpaceMd, 0, SpaceMd, 0),
                Width = width,
                MinHeight = 28,
                TextAlignment = TextAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tb.Resources[SystemColors.HighlightBrushKey] = GoldDim;
            tb.Resources[SystemColors.HighlightTextBrushKey] = TextPrimary;
            if (!string.IsNullOrEmpty(tooltip))
            {
                tb.ToolTip = WrapTooltip(tooltip);
                ToolTipService.SetInitialShowDelay(tb, 350);
                ToolTipService.SetShowDuration(tb, 30000);
            }
            return tb;
        }

        public static ComboBox MakeCombo(string tooltip = null, double minWidth = 110)
        {
            ComboBox cb = new ComboBox
            {
                FontFamily = FontUi,
                FontSize = FontSizeBody,
                Foreground = TextPrimary,
                Background = BgSurface2,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                MinWidth = minWidth,
                MinHeight = 28,
                Padding = new Thickness(SpaceMd, 0, SpaceMd, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            cb.ItemContainerStyle = MakeComboItemStyle();
            if (!string.IsNullOrEmpty(tooltip))
            {
                cb.ToolTip = WrapTooltip(tooltip);
                ToolTipService.SetInitialShowDelay(cb, 350);
                ToolTipService.SetShowDuration(cb, 30000);
            }
            return cb;
        }

        public static object WrapTooltip(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            TextBlock tb = new TextBlock
            {
                Text = text,
                MaxWidth = 320,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = FontUi,
                FontSize = FontSizeBody,
                Foreground = TextPrimary
            };
            return new Border
            {
                Background = BgSurface,
                BorderBrush = BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(Radius),
                Padding = new Thickness(SpaceMd, SpaceSm, SpaceMd, SpaceSm),
                Child = tb
            };
        }

        public static Border MakeProgressBar(double percent, Brush fillBrush, double height = 6)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            Grid g = new Grid { Height = height };
            Border track = new Border
            {
                Background = BgSurface2,
                CornerRadius = new CornerRadius(height / 2)
            };
            Border fill = new Border
            {
                Background = fillBrush,
                CornerRadius = new CornerRadius(height / 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            g.Children.Add(track);
            g.Children.Add(fill);
            g.SizeChanged += (s, e) =>
            {
                fill.Width = Math.Max(0, e.NewSize.Width * (percent / 100.0));
            };
            return new Border { Child = g };
        }

        public static Brush RiskBrushForPct(double pct)
        {
            if (pct >= 75) return RiskRed;
            if (pct >= 25) return RiskAmber;
            return RiskGreen;
        }

        // =====================================================================
        // Simple builders consumed by EssencialChartGuardPanel and the
        // read-only line renderer.
        // =====================================================================

        public static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = TextSecondary,
                FontFamily = FontUi,
                FontSize = FontSizeLabel
            };
        }

        public static TextBlock CreateValue(string text)
        {
            return new TextBlock
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

        // =====================================================================
        // Button + ComboBoxItem styles (replace WPF defaults so focus/hover
        // colors come from the theme, not the system)
        // =====================================================================

        // Note: this class exposes a `Brush Border` token. Inside the style
        // builders below we always reference the WPF Border control by its
        // fully-qualified name `System.Windows.Controls.Border` to avoid the
        // C# resolver picking up the brush instead of the type.
        private static Style cachedButtonStyle;
        public static Style MakeButtonStyle()
        {
            if (cachedButtonStyle != null) return cachedButtonStyle;

            Style s = new Style(typeof(Button));
            s.Setters.Add(new Setter(Button.OverridesDefaultStyleProperty, true));
            s.Setters.Add(new Setter(Button.FocusVisualStyleProperty, null));

            FrameworkElementFactory borderFactory =
                new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            borderFactory.Name = "Bd";
            borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty,
                new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(System.Windows.Controls.Border.BorderBrushProperty,
                new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(System.Windows.Controls.Border.BorderThicknessProperty,
                new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty,
                new CornerRadius(Radius));
            borderFactory.SetValue(System.Windows.Controls.Border.PaddingProperty,
                new TemplateBindingExtension(Button.PaddingProperty));

            FrameworkElementFactory contentFactory =
                new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = borderFactory;
            s.Setters.Add(new Setter(Button.TemplateProperty, template));

            s.Seal();
            cachedButtonStyle = s;
            return s;
        }

        private static Style cachedComboItemStyle;
        public static Style MakeComboItemStyle()
        {
            if (cachedComboItemStyle != null) return cachedComboItemStyle;

            Style s = new Style(typeof(ComboBoxItem));
            s.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, BgSurface2));
            s.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, TextPrimary));
            s.Setters.Add(new Setter(ComboBoxItem.PaddingProperty,
                new Thickness(SpaceMd, SpaceXs, SpaceMd, SpaceXs)));
            s.Setters.Add(new Setter(ComboBoxItem.BorderThicknessProperty, new Thickness(0)));

            FrameworkElementFactory borderFactory =
                new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            borderFactory.Name = "Bd";
            borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty,
                new TemplateBindingExtension(ComboBoxItem.BackgroundProperty));
            borderFactory.SetValue(System.Windows.Controls.Border.PaddingProperty,
                new TemplateBindingExtension(ComboBoxItem.PaddingProperty));

            FrameworkElementFactory contentFactory =
                new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            ControlTemplate template = new ControlTemplate(typeof(ComboBoxItem));
            template.VisualTree = borderFactory;

            Trigger highlightTrigger = new Trigger
            {
                Property = ComboBoxItem.IsHighlightedProperty,
                Value = true
            };
            highlightTrigger.Setters.Add(new Setter(
                System.Windows.Controls.Border.BackgroundProperty, GoldDim, "Bd"));
            highlightTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, TextPrimary));
            template.Triggers.Add(highlightTrigger);

            Trigger selectedTrigger = new Trigger
            {
                Property = ComboBoxItem.IsSelectedProperty,
                Value = true
            };
            selectedTrigger.Setters.Add(new Setter(
                System.Windows.Controls.Border.BackgroundProperty, GoldDim, "Bd"));
            selectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, TextPrimary));
            template.Triggers.Add(selectedTrigger);

            s.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, template));

            s.Seal();
            cachedComboItemStyle = s;
            return s;
        }

        // =====================================================================
        // Color helpers
        // =====================================================================

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
    }

    public enum ButtonRole
    {
        Secondary,
        Buy,
        Sell,
        Danger,
        Info,
        Ghost
    }
}

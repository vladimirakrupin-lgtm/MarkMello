using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Text metrics of an inline formula: the font size the engine typesets at,
/// the fallback font used when the LaTeX does not parse, the line height the
/// run has to fit into, and the body descent that pads the formula box below
/// the baseline.
/// </summary>
internal readonly record struct MarkdownMathMetrics(
    double FontSize,
    IBrush Foreground,
    double FallbackFontSize,
    double LineHeight,
    double Descent)
{
    public static MarkdownMathMetrics Create(FontFamily fontFamily, double baseFontSize, IBrush foreground, double lineHeight)
    {
        var descent = MeasureBodyDescent(fontFamily, baseFontSize);
        return new(
            Math.Max(10, baseFontSize),
            foreground,
            Math.Max(9, baseFontSize * 0.85),
            double.IsNaN(lineHeight) || lineHeight <= 0 ? baseFontSize * 1.5 : lineHeight,
            descent);
    }

    /// <summary>
    /// Body text descent for the current font size. An inline formula box does
    /// not carry a descent of its own, so the real value is measured from the
    /// same font the paragraph uses; guessing a ratio would mis-seat formulas
    /// whenever the reading font or size changes.
    /// </summary>
    private static double MeasureBodyDescent(FontFamily fontFamily, double fontSize)
    {
        using var probe = new TextLayout(
            "np",
            new Typeface(fontFamily),
            Math.Max(1, fontSize),
            Brushes.Black,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        return Math.Max(0, probe.Height - probe.Baseline);
    }
}

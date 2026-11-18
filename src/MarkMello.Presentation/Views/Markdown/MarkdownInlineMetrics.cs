using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Fixed horizontal padding and height of a code "pill". The pad width is a
/// constant; the height and baseline come from the code font so the pill hugs
/// the glyphs it surrounds.
/// </summary>
internal readonly record struct MarkdownInlineCodePadMetrics(
    double LeftWidth,
    double RightWidth,
    double Height,
    double Baseline)
{
    public static MarkdownInlineCodePadMetrics Create(
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        FontStyle fontStyle,
        IBrush foreground)
    {
        using var probe = new TextLayout(
            "M",
            new Typeface(fontFamily, fontStyle, fontWeight),
            fontSize,
            foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 0,
            textStyleOverrides: null);

        var height = Math.Max(1, probe.Height);
        var baseline = Math.Clamp(probe.Baseline, 0, height);
        return new MarkdownInlineCodePadMetrics(4, 4, height, baseline);
    }
}

/// <summary>
/// Size limits for an inline image: it may not exceed the line height too far,
/// and its baseline is seated so taller images grow the line instead of
/// dropping below it. The typeface/font size are the placeholder label's.
/// </summary>
internal readonly record struct MarkdownInlineImageMetrics(
    double MaxHeight,
    double Baseline,
    Typeface Typeface,
    double FontSize,
    IBrush Foreground)
{
    public static MarkdownInlineImageMetrics Create(
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        FontStyle fontStyle,
        double lineHeight,
        IBrush foreground)
    {
        using var probe = new TextLayout(
            "M",
            new Typeface(fontFamily, fontStyle, fontWeight),
            fontSize,
            foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 0,
            textStyleOverrides: null);

        var normalizedLineHeight = double.IsNaN(lineHeight) || lineHeight <= 0
            ? Math.Max(fontSize * 1.25, probe.Height)
            : lineHeight;
        var maxHeight = Math.Max(12, normalizedLineHeight * 0.9);
        var baseline = Math.Clamp(
            probe.Baseline + Math.Max(0, (maxHeight - probe.Height) / 2),
            0,
            maxHeight);

        return new MarkdownInlineImageMetrics(
            maxHeight,
            baseline,
            new Typeface(fontFamily, fontStyle, fontWeight),
            Math.Max(10, fontSize * 0.75),
            foreground);
    }
}

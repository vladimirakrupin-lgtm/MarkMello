using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// The text run that paints a LaTeX formula in place of its whole canonical
/// TeX range (see <see cref="MarkdownMathSpan"/> and ADR-0010). The range stays
/// in the text stream — one run per character keeps caret mapping and
/// selection unchanged — and the first character is replaced by the rendered
/// formula; the rest are zero-width <see cref="MarkdownMathContinuationTextRun"/>
/// characters that claim no advance.
/// </summary>
internal sealed class MarkdownMathTextRun : DrawableTextRun
{
    private readonly string _latex;
    private readonly MarkdownMathMetrics _metrics;
    private readonly Size _size;
    private readonly double _baseline;
    private readonly MarkdownMathLayout? _layout;

    private MarkdownMathTextRun(string latex, MarkdownMathMetrics metrics)
    {
        _latex = latex;
        _metrics = metrics;
        _layout = MarkdownMathRenderer.Measure(latex, metrics.FontSize, metrics.Foreground, isDisplay: false);

        if (_layout is { IsValid: true } layout)
        {
            // Avalonia seats a drawable run with offsetY = (line baseline - run
            // baseline), so reporting the engine ascent as the run baseline lands
            // the math baseline exactly on the text baseline. Pad the box by the
            // text descent only so the formula's own below-baseline part (a
            // subscript, a fraction denominator) cannot be clipped by layout.
            var descender = _metrics.Descent;
            var boxHeight = Math.Max(layout.Size.Height, _metrics.FontSize) + descender;
            _size = new Size(layout.Size.Width, boxHeight);
            _baseline = Math.Clamp(layout.Baseline, 0, boxHeight);
        }
        else
        {
            // Unparseable formula: show the raw LaTeX so nothing is lost
            // (owner decision: fall back to source, not a hard error).
            _size = ResolveFallbackSize(latex, metrics);
            _baseline = _metrics.FontSize;
        }
    }

    public static MarkdownMathTextRun Create(MarkdownMathSpan formula, MarkdownMathMetrics metrics)
        => new(formula.Latex, metrics);

    public static MarkdownMathTextRun Fallback(string latex, MarkdownMathMetrics metrics)
        => new(latex, metrics);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    /// <summary>
    /// The formula owns the horizontal advance of its whole canonical range:
    /// the continuation characters that follow it in the display text are
    /// zero-width, so the width the reader sees and clicks is exactly the
    /// width of the range (ADR-0010).
    /// </summary>
    public override Size Size => _size;

    public override double Baseline => _baseline;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        if (_layout is { IsValid: true } layout)
        {
            // origin is the top-left of the run's box; Draw expects the same,
            // and places the formula's top at that point. The run box is
            // taller than a text line for fractions, so Avalonia positions its
            // top relative to the run Baseline (see MarkdownMathTextRun.Baseline).
            MarkdownMathRenderer.Draw(
                drawingContext,
                _latex,
                layout.Size,
                origin,
                _metrics.FontSize,
                _metrics.Foreground,
                isDisplay: false);
            return;
        }

        DrawSourceFallback(drawingContext, origin);
    }

    private void DrawSourceFallback(DrawingContext drawingContext, Point origin)
    {
        using var textLayout = new TextLayout(
            _latex,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            _metrics.FallbackFontSize,
            _metrics.Foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: Math.Max(1, _size.Width),
            maxHeight: Math.Max(1, _size.Height),
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        textLayout.Draw(drawingContext, origin);
    }

    private static Size ResolveFallbackSize(string latex, MarkdownMathMetrics metrics)
    {
        using var probe = new TextLayout(
            latex.Length == 0 ? " " : latex,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            metrics.FallbackFontSize,
            metrics.Foreground,
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

        return new Size(Math.Max(1, probe.Width), Math.Max(1, probe.Height));
    }

    private sealed class EmptyTextRunProperties : TextRunProperties
    {
        public static EmptyTextRunProperties Instance { get; } = new();

        public override Typeface Typeface { get; } = new(Typeface.Default.FontFamily);

        public override double FontRenderingEmSize => 1;

        public override TextDecorationCollection? TextDecorations => null;

        public override IBrush? ForegroundBrush => null;

        public override IBrush? BackgroundBrush => null;

        public override BaselineAlignment BaselineAlignment => BaselineAlignment.Baseline;

        public override CultureInfo CultureInfo => CultureInfo.InvariantCulture;
    }
}

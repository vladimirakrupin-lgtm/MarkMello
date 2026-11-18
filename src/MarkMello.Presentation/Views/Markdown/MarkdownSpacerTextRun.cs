using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// A fixed-width spacer that pads inline code on each side. It draws nothing;
/// the code "pill" background is painted behind the glyphs by the fragment.
/// </summary>
internal sealed class MarkdownSpacerTextRun : DrawableTextRun
{
    private readonly double _width;
    private readonly double _height;
    private readonly double _baseline;

    private MarkdownSpacerTextRun(double width, double height, double baseline)
    {
        _width = width;
        _height = height;
        _baseline = baseline;
    }

    public static MarkdownSpacerTextRun Left(MarkdownInlineCodePadMetrics metrics)
        => new(metrics.LeftWidth, metrics.Height, metrics.Baseline);

    public static MarkdownSpacerTextRun Right(MarkdownInlineCodePadMetrics metrics)
        => new(metrics.RightWidth, metrics.Height, metrics.Baseline);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => new(_width, _height);

    public override double Baseline => _baseline;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
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

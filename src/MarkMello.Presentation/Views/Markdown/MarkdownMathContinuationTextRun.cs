using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Zero-width position inside a formula's canonical TeX range. The formula
/// itself is painted once, by the <see cref="MarkdownMathTextRun"/> that starts
/// the range; the layout still needs one text run per canonical character so
/// caret, selection and search indices stay 1:1 with the text map (ADR-0010).
/// </summary>
internal sealed class MarkdownMathContinuationTextRun : DrawableTextRun
{
    public static MarkdownMathContinuationTextRun Instance { get; } = new();

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => new(0, 1);

    public override double Baseline => 0;

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

        public override System.Globalization.CultureInfo CultureInfo => System.Globalization.CultureInfo.InvariantCulture;
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed class MarkdownInlineImageTextRun : DrawableTextRun
{
    private const double PlaceholderHorizontalPadding = 8;
    private const double PlaceholderMinWidth = 34;
    private const double PlaceholderMaxWidth = 120;
    private static readonly IBrush DataImageBackground = new SolidColorBrush(Color.FromRgb(250, 250, 250));

    private readonly string _label;
    private readonly MarkdownInlineImageMetrics _metrics;
    private readonly IImage? _image;
    private readonly bool _failed;
    private readonly bool _drawImageBackground;
    private readonly Size _size;
    private readonly double _baseline;

    private MarkdownInlineImageTextRun(
        string label,
        MarkdownInlineImageMetrics metrics,
        IImage? image,
        bool failed,
        bool drawImageBackground)
    {
        _label = string.IsNullOrWhiteSpace(label) ? "image" : label;
        _metrics = metrics;
        _image = image;
        _failed = failed;
        _drawImageBackground = drawImageBackground;
        _size = ResolveSize(_label, metrics, image);
        _baseline = Math.Min(_size.Height, metrics.Baseline);
    }

    public static MarkdownInlineImageTextRun Create(
        MarkdownInlineImageSpan image,
        MarkdownInlineImageState? state,
        MarkdownInlineImageMetrics metrics)
        => new(
            image.PlaceholderText,
            metrics,
            state?.Image,
            state?.Failed == true,
            image.Url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase));

    public static MarkdownInlineImageTextRun Placeholder(string label, MarkdownInlineImageMetrics metrics, bool failed)
        => new(label, metrics, null, failed, drawImageBackground: false);

    public override int Length => 1;

    public override ReadOnlyMemory<char> Text => " ".AsMemory();

    public override TextRunProperties Properties => EmptyTextRunProperties.Instance;

    public override Size Size => _size;

    public override double Baseline => _baseline;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        var rect = new Rect(origin, _size);
        if (_image is not null)
        {
            if (_drawImageBackground)
            {
                drawingContext.DrawRectangle(DataImageBackground, null, rect, 2, 2);
            }

            drawingContext.DrawImage(_image, new Rect(_image.Size), rect);
            return;
        }

        var fill = _failed ? Brushes.Transparent : Brushes.LightGray;
        var pen = new Pen(Brushes.Gray, 1);
        drawingContext.DrawRectangle(fill, pen, rect, 3, 3);

        using var textLayout = new TextLayout(
            _failed ? "image" : _label,
            _metrics.Typeface,
            _metrics.FontSize,
            _metrics.Foreground,
            TextAlignment.Center,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: Math.Max(1, rect.Width - PlaceholderHorizontalPadding),
            maxHeight: Math.Max(1, rect.Height),
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        var textOrigin = new Point(
            rect.X + Math.Max(0, (rect.Width - textLayout.Width) / 2),
            rect.Y + Math.Max(0, (rect.Height - textLayout.Height) / 2));
        textLayout.Draw(drawingContext, textOrigin);
    }

    private static Size ResolveSize(string label, MarkdownInlineImageMetrics metrics, IImage? image)
    {
        if (image is not null)
        {
            var natural = image.Size;
            var scale = Math.Min(1d, metrics.MaxHeight / Math.Max(1, natural.Height));
            return new Size(
                Math.Max(1, natural.Width * scale),
                Math.Max(1, natural.Height * scale));
        }

        var width = Math.Clamp(
            label.Length * Math.Max(5, metrics.FontSize * 0.55) + PlaceholderHorizontalPadding * 2,
            PlaceholderMinWidth,
            PlaceholderMaxWidth);
        return new Size(width, Math.Max(14, metrics.MaxHeight * 0.85));
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

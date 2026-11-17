using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Supplies one text run per display position of a styled text: styled glyphs,
/// line breaks, inline images, code padding and formulas. The characters of a
/// formula after its first are zero-width continuations (see
/// <see cref="MarkdownMathTextRun"/>), so every canonical character keeps its own
/// text source position.
/// </summary>
internal sealed class MarkdownTextSource : ITextSource
{
    private readonly MarkdownDisplayLayoutModel _displayModel;
    private readonly IReadOnlyList<MarkdownInlineImageSpan> _images;
    private readonly IReadOnlyList<MarkdownMathSpan> _math;
    private readonly IReadOnlyDictionary<int, MarkdownInlineImageState> _imageStates;
    private readonly MarkdownTextRunPropertiesFactory _propertiesFactory;
    private readonly MarkdownInlineCodePadMetrics _padMetrics;
    private readonly MarkdownInlineImageMetrics _imageMetrics;
    private readonly MarkdownMathMetrics _mathMetrics;

    public MarkdownTextSource(
        MarkdownDisplayLayoutModel displayModel,
        IReadOnlyList<MarkdownInlineImageSpan> images,
        IReadOnlyList<MarkdownMathSpan> math,
        IReadOnlyDictionary<int, MarkdownInlineImageState> imageStates,
        MarkdownTextRunPropertiesFactory propertiesFactory,
        MarkdownInlineCodePadMetrics padMetrics,
        MarkdownInlineImageMetrics imageMetrics,
        MarkdownMathMetrics mathMetrics,
        double maxWidth = 100_000)
    {
        _displayModel = displayModel;
        _images = images;
        _math = math;
        _imageStates = imageStates;
        _propertiesFactory = propertiesFactory;
        _padMetrics = padMetrics;
        _imageMetrics = imageMetrics;
        _mathMetrics = mathMetrics;
        MaxWidth = maxWidth;
    }

    public double MaxWidth { get; }

    public TextRun GetTextRun(int textSourceIndex)
    {
        if (textSourceIndex >= _displayModel.DisplayLength)
        {
            return new TextEndOfParagraph(1);
        }

        var segmentIndex = _displayModel.FindSegmentIndex(textSourceIndex);
        if (segmentIndex < 0)
        {
            return new TextEndOfParagraph(1);
        }

        var segment = _displayModel.Segments[segmentIndex];
        var localOffset = textSourceIndex - segment.DisplayStart;

        return segment.Kind switch
        {
            MarkdownDisplaySegmentKind.Text => new TextCharacters(
                segment.Text.AsMemory(localOffset),
                _propertiesFactory.Get(segment.Style)),
            MarkdownDisplaySegmentKind.LineBreak => new TextEndOfLine(1),
            MarkdownDisplaySegmentKind.Image => CreateImageRun(segment),
            MarkdownDisplaySegmentKind.Math => CreateMathRun(segment),
            MarkdownDisplaySegmentKind.CodePaddingRight => MarkdownSpacerTextRun.Right(_padMetrics),
            _ => new TextEndOfParagraph(1)
        };
    }

    private MarkdownMathTextRun CreateMathRun(MarkdownDisplaySegment segment)
    {
        if ((uint)segment.MathIndex >= (uint)_math.Count)
        {
            return MarkdownMathTextRun.Fallback(segment.Text, _mathMetrics);
        }

        var formula = _math[segment.MathIndex];
        return MarkdownMathTextRun.Create(formula, _mathMetrics);
    }

    private MarkdownInlineImageTextRun CreateImageRun(MarkdownDisplaySegment segment)
    {
        if ((uint)segment.ImageIndex >= (uint)_images.Count)
        {
            return MarkdownInlineImageTextRun.Placeholder(segment.Text, _imageMetrics, failed: true);
        }

        var image = _images[segment.ImageIndex];
        _imageStates.TryGetValue(image.Index, out var state);
        return MarkdownInlineImageTextRun.Create(image, state, _imageMetrics);
    }
}

/// <summary>
/// Caches one <see cref="TextRunProperties"/> per inline style state, so the
/// text formatter gets identical run properties for identical styles.
/// </summary>
internal sealed class MarkdownTextRunPropertiesFactory
{
    internal const double InlineCodeFontScale = 0.92;

    private readonly Dictionary<MarkdownInlineStyleState, TextRunProperties> _cache = new();
    private readonly FontFamily _baseFontFamily;
    private readonly FontFamily _inlineCodeFontFamily;
    private readonly double _fontSize;
    private readonly FontWeight _fontWeight;
    private readonly FontStyle _fontStyle;
    private readonly IBrush _foreground;
    private readonly TextDecorationCollection? _linkDecorations;

    public MarkdownTextRunPropertiesFactory(
        FontFamily baseFontFamily,
        FontFamily inlineCodeFontFamily,
        double fontSize,
        FontWeight fontWeight,
        FontStyle fontStyle,
        IBrush foreground,
        TextDecorationCollection? linkDecorations)
    {
        _baseFontFamily = baseFontFamily;
        _inlineCodeFontFamily = inlineCodeFontFamily;
        _fontSize = fontSize;
        _fontWeight = fontWeight;
        _fontStyle = fontStyle;
        _foreground = foreground;
        _linkDecorations = linkDecorations;
    }

    public TextRunProperties Get(MarkdownInlineStyleState style)
    {
        if (_cache.TryGetValue(style, out var properties))
        {
            return properties;
        }

        var family = style.IsCode ? _inlineCodeFontFamily : _baseFontFamily;
        var fontSize = style.IsCode ? _fontSize * InlineCodeFontScale : _fontSize;
        var weight = style.IsBold ? FontWeight.Bold : _fontWeight;
        var fontStyle = style.IsItalic ? FontStyle.Italic : _fontStyle;
        properties = new GenericTextRunProperties(
            new Typeface(family, fontStyle, weight),
            fontSize,
            style.IsLink ? _linkDecorations : null,
            _foreground,
            backgroundBrush: null,
            BaselineAlignment.Baseline,
            CultureInfo.CurrentUICulture);
        _cache.Add(style, properties);
        return properties;
    }
}

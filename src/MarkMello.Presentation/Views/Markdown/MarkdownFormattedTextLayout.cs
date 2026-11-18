using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Lays out one styled text fragment: display segments (see
/// <see cref="MarkdownDisplayLayoutModel"/>) are turned into text runs, the
/// formatter produces visual lines, and the canonical ↔ display caret maps make
/// selection, caret hit-testing and code boxes work on document offsets.
/// </summary>
internal sealed class MarkdownFormattedTextLayout : IDisposable
{
    private readonly MarkdownDisplayLayoutModel _displayModel;
    private readonly MarkdownInlineCodePadMetrics _codePadMetrics;
    private readonly List<FormattedLine> _lines = new();

    public MarkdownFormattedTextLayout(
        MarkdownStyledText styledText,
        IReadOnlyDictionary<int, MarkdownInlineImageState>? inlineImages,
        FontFamily baseFontFamily,
        FontFamily inlineCodeFontFamily,
        double baseFontSize,
        FontWeight baseFontWeight,
        FontStyle baseFontStyle,
        double lineHeight,
        double letterSpacing,
        TextWrapping textWrapping,
        double maxWidth,
        IBrush foreground,
        TextDecorationCollection? linkDecorations)
    {
        _displayModel = MarkdownDisplayLayoutModel.Create(styledText);
        var textProperties = new MarkdownTextRunPropertiesFactory(
            baseFontFamily,
            inlineCodeFontFamily,
            baseFontSize,
            baseFontWeight,
            baseFontStyle,
            foreground,
            linkDecorations);
        var padMetrics = MarkdownInlineCodePadMetrics.Create(
            inlineCodeFontFamily,
            baseFontSize * MarkdownTextRunPropertiesFactory.InlineCodeFontScale,
            baseFontWeight,
            baseFontStyle,
            foreground);
        _codePadMetrics = padMetrics;
        var imageMetrics = MarkdownInlineImageMetrics.Create(
            baseFontFamily,
            baseFontSize,
            baseFontWeight,
            baseFontStyle,
            lineHeight,
            foreground);
        var mathMetrics = MarkdownMathMetrics.Create(baseFontFamily, baseFontSize, foreground, lineHeight);
        var source = new MarkdownTextSource(
            _displayModel,
            styledText.Images,
            styledText.Math,
            inlineImages ?? EmptyInlineImages,
            textProperties,
            padMetrics,
            imageMetrics,
            mathMetrics,
            maxWidth);
        var paragraphProperties = new GenericTextParagraphProperties(
            new GenericTextRunProperties(
                new Typeface(baseFontFamily, baseFontStyle, baseFontWeight),
                baseFontSize,
                textDecorations: null,
                foreground,
                backgroundBrush: null,
                BaselineAlignment.Baseline,
                CultureInfo.CurrentUICulture),
            TextAlignment.Left,
            textWrapping,
            lineHeight,
            letterSpacing);

        BuildLines(source, paragraphProperties);
    }

    public double WidthIncludingTrailingWhitespace { get; private set; }

    public double Height { get; private set; }

    public int CanonicalLength => _displayModel.CanonicalLength;

    public IReadOnlyList<MarkdownDisplayCodeBox> CodeBoxes => _displayModel.CodeBoxes;

    public IReadOnlyList<MarkdownFormattedTextLineMetrics> GetLineMetrics()
    {
        if (_lines.Count == 0)
        {
            return Array.Empty<MarkdownFormattedTextLineMetrics>();
        }

        var result = new List<MarkdownFormattedTextLineMetrics>(_lines.Count);
        foreach (var line in _lines)
        {
            result.Add(new MarkdownFormattedTextLineMetrics(
                new Rect(
                    0,
                    line.Y,
                    Math.Max(1, line.TextLine.WidthIncludingTrailingWhitespace),
                    Math.Max(1, line.TextLine.Height))));
        }

        return result;
    }

    public void Draw(DrawingContext context)
    {
        foreach (var line in _lines)
        {
            line.TextLine.Draw(context, new Point(0, line.Y));
        }
    }

    public IReadOnlyList<Rect> GetCodeBoxRects(MarkdownDisplayCodeBox box)
    {
        if (box.DisplayLength <= 0 || _lines.Count == 0)
        {
            return Array.Empty<Rect>();
        }

        var displayEnd = box.DisplayStart + box.DisplayLength;
        var rects = new List<Rect>();

        foreach (var line in _lines)
        {
            var lineStart = line.TextLine.FirstTextSourceIndex;
            var lineEnd = lineStart + line.TextLine.Length;
            var overlapStart = Math.Max(box.DisplayStart, lineStart);
            var overlapEnd = Math.Min(displayEnd, lineEnd);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            foreach (var bounds in line.TextLine.GetTextBounds(overlapStart, overlapEnd - overlapStart))
            {
                var centeredY = line.Y + Math.Max(0, (line.TextLine.Height - _codePadMetrics.Height) / 2);
                rects.Add(new Rect(
                    bounds.Rectangle.X,
                    centeredY,
                    bounds.Rectangle.Width,
                    _codePadMetrics.Height));
            }
        }

        return rects;
    }

    public IReadOnlyList<Rect> GetSelectionRects(DocumentTextRange canonicalRange)
    {
        var canonicalStart = Math.Clamp(canonicalRange.Start, 0, CanonicalLength);
        var canonicalEnd = Math.Clamp(canonicalRange.End, canonicalStart, CanonicalLength);
        if (canonicalEnd <= canonicalStart)
        {
            return Array.Empty<Rect>();
        }

        var displayStart = _displayModel.GetDisplayStartForCanonicalCaret(canonicalStart);
        var displayEnd = _displayModel.GetDisplayEndForCanonicalCaret(canonicalEnd);
        return displayEnd <= displayStart
            ? Array.Empty<Rect>()
            : CollectDisplayRects(displayStart, displayEnd - displayStart);
    }

    public int GetCanonicalCaretOffset(Point point)
    {
        if (_lines.Count == 0)
        {
            return 0;
        }

        var lineIndex = FindLineIndex(point.Y);
        var line = _lines[lineIndex];
        var localX = Math.Clamp(point.X, 0, Math.Max(line.TextLine.WidthIncludingTrailingWhitespace, 0));
        var hit = line.TextLine.GetCharacterHitFromDistance(localX);
        var displayCaret = hit.FirstCharacterIndex + hit.TrailingLength;
        return _displayModel.GetCanonicalCaretForDisplayCaret(displayCaret);
    }

    private int GetLineCanonicalEnd(FormattedLine line)
    {
        var lineEnd = line.TextLine.FirstTextSourceIndex + line.TextLine.Length;
        return _displayModel.GetCanonicalCaretForDisplayCaret(lineEnd);
    }

    public bool IsPointInsideText(Point point)
    {
        if (_lines.Count == 0)
        {
            return false;
        }

        var lineIndex = FindLineIndex(point.Y);
        var line = _lines[lineIndex];
        return point.Y >= line.Y
            && point.Y <= line.Y + line.TextLine.Height
            && point.X >= 0
            && point.X <= line.TextLine.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// Canonical offset the reader points at. The caret comes from the text
    /// formatter; at or past the end of a line it has no character to hit and
    /// falls back to the line start, so that case maps to the end of the line's
    /// canonical range explicitly (ADR-0010: canonical offsets).
    /// </summary>
    public int GetCanonicalOffset(Point point)
    {
        var caret = GetCanonicalCaretOffset(point);
        if (_lines.Count == 0)
        {
            return caret;
        }

        var line = _lines[FindLineIndex(point.Y)];
        var nearLineEnd = point.X >= Math.Max(0, line.TextLine.WidthIncludingTrailingWhitespace - 1);
        return nearLineEnd && caret < GetLineCanonicalEnd(line) && point.X > 0
            ? GetLineCanonicalEnd(line)
            : caret;
    }

    /// <summary>
    /// True when <paramref name="point"/> is on a formula but to the right of the
    /// small box its first canonical character reports: the formula is painted
    /// once, at the start of its range, and the click must land there (ADR-0010).
    /// The point must also be inside the formula box horizontally — clicks on
    /// trailing text on the same line are not on the formula.
    /// </summary>
    public bool IsBeyondFormulaStart(MarkdownMathSpan formula, Point point)
    {
        if (_lines.Count == 0 || formula.Range.IsEmpty)
        {
            return false;
        }

        var displayStart = _displayModel.GetDisplayStartForCanonicalCaret(formula.Range.Start);
        foreach (var line in _lines)
        {
            var lineStart = line.TextLine.FirstTextSourceIndex;
            var lineEnd = lineStart + line.TextLine.Length;
            if (displayStart < lineStart || displayStart >= lineEnd)
            {
                continue;
            }

            foreach (var bounds in line.TextLine.GetTextBounds(displayStart, 1))
            {
                if (bounds.Rectangle.Width <= 1)
                {
                    continue;
                }

                var inY = point.Y >= line.Y && point.Y <= line.Y + line.TextLine.Height;
                var pastLeft = point.X > bounds.Rectangle.X + 1;
                var insideRight = point.X <= bounds.Rectangle.X + bounds.Rectangle.Width;
                return pastLeft && insideRight && inY;
            }
        }

        return false;
    }

    public void Dispose()
    {
        foreach (var line in _lines)
        {
            line.TextLine.Dispose();
        }

        _lines.Clear();
    }

    private void BuildLines(MarkdownTextSource source, TextParagraphProperties paragraphProperties)
    {
        if (_displayModel.DisplayLength == 0)
        {
            return;
        }

        var formatter = TextFormatter.Current;
        TextLineBreak? previousLineBreak = null;
        var index = 0;
        var y = 0d;

        while (index < _displayModel.DisplayLength)
        {
            var line = formatter.FormatLine(source, index, source.MaxWidth, paragraphProperties, previousLineBreak);
            if (line is null)
            {
                break;
            }

            _lines.Add(new FormattedLine(line, y));
            WidthIncludingTrailingWhitespace = Math.Max(WidthIncludingTrailingWhitespace, line.WidthIncludingTrailingWhitespace);
            y += line.Height;

            // Length already includes trailing newline TextSource positions; adding NewLineLength
            // skips the next visual line and breaks multiline selection hit-testing.
            var consumed = line.Length;
            if (consumed <= 0)
            {
                break;
            }

            index = line.FirstTextSourceIndex + consumed;
            previousLineBreak = line.TextLineBreak;
        }

        Height = y;
    }

    private IReadOnlyList<Rect> CollectDisplayRects(int displayStart, int displayLength)
    {
        if (displayLength <= 0 || _lines.Count == 0)
        {
            return Array.Empty<Rect>();
        }

        var displayEnd = displayStart + displayLength;
        var rects = new List<Rect>();

        foreach (var line in _lines)
        {
            var lineStart = line.TextLine.FirstTextSourceIndex;
            var lineEnd = lineStart + line.TextLine.Length;
            var overlapStart = Math.Max(displayStart, lineStart);
            var overlapEnd = Math.Min(displayEnd, lineEnd);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            foreach (var bounds in line.TextLine.GetTextBounds(overlapStart, overlapEnd - overlapStart))
            {
                var rect = bounds.Rectangle.Translate(new Vector(0, line.Y));
                if (rect.Width <= 0)
                {
                    // Zero-width positions (the continuation characters of a
                    // formula, ADR-0010) report no box of their own; the
                    // formula they belong to is drawn inside this line, so
                    // the highlight covers the part of the line it stands for.
                    rect = new Rect(rect.X, line.Y, 1, line.TextLine.Height);
                }

                rects.Add(rect);
            }
        }

        return rects;
    }

    private int FindLineIndex(double y)
    {
        if (y <= 0)
        {
            return 0;
        }

        for (var index = 0; index < _lines.Count; index++)
        {
            var line = _lines[index];
            if (y < line.Y + line.TextLine.Height || index == _lines.Count - 1)
            {
                return index;
            }
        }

        return _lines.Count - 1;
    }

    private readonly record struct FormattedLine(TextLine TextLine, double Y);

    private static IReadOnlyDictionary<int, MarkdownInlineImageState> EmptyInlineImages { get; } =
        new Dictionary<int, MarkdownInlineImageState>();
}

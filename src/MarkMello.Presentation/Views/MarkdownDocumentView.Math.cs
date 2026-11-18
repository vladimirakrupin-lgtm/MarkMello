using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    /// <summary>
    /// Block-level display formula. The formula occupies its whole TeX range in
    /// the document text map, so the visual is registered as a selection
    /// fragment: selection, copy and search highlight it as one unit instead of
    /// letting it fall out of the document flow (ADR-0010).
    /// </summary>
    private StackPanel BuildMathBlock(MarkdownMathBlock block, string path)
    {
        var baseFontSize = ReadingPreferences.FontSize;
        var foreground = LookupBrush("MmTextBrush") ?? Brushes.Black;

        var layout = MarkdownMathRenderer.Measure(
            block.Source,
            baseFontSize,
            foreground,
            isDisplay: true);

        var host = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(0, 14, 0, 18),
            HorizontalAlignment = HorizontalAlignment.Center,
            UseLayoutRounding = true
        };

        if (!_textMap.TryGetFragment(path, out var fragment))
        {
            // No canonical range to mirror (the block is not part of the text
            // flow): keep the old non-selectable rendering.
            host.Children.Add(CreateMathBlockContent(block, layout, baseFontSize, foreground));
            return host;
        }

        // The block fragment carries the Pandoc display delimiters around the
        // TeX (ADR-0012); the measured box mirrors exactly that canonical text.
        if (string.Equals(fragment.Text, "\\[\n" + block.Source + "\n\\]", StringComparison.Ordinal))
        {
            var content = CreateMathBlockContent(block, layout, baseFontSize, foreground);
            var selection = new MarkdownMathSelectionFragment(
                content,
                Math.Max(1, layout?.Size.Width ?? baseFontSize * 2),
                Math.Max(1, layout?.Size.Height ?? baseFontSize))
            {
                DocumentRange = fragment.Range,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // The formula box is the only visual; it hosts the content for
            // measurement and carries the fragment itself for hit-testing.
            selection.ApplyThemeBrushes(
                LookupBrush("MmSelectionBrush") ?? LookupBrush("MmAccentSoftBrush"),
                LookupBrush("MmAccentBrush"),
                LookupBrush("MmFindHighlightBrush"),
                LookupBrush("MmFindActiveBrush"),
                LookupBrush("MmAccentBrush"));

            RegisterSelectionFragment(selection, path);
            selection.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
            host.Children.Add(selection);
            return host;
        }

        // A longer canonical text (for example a one-line $$...$$ kept inside a
        // paragraph) cannot be mirrored by the measured box alone: render the
        // source as selectable text so every canonical character stays mapped.
        var textFragment = new MarkdownSelectionTextFragment
        {
            StyledText = MarkdownStyledText.FromInlines([new MarkdownTextInline(fragment.Text)]),
            DocumentRange = fragment.Range,
            BaseFontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
            BaseFontSize = Math.Max(12, baseFontSize - 2),
            BaseFontWeight = FontWeight.Normal,
            BaseFontStyle = FontStyle.Normal,
            BaseLineHeight = GetBodyLineHeight(),
            InlineCodeFontFamily = ResolveMonoFontFamily(),
            LayoutTextWrapping = TextWrapping.Wrap
        };

        host.HorizontalAlignment = HorizontalAlignment.Stretch;
        RegisterSelectionFragment(textFragment, path);
        textFragment.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
        host.Children.Add(textFragment);
        return host;
    }

    private static Control CreateMathBlockContent(
        MarkdownMathBlock block,
        MarkdownMathLayout? layout,
        double baseFontSize,
        IBrush foreground)
    {
        if (layout is { IsValid: true } valid)
        {
            return new MarkdownMathBlockVisual(
                block.Source,
                baseFontSize,
                foreground,
                valid.Size);
        }

        // Owner decision: on invalid LaTeX show the source instead of an
        // error ornament, so the author never loses the formula text.
        return new SelectableTextBlock
        {
            Text = block.Source,
            FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
            FontSize = Math.Max(12, baseFontSize - 2),
            TextWrapping = TextWrapping.Wrap,
            UseLayoutRounding = true
        };
    }
}

/// <summary>
/// Block-level display-formula visual. Measures once at construction; the
/// document view rebuilds on typography/theme changes, which re-measures.
/// </summary>
internal sealed class MarkdownMathBlockVisual : Control
{
    private readonly string _latex;
    private readonly double _fontSize;
    private readonly IBrush _foreground;
    private readonly Size _size;

    public MarkdownMathBlockVisual(string latex, double fontSize, IBrush foreground, Size size)
    {
        _latex = latex;
        _fontSize = fontSize;
        _foreground = foreground;
        _size = size;
        HorizontalAlignment = HorizontalAlignment.Center;
        UseLayoutRounding = true;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(Math.Min(_size.Width, double.IsFinite(availableSize.Width) ? availableSize.Width : _size.Width), _size.Height);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = double.IsFinite(Bounds.Width) ? Bounds.Width : _size.Width;
        var origin = new Point(Math.Max(0, (width - _size.Width) / 2), 0);
        MarkdownMathRenderer.Draw(
            context,
            _latex,
            _size,
            origin,
            _fontSize,
            _foreground,
            isDisplay: true);
    }
}

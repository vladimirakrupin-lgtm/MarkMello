using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Regressions for the formula coordinate model (ADR-0010): a formula keeps its
/// full TeX in the canonical text, so the document text map and the styled text
/// share offsets, and the block formula behaves as one selectable unit.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownMathSelectionTests
{
    private const string ParagraphBefore = "A ";
    private const string Formula = "x^2";
    private const string ParagraphAfter = " B";
    private const string InlineText = ParagraphBefore + Formula + ParagraphAfter;

    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownMathSelectionTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task TextMapAndStyledTextShareFormulaOffsets()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(InlineDocument());
            var paragraph = GetParagraph(view);

            Assert.Equal(InlineText, MarkdownDocumentTextMap.Create(view.Document!).Text.TrimEnd('\n'));
            Assert.Equal(InlineText, paragraph.StyledText.Text);
            Assert.Equal(paragraph.DocumentRange.Length, paragraph.StyledText.Text.Length);
            Assert.Equal(0, paragraph.DocumentRange.Start);

            var formulaStart = InlineText.IndexOf(Formula, StringComparison.Ordinal);
            var layout = GetTextLayout(paragraph);
            var formulaRects = layout.GetSelectionRects(new DocumentTextRange(formulaStart, formulaStart + Formula.Length));
            Assert.NotEmpty(formulaRects);
            var formulaRect = formulaRects[0];
            Assert.True(formulaRect.Width > 1, "formula must paint wider than a 1px caret box");

            // A point inside the painted formula box resolves onto the formula.
            Assert.True(
                layout.IsBeyondFormulaStart(
                    paragraph.StyledText.Math[0],
                    new Point(formulaRect.X + formulaRect.Width - 1, formulaRect.Y + 1)),
                "a point inside the painted formula must resolve onto the formula");

            // A point clearly inside the trailing text (center of the space
            // after the formula) is not on the formula, so clicks there must
            // not snap to the formula start.
            var afterFormula = layout.GetSelectionRects(new DocumentTextRange(formulaStart + Formula.Length, formulaStart + Formula.Length + 1));
            Assert.NotEmpty(afterFormula);
            var afterRect = afterFormula[0];
            var trailingPoint = new Point(afterRect.X + afterRect.Width / 2, afterRect.Y + 1);
            Assert.True(
                layout.IsBeyondFormulaStart(paragraph.StyledText.Math[0], trailingPoint) is false,
                "the point inside the trailing text is not on the formula");
        }, CancellationToken.None);

    [Fact]
    public Task SelectingOnlyInlineFormulaCopiesItsTex()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(InlineDocument());
            var start = InlineText.IndexOf(Formula, StringComparison.Ordinal);

            view.SelectRange(new DocumentTextRange(start, start + Formula.Length));

            Assert.Equal(Formula, view.SelectedText);
        }, CancellationToken.None);

    [Fact]
    public Task SelectingAcrossInlineFormulaIncludesItOnce()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(InlineDocument());
            var start = ParagraphBefore.Length;
            var end = start + Formula.Length + ParagraphAfter.Length;

            view.SelectRange(new DocumentTextRange(start, end));

            Assert.Equal(Formula + ParagraphAfter, view.SelectedText);
        }, CancellationToken.None);

    [Fact]
    public Task SearchHighlightCoversWholeInlineFormula()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(InlineDocument());
            var paragraph = GetParagraph(view);
            var formulaStart = InlineText.IndexOf(Formula, StringComparison.Ordinal);

            view.ApplySearchQuery(Formula);

            Assert.Equal(1, view.MatchCount);
            var highlight = Assert.Single(paragraph.SearchHighlightRanges);
            Assert.Equal(new DocumentTextRange(formulaStart, formulaStart + Formula.Length), highlight);
            Assert.Equal(highlight, paragraph.ActiveSearchHighlight);

            var layout = GetTextLayout(paragraph);
            var rects = layout.GetSelectionRects(
                new DocumentTextRange(highlight.Start - paragraph.DocumentRange.Start, highlight.End - paragraph.DocumentRange.Start));
            Assert.Contains(rects, rect => rect.Width > 0);
            Assert.All(rects, rect => Assert.True(rect.Height > 0, "search highlight must have a height"));
        }, CancellationToken.None);

    [Fact]
    public Task SearchMatchAtFragmentTailHighlightsFormulaDrawnRightBeforeIt()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(new RenderedMarkdownDocument(
            [
                Paragraph([Text(ParagraphBefore), InlineMath(Formula)]),
            ]));
            var paragraph = GetParagraph(view);

            view.ApplySearchQuery(Formula);

            Assert.Equal(1, view.MatchCount);
            var highlight = Assert.Single(paragraph.SearchHighlightRanges);
            var formulaStart = paragraph.DocumentRange.Start + ParagraphBefore.Length;
            Assert.Equal(new DocumentTextRange(formulaStart, formulaStart + Formula.Length), highlight);
            Assert.Equal(highlight, paragraph.ActiveSearchHighlight);

            var layout = GetTextLayout(paragraph);
            var rects = layout.GetSelectionRects(
                new DocumentTextRange(ParagraphBefore.Length, ParagraphBefore.Length + Formula.Length));
            Assert.Contains(rects, rect => rect.Width > 0);
            Assert.All(rects, rect => Assert.True(rect.Height > 0, "search highlight must have a height"));
        }, CancellationToken.None);

    [Fact]
    public Task BlockFormulaIsASelectionFragmentMirroringItsSource()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(new RenderedMarkdownDocument(
            [
                new MarkdownMathBlock(BlockFormula),
            ]));

            var fragment = view.GetVisualDescendants().OfType<MarkdownMathSelectionFragment>().Single();
            var mapText = MarkdownDocumentTextMap.Create(view.Document!).Text;

            Assert.Equal(BlockFormula, mapText[fragment.DocumentRange.Start..fragment.DocumentRange.End]);
            Assert.True(fragment.Bounds.Width > 0);
            Assert.True(fragment.Bounds.Height > 0);

            // Granularity decision: the whole formula is one unit.
            var inside = new Point(fragment.Bounds.Width / 2, fragment.Bounds.Height / 2);
            Assert.Equal(fragment.DocumentRange.Start, fragment.GetDocumentOffset(inside));
            Assert.Equal(fragment.DocumentRange, fragment.GetDocumentWordRange(inside));
        }, CancellationToken.None);

    [Fact]
    public Task SelectingBlockFormulaCopiesItsSource()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(new RenderedMarkdownDocument(
            [
                new MarkdownMathBlock(BlockFormula),
            ]));

            var fragment = view.GetVisualDescendants().OfType<MarkdownMathSelectionFragment>().Single();
            view.SelectRange(fragment.DocumentRange);

            Assert.Equal(BlockFormula, view.SelectedText);
        }, CancellationToken.None);

    [Fact]
    public Task InvalidBlockFormulaKeepsSourceAsSelectableText()
        => _fixture.Session.Dispatch(() =>
        {
            var (view, _) = CreateAttachedView(new RenderedMarkdownDocument(
            [
                new MarkdownMathBlock(@"\undefinedcommand{x}"),
            ]));

            Assert.Empty(view.GetVisualDescendants().OfType<MarkdownMathBlockVisual>());
            var fallback = view.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
            Assert.Contains("undefinedcommand", fallback.Text ?? string.Empty, StringComparison.Ordinal);
            Assert.Single(view.GetVisualDescendants().OfType<MarkdownMathSelectionFragment>());
        }, CancellationToken.None);

    [Fact]
    public Task SearchInsideBlockFormulaHighlightsAndScrollsToIt()
        => _fixture.Session.Dispatch(() =>
        {
            EnsureScrollableWindowTheme();

            var view = CreateView(new RenderedMarkdownDocument(
            [
                new MarkdownParagraphBlock([new MarkdownTextInline("intro paragraph")]),
                new MarkdownMathBlock(@"R = \frac{target}{b}"),
                new MarkdownParagraphBlock([new MarkdownTextInline("outro")]),
            ]));
            var scrollViewer = new ScrollViewer { Content = view };
            var window = new Window { Width = 600, Height = 60, Content = scrollViewer };
            window.Show();
            window.UpdateLayout();

            Assert.True(scrollViewer.ScrollBarMaximum.Y > 0);
            Assert.Equal(0, scrollViewer.Offset.Y);

            view.ApplySearchQuery("target");

            Assert.Equal(1, view.MatchCount);
            var fragment = view.GetVisualDescendants().OfType<MarkdownMathSelectionFragment>().Single();
            var highlight = Assert.Single(fragment.SearchHighlightRanges);
            Assert.Equal(highlight, fragment.ActiveSearchHighlight);
            Assert.Equal(highlight, fragment.DocumentRange.Intersection(highlight));
            Assert.True(scrollViewer.Offset.Y > 0);
        }, CancellationToken.None);

    /// <summary>
    /// The headless app has no styles, so a ScrollViewer cannot even lay out or
    /// scroll. The base Fluent theme (with its ScrollViewer template) is added
    /// once per session; MarkMello's own theme resources are not needed to
    /// measure and scroll the document.
    /// </summary>
    private static void EnsureScrollableWindowTheme()
    {
        var app = Avalonia.Application.Current!;
        if (app.Styles.OfType<Avalonia.Styling.Styles>().Any())
        {
            return;
        }

        var styles = new Avalonia.Styling.Styles();
        styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        app.Styles.Add(styles);
    }

    private const string BlockFormula = @"R = \frac{a}{b}";

    private static RenderedMarkdownDocument InlineDocument()
        => new(
        [
            Paragraph([Text(ParagraphBefore), InlineMath(Formula), Text(ParagraphAfter)]),
        ]);

    private static MarkdownMathInline InlineMath(string content) => new(content, IsDisplay: false);

    private static MarkdownTextInline Text(string text) => new(text);

    private static MarkdownParagraphBlock Paragraph(IReadOnlyList<MarkdownInline> inlines) => new(inlines);

    private static MarkdownDocumentView CreateView(RenderedMarkdownDocument document)
        => new()
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default,
        };

    /// <summary>
    /// Same host setup the other document-view math tests use: the view is the
    /// window content, so showing and laying out the window runs a full layout
    /// pass and materialises the text fragments' layouts.
    /// </summary>
    private static (MarkdownDocumentView View, Window Window) CreateAttachedView(RenderedMarkdownDocument document)
    {
        var view = CreateView(document);
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        window.UpdateLayout();
        return (view, window);
    }

    private static MarkdownSelectionTextFragment GetParagraph(MarkdownDocumentView view)
        => view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();

    private static MarkdownFormattedTextLayout GetTextLayout(MarkdownSelectionTextFragment fragment)
    {
        var field = typeof(MarkdownSelectionTextFragment).GetField(
            "_textLayout",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var layout = (MarkdownFormattedTextLayout?)field!.GetValue(fragment);
        Assert.NotNull(layout);
        return layout;
    }
}

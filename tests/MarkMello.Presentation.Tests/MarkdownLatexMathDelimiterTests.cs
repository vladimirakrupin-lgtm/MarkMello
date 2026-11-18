using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// ADR-0012: Pandoc-style <c>\(...\)</c> / <c>\[...\]</c> delimiters reach the
/// same math nodes as the dollar forms, while code and dangling delimiters stay
/// plain text.
/// </summary>
public sealed class MarkdownLatexMathDelimiterTests
{
    private static RenderedMarkdownDocument Render(string markdown)
        => new MarkdigMarkdownDocumentRenderer().Render(markdown);

    [Fact]
    public void InlineDelimiterBecomesInlineMath()
    {
        var document = Render(@"Легирование \(N_D \approx 10^{16}\) задано.");

        var paragraph = Assert.IsType<MarkdownParagraphBlock>(Assert.Single(document.Blocks));
        var math = Assert.Single(paragraph.Inlines.OfType<MarkdownMathInline>());
        Assert.Equal(@"N_D \approx 10^{16}", math.Content);
        Assert.False(math.IsDisplay);
        Assert.DoesNotContain(paragraph.Inlines.OfType<MarkdownTextInline>(), text =>
            text.Text.Contains(@"\(", StringComparison.Ordinal));
    }

    [Fact]
    public void DisplayDelimiterBecomesMathBlock()
    {
        const string markdown = """
            Текст до.

            \[
            R_s =
            \frac{n_sp_s-n_i^2}
            {\dfrac{n_s+n_{1s}}{S_p}
            +\dfrac{p_s+p_{1s}}{S_n}}
            \]

            Текст после.
            """;

        var document = Render(markdown);

        Assert.Collection(
            document.Blocks,
            block => Assert.IsType<MarkdownParagraphBlock>(block),
            block =>
            {
                var math = Assert.IsType<MarkdownMathBlock>(block);
                Assert.StartsWith(@"R_s =", math.Source, StringComparison.Ordinal);
                Assert.Contains(@"\dfrac", math.Source, StringComparison.Ordinal);
                Assert.DoesNotContain(@"\[", math.Source, StringComparison.Ordinal);
                Assert.NotNull(math.SourceSpan);
            },
            block => Assert.IsType<MarkdownParagraphBlock>(block));
    }

    [Fact]
    public void InlineDelimiterInsideTableCellStaysInlineMath()
    {
        const string markdown = """
            | Поле | Значение |
            |---|---|
            | \(S_n\) | \(3.4\times10^4\) см/с |
            """;

        var table = Assert.IsType<MarkdownTableBlock>(Assert.Single(Render(markdown).Blocks));
        var math = table.Rows[0][0].Inlines.OfType<MarkdownMathInline>().Single();
        Assert.Equal("S_n", math.Content);
    }

    [Fact]
    public void EscapedAndCodeDelimitersStayPlainText()
    {
        const string markdown = "Код `\\(x\\)` и \\\\(esc\\) и фенс:\n\n```\n\\(y\\)\n```\n";

        var document = Render(markdown);

        Assert.Empty(document.Blocks.OfType<MarkdownMathBlock>());
        var paragraph = Assert.IsType<MarkdownParagraphBlock>(document.Blocks[0]);
        Assert.DoesNotContain(paragraph.Inlines, inline => inline is MarkdownMathInline);
        var code = Assert.IsType<MarkdownCodeBlock>(document.Blocks[1]);
        Assert.Contains(@"\(y\)", code.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void DanglingInlineDelimiterStaysPlainText()
    {
        var paragraph = Assert.IsType<MarkdownParagraphBlock>(
            Assert.Single(Render(@"Открытая формула \(N_D без закрытия.").Blocks));

        Assert.DoesNotContain(paragraph.Inlines, inline => inline is MarkdownMathInline);
    }

    [Fact]
    public void UnclosedDisplayDelimiterStaysPlainText()
    {
        const string markdown = """
            Текст.

            \[
            a + b

            Без закрытия.
            """;

        var document = Render(markdown);

        Assert.Empty(document.Blocks.OfType<MarkdownMathBlock>());
    }

    [Fact]
    public void FencedCodeInsideDisplayBodyDoesNotCloseIt()
    {
        const string markdown = """
            \[
            a + b
            ```
            \]
            ```
            \]
            """;

        var math = Assert.IsType<MarkdownMathBlock>(Assert.Single(Render(markdown).Blocks));
        Assert.Contains(@"\]", math.Source, StringComparison.Ordinal);
        Assert.EndsWith("```", math.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void TableLineBreakDelimiterIsNotADisplayBlock()
    {
        const string markdown = """
            | A | B |
            |---|---|
            | \(a\) | \(b\) |
            """;

        var table = Assert.IsType<MarkdownTableBlock>(Assert.Single(Render(markdown).Blocks));
        Assert.Single(table.Rows);
        Assert.Empty(table.Rows[0][0].Inlines.OfType<MarkdownLineBreakInline>());
    }

    [Fact]
    public void TextMapKeepsTexWithPandocDelimiters()
    {
        // ADR-0012: the canonical text keeps \(...\) around the TeX next to the
        // $...$ form, so copied text carries its markup.
        var document = Render(@"Значение \(x^2\) и $y$ здесь.");

        var text = MarkdownDocumentTextMap.Create(document).Text;

        Assert.Contains(@"\(x^2\)", text, StringComparison.Ordinal);
        Assert.Contains(@"\(y\)", text, StringComparison.Ordinal);
    }
}

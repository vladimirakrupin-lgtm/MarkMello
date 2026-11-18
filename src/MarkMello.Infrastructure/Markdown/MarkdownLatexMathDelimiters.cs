namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Rewrites Pandoc-style LaTeX math delimiters (<c>\(...\)</c> and <c>\[...\]</c>)
/// in a working copy of the markdown source, so that Markdig's dollar-only math
/// extension sees them as ordinary math.
///
/// Why a pre-parse stage and not a post-AST pass: Markdig treats <c>\(</c>,
/// <c>\)</c>, <c>\[</c> and <c>\]</c> as escaped punctuation and hands the
/// parser a bare <c>(</c> / <c>)</c>. By the time the AST exists the backslash
/// is gone, so <c>\(x\)</c> and a literal <c>(x)</c> are indistinguishable.
///
/// Inline formulas become <c>$...$</c> (the only form Markdig keeps inline in
/// list items and table cells); display formulas become a standalone
/// <c>$$</c> block. All prose stays on its original lines, so the line numbers
/// used for <see cref="Domain.MarkdownSourceSpan"/> remain valid.
///
/// Code contexts (inline code spans, fenced and indented code blocks, HTML
/// comments) are left untouched, and <c>\\(</c> / <c>\\[</c> stay literal.
/// </summary>
internal static partial class MarkdownLatexMathDelimiters
{
    private const char Escape = '\\';

    /// <summary>
    /// Returns a copy of <paramref name="markdown"/> in which <c>\(...\)</c>
    /// became inline math and <c>\[...\]</c> blocks became Markdig display
    /// blocks.
    /// </summary>
    public static string Prepare(string markdown)
    {
        if (markdown.IndexOf('\\', StringComparison.Ordinal) < 0)
        {
            return markdown;
        }

        var lines = SplitLines(markdown);
        var code = new ScanState(markdown, lines);
        var edits = new List<DelimiterEdit>();
        var cursor = 0;

        while (cursor < markdown.Length)
        {
            if (code.TrySkip(cursor, out var protectedEnd) && protectedEnd > cursor)
            {
                cursor = protectedEnd;
                continue;
            }

            if (markdown[cursor] != Escape || cursor + 1 >= markdown.Length)
            {
                cursor++;
                continue;
            }

            var next = markdown[cursor + 1];
            if (next == '(' && TryReadInline(markdown, cursor, out var inlineEdit, out var inlineEnd))
            {
                edits.Add(inlineEdit);
                cursor = inlineEnd;
                continue;
            }

            // \[ on its own line is a display formula; inside a table row or in
            // running text it means a LaTeX line break.
            if (next == '[' && code.IsDisplayOpener(cursor)
                && TryReadDisplayBlock(markdown, code, cursor, out var displayEdit, out var displayEnd))
            {
                edits.Add(displayEdit);
                cursor = displayEnd;
                continue;
            }

            cursor += 2;
        }

        return edits.Count == 0 ? markdown : Apply(markdown, edits);
    }

    /// <summary>
    /// One rewrite of the working copy: characters in <c>[Start, End)</c> are
    /// replaced by <see cref="Replacement"/>.
    /// </summary>
    internal readonly record struct DelimiterEdit(int Start, int End, string Replacement);
}

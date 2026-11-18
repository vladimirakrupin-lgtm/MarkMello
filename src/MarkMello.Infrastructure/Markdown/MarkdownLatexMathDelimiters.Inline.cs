namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Inline half of <see cref="MarkdownLatexMathDelimiters"/>: <c>\(...\)</c>.
/// </summary>
internal static partial class MarkdownLatexMathDelimiters
{
    /// <summary>
    /// Reads an inline formula that opens at <paramref name="open"/>.
    /// <c>\(...\)</c> becomes a single-dollar <c>$...$</c> formula: that is the
    /// only inline form Markdig keeps inline inside list items and table cells
    /// (a <c>$$</c> inline there is pulled apart by the block parser). The
    /// formula must close on the same line, so a dangling <c>\(</c> never
    /// swallows the rest of the paragraph.
    /// </summary>
    private static bool TryReadInline(
        string markdown,
        int open,
        out DelimiterEdit edit,
        out int end)
    {
        edit = default;
        end = open;
        var close = FindInlineClose(markdown, open + 2);
        if (close < 0)
        {
            return false;
        }

        var content = markdown[(open + 2)..close];
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        edit = new DelimiterEdit(open, close + 2, "$" + content.Trim() + "$");
        end = close + 2;
        return true;
    }

    private static int FindInlineClose(string markdown, int start)
    {
        for (var i = start; i < markdown.Length; i++)
        {
            var c = markdown[i];
            if (c == '\n' || c == '\r')
            {
                return -1;
            }

            if (c == Escape && i + 1 < markdown.Length)
            {
                if (markdown[i + 1] == ')')
                {
                    return i;
                }

                // Skip the escaped character: \( stays a delimiter, \\) stays
                // literal and never closes the formula.
                i++;
            }
        }

        return -1;
    }
}

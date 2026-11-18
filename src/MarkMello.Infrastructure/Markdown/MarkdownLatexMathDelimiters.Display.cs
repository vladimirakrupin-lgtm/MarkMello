using System.Text;

namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Display half of <see cref="MarkdownLatexMathDelimiters"/>: <c>\[...\]</c>.
/// </summary>
internal static partial class MarkdownLatexMathDelimiters
{
    /// <summary>
    /// Reads a display formula whose <c>\[</c> opens a line on
    /// <paramref name="open"/>. <c>\[</c> becomes <c>$$</c> (the formula body
    /// starts on the next line) and the closing <c>\]</c> becomes <c>$$</c>, so
    /// Markdig sees the same multi-line shape as a native <c>$$</c> display
    /// block: line breaks inside the formula are preserved, and the block's
    /// line span still covers the original lines.
    /// <c>\]</c> must be the first token of its own line — a stray <c>\]</c>
    /// inside running text is not a delimiter.
    /// </summary>
    private static bool TryReadDisplayBlock(
        string markdown,
        ScanState code,
        int open,
        out DelimiterEdit edit,
        out int end)
    {
        edit = default;
        end = open;

        var openLine = open + 2;
        if (!IsRestOfLineBlank(markdown, openLine))
        {
            return false;
        }

        var close = FindDisplayClose(markdown, code, openLine);
        if (close < 0)
        {
            return false;
        }

        var body = markdown[openLine..close];
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        // The body starts right after \[ (usually on the next line); the
        // formula itself starts at its first non-blank character.
        var bodyStart = 0;
        while (bodyStart < body.Length && (body[bodyStart] == '\n' || body[bodyStart] == '\r'))
        {
            bodyStart++;
        }

        var replacement = new StringBuilder(close + 2 - open);
        // Blank lines separate the display block from the prose around it:
        // Markdig treats a non-blank paragraph line right after $$ as a lazy
        // continuation of the math block, which would swallow the next
        // paragraph or table.
        replacement.Append("$$\n").Append(body[bodyStart..]).Append("\n$$\n");
        edit = new DelimiterEdit(open, close + 2, replacement.ToString());
        end = close + 2;
        return true;
    }

    private static bool IsRestOfLineBlank(string markdown, int offset)
    {
        for (var i = offset; i < markdown.Length; i++)
        {
            var c = markdown[i];
            if (c == '\n')
            {
                return true;
            }

            if (c != ' ' && c != '\t' && c != '\r')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the closing <c>\]</c> on a line of its own, skipping blank lines
    /// and any line that belongs to a code block.
    /// </summary>
    private static int FindDisplayClose(string markdown, ScanState code, int afterOpening)
    {
        var lineEnd = afterOpening;
        while (true)
        {
            var nextLineStart = lineEnd + CountLineEnding(markdown, lineEnd);
            if (nextLineStart <= lineEnd || nextLineStart >= markdown.Length)
            {
                return -1;
            }

            var line = code.LineOf(nextLineStart);
            if (code.IsCodeLine(line))
            {
                // Skip the whole code run: a fence line hides everything up to
                // its closing fence, an indented line just itself.
                var skip = code.TrySkip(nextLineStart, out var blockEnd) && blockEnd > nextLineStart
                    ? blockEnd
                    : nextLineStart;
                lineEnd = Math.Max(lineEnd + 1, skip - 1);
                if (lineEnd >= markdown.Length)
                {
                    return -1;
                }

                continue;
            }

            var trimmed = TrimStart(markdown, nextLineStart);
            if (trimmed + 2 <= markdown.Length
                && markdown[trimmed] == '\\'
                && markdown[trimmed + 1] == ']'
                && IsRestOfLineBlank(markdown, trimmed + 2))
            {
                return trimmed;
            }

            lineEnd = NextLineEnd(markdown, nextLineStart);
        }
    }

    private static int TrimStart(string markdown, int offset)
    {
        while (offset < markdown.Length && (markdown[offset] == ' ' || markdown[offset] == '\t'))
        {
            offset++;
        }

        return offset;
    }

    private static int NextLineEnd(string markdown, int lineStart)
    {
        var newline = markdown.IndexOf('\n', lineStart);
        return newline < 0 ? markdown.Length : newline;
    }
}

using System.Text;

namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Shared helpers for <see cref="MarkdownLatexMathDelimiters"/>.
/// </summary>
internal static partial class MarkdownLatexMathDelimiters
{
    private static string[] SplitLines(string markdown)
        => markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static int CountLineEnding(string markdown, int index)
    {
        if (index >= markdown.Length)
        {
            return 0;
        }

        if (markdown[index] == '\n')
        {
            return 1;
        }

        return markdown[index] == '\r' && index + 1 < markdown.Length && markdown[index + 1] == '\n'
            ? 2
            : 0;
    }

    /// <summary>
    /// Applies non-overlapping, ordered edits on the offset grid of the
    /// working copy.
    /// </summary>
    private static string Apply(string markdown, List<DelimiterEdit> edits)
    {
        var builder = new StringBuilder(markdown.Length + (edits.Count * 8));
        var cursor = 0;
        foreach (var edit in edits)
        {
            if (edit.Start < cursor)
            {
                continue;
            }

            builder.Append(markdown, cursor, edit.Start - cursor);
            builder.Append(edit.Replacement);
            cursor = edit.End;
        }

        builder.Append(markdown, cursor, markdown.Length - cursor);
        return builder.ToString();
    }
}

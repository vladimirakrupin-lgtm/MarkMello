namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Rewrites <c>\boxed{X}</c> into <c>\overline{\underline{X}}</c>. The bundled
/// engine has no box primitive at all (<c>\boxed</c>, <c>\fbox</c> and
/// <c>\frame</c> all fail to parse), so without this rewrite a boxed result
/// formula falls back to raw source. The double rule above and below keeps the
/// visual emphasis of a box on the supported primitives.
///
/// Arguments are read with balanced braces, so nested groups and commands
/// survive; an unmatched <c>\boxed</c> is left untouched and hits the regular
/// invalid-formula fallback.
/// </summary>
internal static class MarkdownMathBoxedExpander
{
    public static string Expand(string latex)
    {
        if (latex.IndexOf(@"\boxed", StringComparison.Ordinal) < 0)
        {
            return latex;
        }

        var builder = new System.Text.StringBuilder(latex.Length + 32);
        var index = 0;
        while (index < latex.Length)
        {
            if (latex.AsSpan(index).StartsWith(@"\boxed", StringComparison.Ordinal)
                && TryReadGroup(latex, index + 6, out var content, out var end))
            {
                builder.Append(@"\overline{\underline{")
                    .Append(Expand(content))
                    .Append("}}");
                index = end;
                continue;
            }

            builder.Append(latex[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool TryReadGroup(string latex, int cursor, out string content, out int end)
    {
        content = string.Empty;
        end = cursor;

        while (cursor < latex.Length && char.IsWhiteSpace(latex[cursor]))
        {
            cursor++;
        }

        if (cursor >= latex.Length || latex[cursor] != '{')
        {
            return false;
        }

        var depth = 0;
        var start = cursor + 1;
        for (var i = cursor; i < latex.Length; i++)
        {
            switch (latex[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        content = latex[start..i];
                        end = i + 1;
                        return true;
                    }

                    break;
            }
        }

        return false;
    }
}

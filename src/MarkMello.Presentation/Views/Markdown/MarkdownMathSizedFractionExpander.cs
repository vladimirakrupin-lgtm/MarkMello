using System.Text;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Rewrites <c>\dfrac{a}{b}</c> into <c>{\displaystyle\frac{a}{b}}</c> and
/// <c>\tfrac{a}{b}</c> into <c>{\textstyle\frac{a}{b}}</c>. The engine has no
/// sized-fraction commands and would fall back to raw source; arguments are
/// read with balanced braces so nested fractions survive.
/// </summary>
internal static class MarkdownMathSizedFractionExpander
{
    public static string Expand(string latex)
    {
        if (latex.IndexOf(@"\dfrac", StringComparison.Ordinal) < 0
            && latex.IndexOf(@"\tfrac", StringComparison.Ordinal) < 0)
        {
            return latex;
        }

        var builder = new StringBuilder(latex.Length + 32);
        var index = 0;
        while (index < latex.Length)
        {
            if (TryReadSizedFraction(latex, index, out var consumed, out var numerator, out var denominator, out var style))
            {
                builder.Append('{').Append(style).Append(@"\frac{")
                    .Append(Expand(numerator))
                    .Append("}{")
                    .Append(Expand(denominator))
                    .Append("}}");
                index += consumed;
                continue;
            }

            builder.Append(latex[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool TryReadSizedFraction(
        string latex,
        int index,
        out int consumed,
        out string numerator,
        out string denominator,
        out string style)
    {
        consumed = 0;
        numerator = string.Empty;
        denominator = string.Empty;
        style = @"\displaystyle";

        if (!latex.AsSpan(index).StartsWith(@"\dfrac", StringComparison.Ordinal)
            && !latex.AsSpan(index).StartsWith(@"\tfrac", StringComparison.Ordinal))
        {
            return false;
        }

        style = latex.AsSpan(index).StartsWith(@"\tfrac", StringComparison.Ordinal)
            ? @"\textstyle"
            : @"\displaystyle";

        var cursor = index + 6; // skip "\dfrac" / "\tfrac"
        if (!TryReadGroup(latex, ref cursor, out numerator)
            || !TryReadGroup(latex, ref cursor, out denominator))
        {
            return false;
        }

        consumed = cursor - index;
        return true;
    }

    /// <summary>
    /// Reads one <c>{...}</c> argument at <paramref name="cursor"/>, skipping
    /// leading spaces and honouring nested braces. Returns the inner text.
    /// </summary>
    private static bool TryReadGroup(string latex, ref int cursor, out string content)
    {
        content = string.Empty;
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
                        cursor = i + 1;
                        return true;
                    }

                    break;
            }
        }

        return false;
    }
}

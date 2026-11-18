using System.Text;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Inserts a thin space around binary <c>+</c> / <c>−</c> whose neighbour is a
/// fraction-like group. The engine gives a binary operator only a thin medium
/// space, so with tall neighbours (two stacked fractions) the sign nearly
/// touches them; the inserted space keeps the standard TeX look without
/// changing simple cases such as <c>a + b</c>.
/// </summary>
internal static class MarkdownMathBinarySpacingExpander
{
    public static string Expand(string latex)
    {
        var builder = new StringBuilder(latex.Length + 16);
        for (var i = 0; i < latex.Length; i++)
        {
            var ch = latex[i];
            var isBinarySign = ch is '+' or '-' or '\u2212';
            if (isBinarySign && IsBinaryContext(latex, i) && NeedsFractionSpacing(latex, i))
            {
                builder.Append(@"\,\,");
                builder.Append(ch);
                builder.Append(@"\,\,");
                i = SkipFollowingSpaces(latex, i) - 1;
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A sign is binary when it has a token before it (not an opening brace or
    /// another operator) and a token after it.
    /// </summary>
    private static bool IsBinaryContext(string latex, int index)
    {
        var previous = PreviousNonSpace(latex, index);
        var next = SkipFollowingSpaces(latex, index);
        if (next >= latex.Length)
        {
            return false;
        }

        var previousChar = previous >= 0 ? latex[previous] : '\0';
        return previousChar is not ('{' or '[' or '(' or ',' or '\0')
            && previousChar is not ('+' or '-' or '*' or '/' or '=' or '<' or '>')
            && previousChar != '\u2212';
    }

    private static int PreviousNonSpace(string latex, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(latex[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int SkipFollowingSpaces(string latex, int index)
    {
        var i = index + 1;
        while (i < latex.Length && char.IsWhiteSpace(latex[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// True when either neighbour of the sign is a group that starts with a
    /// fraction (plain <c>\frac</c> or the <c>\displaystyle\frac</c> form the
    /// normalizer produces), i.e. a tall stack that the sign can touch.
    /// </summary>
    private static bool NeedsFractionSpacing(string latex, int index)
        => NeighbourIsFractionGroup(latex, index, lookRight: false)
            || NeighbourIsFractionGroup(latex, index, lookRight: true);

    private static bool NeighbourIsFractionGroup(string latex, int index, bool lookRight)
    {
        var cursor = lookRight ? SkipFollowingSpaces(latex, index) : PreviousNonSpace(latex, index);
        if (cursor < 0 || cursor >= latex.Length)
        {
            return false;
        }

        if (lookRight)
        {
            return StartsWithFraction(latex, cursor);
        }

        // Look left: the sign may follow "...}\frac{a}{b}", so walk back to the
        // opening brace of the group that ends right before the sign.
        if (latex[cursor] != '}')
        {
            return false;
        }

        var depth = 0;
        for (var i = cursor; i >= 0; i--)
        {
            if (latex[i] == '}')
            {
                depth++;
            }
            else if (latex[i] == '{')
            {
                depth--;
                if (depth == 0)
                {
                    // The group may be a bare argument of \frac; also check the
                    // fraction that owns it: "\frac{a}{b}" ends with this brace.
                    return StartsWithFraction(latex, SkipSpacesForward(latex, i + 1));
                }
            }
        }

        return false;
    }

    private static int SkipSpacesForward(string latex, int index)
    {
        var i = index;
        while (i < latex.Length && char.IsWhiteSpace(latex[i]))
        {
            i++;
        }

        return i;
    }

    private static bool StartsWithFraction(string latex, int index)
    {
        var span = latex.AsSpan(index);
        return span.StartsWith(@"\frac", StringComparison.Ordinal)
            || span.StartsWith(@"\displaystyle\frac", StringComparison.Ordinal)
            || span.StartsWith(@"\textstyle\frac", StringComparison.Ordinal)
            || span.StartsWith(@"\dfrac", StringComparison.Ordinal)
            || span.StartsWith(@"\tfrac", StringComparison.Ordinal)
            || span.StartsWith(@"\binom", StringComparison.Ordinal);
    }
}

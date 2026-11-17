namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Rewrites LaTeX constructs that the bundled CSharpMath engine does not
/// understand, or that it typesets too tightly compared with LaTeX, into
/// equivalent input that renders correctly:
///
/// <list type="bullet">
/// <item>Sized fractions (<c>\dfrac</c> / <c>\tfrac</c>) — see
/// <see cref="MarkdownMathSizedFractionExpander"/>.</item>
/// <item>Command aliases the engine does not define (<c>\lVert</c>,
/// <c>\rvert</c>, ...) — see <see cref="MarkdownMathCommandAliasExpander"/>.</item>
/// <item>Binary <c>+</c> / <c>−</c> next to a fraction — see
/// <see cref="MarkdownMathBinarySpacingExpander"/>.</item>
/// </list>
///
/// The normalizer is purely textual and deterministic, so its result can be
/// part of the measurement cache key (<see cref="MarkdownMathRenderer"/>).
/// </summary>
internal static class MarkdownMathTexNormalizer
{
    public static string Normalize(string latex)
    {
        if (string.IsNullOrEmpty(latex))
        {
            return latex;
        }

        var expanded = MarkdownMathSizedFractionExpander.Expand(latex);
        var aliased = MarkdownMathCommandAliasExpander.Expand(expanded);
        return MarkdownMathBinarySpacingExpander.Expand(aliased);
    }
}

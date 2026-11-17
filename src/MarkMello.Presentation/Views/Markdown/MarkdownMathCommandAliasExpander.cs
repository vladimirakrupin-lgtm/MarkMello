namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Rewrites LaTeX commands the engine does not define into the equivalent ones
/// it does. Both pairs are standard amsmath spellings: <c>\lVert</c>/<c>\rVert</c>
/// (norm bars) and <c>\lvert</c>/<c>\rvert</c> (absolute-value bars). Without
/// this a formula containing either falls back to raw source entirely.
/// </summary>
internal static class MarkdownMathCommandAliasExpander
{
    public static string Expand(string latex)
    {
        if (latex.IndexOf(@"\lVert", StringComparison.Ordinal) < 0
            && latex.IndexOf(@"\rVert", StringComparison.Ordinal) < 0
            && latex.IndexOf(@"\lvert", StringComparison.Ordinal) < 0
            && latex.IndexOf(@"\rvert", StringComparison.Ordinal) < 0)
        {
            return latex;
        }

        // Longest names first so \lVert is not partially matched by \lvert and
        // vice versa; the engine understands \Vert and \vert.
        return latex
            .Replace(@"\lVert", @"\Vert", StringComparison.Ordinal)
            .Replace(@"\rVert", @"\Vert", StringComparison.Ordinal)
            .Replace(@"\lvert", @"\vert", StringComparison.Ordinal)
            .Replace(@"\rvert", @"\vert", StringComparison.Ordinal);
    }
}

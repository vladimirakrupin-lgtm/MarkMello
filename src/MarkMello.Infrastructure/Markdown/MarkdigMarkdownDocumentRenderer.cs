using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using MarkdigMarkdown = Markdig.Markdown;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkMello.Application.Abstractions;
using MarkMello.Application.Diagrams;
using MarkMello.Domain;
using CodeBlock = Markdig.Syntax.CodeBlock;

namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Markdig-based parse layer for M3.
/// Даёт устойчивый AST CommonMark + common extensions, затем переводит его
/// в UI-agnostic document model.
/// </summary>
public sealed partial class MarkdigMarkdownDocumentRenderer : IMarkdownDocumentRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAlertBlocks()
        .Build();

    public RenderedMarkdownDocument Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return RenderedMarkdownDocument.Empty;
        }

        // Pandoc-style \(...\) / \[...\] delimiters are invisible to Markdig's
        // dollar math extension, and its escape pass strips the backslash, so
        // they are rewritten on a working copy whose line/offset grid matches
        // the original (ADR-0012).
        var working = MarkdownLatexMathDelimiters.Prepare(markdown);
        var document = MarkdigMarkdown.Parse(working, Pipeline);
        var blocks = ConvertBlocks(document, working);
        return new RenderedMarkdownDocument(blocks);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Selection fragment for a block-level formula. The whole block is one atomic
/// unit: the canonical TeX range is mirrored into the document text map, the
/// measured formula box is the only visual, and caret/selection granularity is
/// "the formula as a whole" (owner decision in ADR-0010) — a point inside the
/// box gives a caret at the range start and a double-click selects all of it.
///
/// Highlights are painted behind <see cref="Content"/> and use the existing
/// search/selection brushes only; no new visuals are introduced.
/// </summary>
internal sealed class MarkdownMathSelectionFragment : MarkdownDocumentSelectionFragmentBase
{
    private readonly double _formulaWidth;
    private readonly double _formulaHeight;
    private IBrush? _accentBrush;

    /// <summary>
    /// Creates the fragment with the control it draws (the formula visual or
    /// the invalid-LaTeX source fallback) as the only visual child.
    /// </summary>
    public MarkdownMathSelectionFragment(Control content, double formulaWidth, double formulaHeight)
    {
        ArgumentNullException.ThrowIfNull(content);
        VisualChildren.Add(content);
        LogicalChildren.Add(content);
        _formulaWidth = Math.Max(1, formulaWidth);
        _formulaHeight = Math.Max(1, formulaHeight);
        Focusable = false;
        ClipToBounds = false;
        UseLayoutRounding = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        ResourcesChanged += (_, _) => InvalidateVisual();
    }

    public Control Content => (Control)VisualChildren[0];

    public IBrush? SelectionBrush { get; private set; }

    public IBrush? SelectionBorderBrush { get; private set; }

    public IBrush? FindHighlightBrush { get; private set; }

    public IBrush? FindActiveBrush { get; private set; }

    /// <summary>
    /// Brushes stay optional so the block is still a plain selectable visual
    /// when the theme dictionary is not attached; the keys are the same ones
    /// the text fragments use.
    /// </summary>
    public void ApplyThemeBrushes(
        IBrush? selectionBrush,
        IBrush? selectionBorderBrush,
        IBrush? findHighlightBrush,
        IBrush? findActiveBrush,
        IBrush? accentBrush)
    {
        SelectionBrush = selectionBrush;
        SelectionBorderBrush = selectionBorderBrush;
        FindHighlightBrush = findHighlightBrush;
        FindActiveBrush = findActiveBrush;
        _accentBrush = accentBrush;
        InvalidateVisual();
    }

    public override int GetDocumentOffset(Point localPoint)
        => DocumentRange.Start;

    public override DocumentTextRange GetDocumentWordRange(Point localPoint)
        => localPoint.X >= 0 && localPoint.X <= Bounds.Width && localPoint.Y >= 0 && localPoint.Y <= Bounds.Height
            ? DocumentRange
            : DocumentTextRange.Empty;

    public override bool TryGetLinkAt(Point localPoint, out MarkdownLinkSpan linkSpan)
    {
        linkSpan = default;
        return false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? Math.Min(_formulaWidth, availableSize.Width) : _formulaWidth;
        Content.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, Math.Max(_formulaHeight, Content.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Content.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        var selection = DocumentRange.Intersection(SelectionRange);
        if (!selection.IsEmpty)
        {
            if (SelectionBrush is { } selectionFill)
            {
                context.FillRectangle(selectionFill, bounds, 2);
            }

            if (SelectionBorderBrush is { } selectionStroke)
            {
                context.DrawRectangle(null, new Pen(selectionStroke, 1), bounds.Deflate(0.5), 2, 2);
            }
        }

        if (FindHighlightBrush is { } findFill)
        {
            foreach (var range in SearchHighlightRanges)
            {
                if (range.Intersects(DocumentRange))
                {
                    context.FillRectangle(findFill, bounds, 2);
                }
            }
        }

        if (ActiveSearchHighlight is { } active
            && active.Intersects(DocumentRange)
            && FindActiveBrush is { } activeFill)
        {
            var pen = _accentBrush is { } accent ? new Pen(accent, 1) : null;
            context.DrawRectangle(activeFill, pen, bounds.Deflate(0.5), 2, 2);
        }
    }

    public override void Dispose()
    {
    }
}

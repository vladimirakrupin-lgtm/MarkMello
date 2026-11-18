using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// ADR-0011 §5: содержимое тултипа обрезанной формулы — отрендеренная формула
/// нормального размера (контрол, не LaTeX-текст — отклонённый вариант ADR).
/// Inline-вариант <see cref="MarkdownMathBlockVisual"/>: замер один раз при
/// создании, отрисовка MarkdownMathRenderer.Draw с isDisplay: false;
/// невалидный LaTeX показывается исходником моноширинным текстом
/// (устоявшийся фолбэк проекта, MarkdownDocumentView.Math.cs).
/// </summary>
internal sealed class MarkdownMathInlineVisual : Control
{
    private readonly string _latex;
    private readonly double _fontSize;
    private readonly IBrush _foreground;
    private readonly Size _size;
    private readonly MarkdownMathLayout? _layout;

    public MarkdownMathInlineVisual(string latex, double fontSize, IBrush foreground)
    {
        _latex = latex;
        _fontSize = Math.Max(10, fontSize);
        _foreground = foreground;
        _layout = MarkdownMathRenderer.Measure(_latex, _fontSize, foreground, isDisplay: false);
        _size = _layout is { IsValid: true } valid
            ? valid.Size
            : MeasureFallbackSize(_latex.Length == 0 ? " " : _latex);
        UseLayoutRounding = true;
    }

    /// <summary>Тултип существует ради полного размера формулы — обрезание
    /// в тултипе лишило бы его смысла.</summary>
    protected override Size MeasureOverride(Size availableSize) => _size;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_layout is { IsValid: true } layout)
        {
            MarkdownMathRenderer.Draw(
                context,
                _latex,
                layout.Size,
                new Point(0, 0),
                _fontSize,
                _foreground,
                isDisplay: false);
            return;
        }

        DrawSourceFallback(context);
    }

    private void DrawSourceFallback(DrawingContext context)
    {
        using var textLayout = new Avalonia.Media.TextFormatting.TextLayout(
            _latex,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            Math.Max(9, _fontSize * 0.85),
            _foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: Math.Max(1, _size.Width),
            maxHeight: Math.Max(1, _size.Height),
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        textLayout.Draw(context, new Point(0, 0));
    }

    private Size MeasureFallbackSize(string text)
    {
        using var probe = new Avalonia.Media.TextFormatting.TextLayout(
            text,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            Math.Max(9, _fontSize * 0.85),
            _foreground,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: null);

        return new Size(Math.Max(1, probe.Width), Math.Max(1, probe.Height));
    }
}

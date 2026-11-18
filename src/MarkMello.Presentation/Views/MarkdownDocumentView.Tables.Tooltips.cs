using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    /// <summary>
    /// ADR-0011 §5: тултипы на скрытых данных в скролл-режиме. Тултип —
    /// ускоритель, а не единственный путь к данным: скрытое всегда доступно
    /// горизонтальным скроллом, выделение и копирование сохраняются.
    /// Для формулы содержимое тултипа — отрендеренная формула нормального
    /// размера (не LaTeX-текст — отклонённый вариант ADR); для обрезаемого
    /// текста — полный текст ячейки. API — ToolTip.SetTip по образцу
    /// MarkdownImageView.
    /// </summary>
    private void AttachTableCellTooltips(MarkdownTableLayoutState state)
    {
        var step = MarkdownTableLayoutConstants.FontLadderMultipliers.Length - 1;
        var padding = 2 * MarkdownTableLayoutConstants.GetHorizontalPadding(step);
        var baseFontSize = ReadingPreferences.FontSize;
        var headerFontSize = MarkdownTableLayoutConstants.GetHeaderFontSize(step, baseFontSize);
        var bodyFontSize = MarkdownTableLayoutConstants.GetBodyFontSize(step, baseFontSize);

        foreach (var cell in state.Cells)
        {
            var appliedWidths = state.AppliedWidths;
            if (cell.Column < 0 || appliedWidths is null || cell.Column >= appliedWidths.Length)
            {
                ToolTip.SetTip(cell.Border, null);
                continue;
            }

            // Колонка в скролл-режиме = min-content самой широкой ячейки; тултип
            // получает ячейка, чей собственный контент шире своей колонки
            // (в первую очередь широкая формула в колонке с более узким текстом).
            var columnContentWidth = appliedWidths[cell.Column] - padding;
            var fontSize = cell.IsHeader ? headerFontSize : bodyFontSize;
            var fontWeight = cell.IsHeader ? FontWeight.SemiBold : FontWeight.Normal;
            var letterSpacing = (cell.IsHeader ? headerFontSize * 0.05 : 0) + ReadingPreferences.LetterSpacing;
            var lineHeight = cell.IsHeader
                ? Math.Max(headerFontSize * 1.45, headerFontSize + 4)
                : Math.Max(bodyFontSize * 1.55, bodyFontSize + 4);
            var foreground = cell.IsHeader
                ? LookupBrush("MmTextSoftBrush") ?? Brushes.Black
                : LookupBrush("MmTextBrush") ?? Brushes.Black;

            var measure = MeasureTableCell(
                cell.Cell, cell.IsHeader, state.SansFontFamily,
                fontSize, fontWeight, letterSpacing, lineHeight, foreground);

            ToolTip.SetTip(cell.Border,
                measure.MaxContentWidth > columnContentWidth
                    ? BuildTableCellTooltip(cell, baseFontSize)
                    : null);
        }
    }

    /// <summary>
    /// Содержимое тултипа ячейки (ADR-0011 §5): формулы — отрендеренные
    /// нормальным шрифтом; текст ячейки — полным текстом. LaTeX-источник
    /// формул в тултип не попадает (отклонённый вариант ADR).
    /// </summary>
    private Control BuildTableCellTooltip(MarkdownTableCellLayout cell, double baseFontSize)
    {
        var styled = MarkdownStyledText.FromInlines(cell.Cell.Inlines);
        var foreground = LookupBrush("MmTextBrush") ?? Brushes.Black;

        if (styled.Math.Count == 0)
        {
            return new TextBlock
            {
                Text = styled.Text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = baseFontSize,
                FontFamily = ResolveBodyFontFamily(),
                UseLayoutRounding = true
            };
        }

        var tooltip = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        foreach (var formula in styled.Math)
        {
            tooltip.Children.Add(new MarkdownMathInlineVisual(formula.Latex, baseFontSize, foreground));
        }

        // Текст вокруг формул показываем без TeX-источника самой формулы —
        // в тултипе уже есть её отрендеренный вариант.
        var text = RemoveTableCellMathRanges(styled);
        if (!string.IsNullOrWhiteSpace(text))
        {
            tooltip.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = baseFontSize,
                FontFamily = ResolveBodyFontFamily(),
                UseLayoutRounding = true
            });
        }

        return tooltip;
    }

    /// <summary>Канонический текст ячейки без TeX-диапазонов формул.</summary>
    private static string RemoveTableCellMathRanges(MarkdownStyledText styled)
    {
        var text = styled.Text;
        var builder = new System.Text.StringBuilder(text.Length);
        var position = 0;
        foreach (var formula in styled.Math)
        {
            var start = Math.Clamp(formula.Range.Start, 0, text.Length);
            var end = Math.Clamp(formula.Range.End, start, text.Length);
            builder.Append(text, position, start - position);
            position = Math.Max(position, end);
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }
}

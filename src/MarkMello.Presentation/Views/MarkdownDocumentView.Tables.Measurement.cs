using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    /// <summary>
    /// ADR-0011 §1/§6: кэш замеров ячеек по ключу
    /// (текст, fontSize, letterSpacing, шрифты, вес). Замер через пробные
    /// TextLayout стоит дорого, а повторяется при каждом reflow пайплайна —
    /// ресайз окна и перебор ступеней деградации попадают в кэш.
    /// Инвалидация — при rebuild документа (новые замеры приходят с новыми
    /// параметрами шрифтов; кэш ограничен по размеру).
    /// </summary>
    private readonly Dictionary<MarkdownTableCellMeasureKey, MarkdownTableCellMeasure> _tableMeasureCache = [];

    private const int MaxTableMeasureCacheEntries = 512;

    /// <summary>
    /// ADR-0011 §1: замер ячейки таблицы (min/max-content) с кэшем.
    /// Параметры совпадают с теми, что BuildSelectionFragment отдаёт ячейкам:
    /// fontFamily, fontSize, letterSpacing, fontWeight — чтобы замер совпадал
    /// с реальной вёрсткой.
    /// Замер внутренний (InternalsVisibleTo): тесты T9 проверяют min/max-content
    /// и кэш напрямую.
    /// </summary>
    internal MarkdownTableCellMeasure MeasureTableCell(
        MarkdownTableCell cell,
        bool isHeader,
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        double letterSpacing,
        double lineHeight,
        IBrush foreground)
    {
        var styled = MarkdownStyledText.FromInlines(cell.Inlines);
        if (styled.Text.Length == 0)
        {
            return default;
        }

        var key = new MarkdownTableCellMeasureKey(
            styled.Text,
            fontSize,
            letterSpacing,
            fontWeight,
            fontFamily.Name,
            ResolveMonoFontFamily().Name);
        if (_tableMeasureCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var measure = MeasureTableCellUncached(
            styled, fontFamily, fontSize, fontWeight, letterSpacing, lineHeight, foreground);
        if (_tableMeasureCache.Count >= MaxTableMeasureCacheEntries)
        {
            _tableMeasureCache.Clear();
        }

        _tableMeasureCache[key] = measure;
        return measure;
    }

    private MarkdownTableCellMeasure MeasureTableCellUncached(
        MarkdownStyledText styled,
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        double letterSpacing,
        double lineHeight,
        IBrush foreground)
    {
        return new MarkdownTableCellMeasure(
            MeasureTableCellMinContent(styled, fontFamily, fontSize, fontWeight, letterSpacing, foreground),
            MeasureTableCellMaxContent(styled, fontFamily, fontSize, fontWeight, letterSpacing, lineHeight, foreground));
    }

    /// <summary>
    /// ADR-0011 §1: max-content — ширина контента в одну строку без переноса.
    /// Меряется пробным лейаутом без живых контролов: тот же
    /// MarkdownFormattedTextLayout, которым вёрстка строится, но с NoWrap
    /// и бесконечной шириной — формулы, inline-код, паддинги и letter-spacing
    /// учитываются ровно как в реальном рендере.
    /// </summary>
    private double MeasureTableCellMaxContent(
        MarkdownStyledText styled,
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        double letterSpacing,
        double lineHeight,
        IBrush foreground)
    {
        using var layout = new MarkdownFormattedTextLayout(
            styled,
            inlineImages: null,
            fontFamily,
            ResolveMonoFontFamily(),
            fontSize,
            fontWeight,
            FontStyle.Normal,
            lineHeight,
            letterSpacing,
            TextWrapping.NoWrap,
            double.PositiveInfinity,
            foreground,
            linkDecorations: null);

        return Math.Max(1, layout.WidthIncludingTrailingWhitespace);
    }

    /// <summary>
    /// ADR-0011 §1: min-content — минимальная ширина без переполнения.
    /// Канонический текст ячейки разбивается на «неделимые единицы»:
    /// слова текста (разделяются пробелами), формулы (не разрываются,
    /// приклеиваются к словам без пробела) и inline-код (с паддингом).
    /// min-content — ширина самой широкой единицы.
    /// </summary>
    private double MeasureTableCellMinContent(
        MarkdownStyledText styled,
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        double letterSpacing,
        IBrush foreground)
    {
        var best = 0d;
        var current = 0d;
        var text = styled.Text;

        var monoFontFamily = ResolveMonoFontFamily();
        var atomRanges = CollectTableCellAtomRanges(styled);
        var position = 0;

        foreach (var (start, end, math, code) in atomRanges)
        {
            AccumulateTableCellTextUnits(
                text, position, start,
                fontFamily, fontSize, fontWeight, letterSpacing,
                ref current, ref best);
            if (math is { } mathSpan)
            {
                current += MeasureTableCellMathAtom(mathSpan, fontSize, foreground);
            }
            else if (code is { } codeSpan)
            {
                current += MeasureTableCellCodeAtom(styled, codeSpan, monoFontFamily, fontSize, letterSpacing);
            }

            position = end;
        }

        AccumulateTableCellTextUnits(
            text, position, text.Length,
            fontFamily, fontSize, fontWeight, letterSpacing,
            ref current, ref best);

        // Последняя единица не закрывается пробелом — сравниваем отдельно.
        best = Math.Max(best, current);

        return Math.Max(1, Math.Ceiling(best));
    }

    /// <summary>
    /// Собирает непересекающиеся диапазоны «атомов» ячейки — формул и
    /// inline-кода — в порядке следования. Формула не разрывается (ADR-0009),
    /// inline-код рендерится с фоновыми паддингами как единое поле.
    /// </summary>
    private static List<(int Start, int End, MarkdownMathSpan? Math, MarkdownTextStyleSpan? Code)>
        CollectTableCellAtomRanges(MarkdownStyledText styled)
    {
        var ranges = new List<(int Start, int End, MarkdownMathSpan? Math, MarkdownTextStyleSpan? Code)>();
        foreach (var span in styled.Spans)
        {
            if (span.Style.IsCode && span.Range.End > span.Range.Start)
            {
                ranges.Add((span.Range.Start, span.Range.End, null, span));
            }
        }

        foreach (var formula in styled.Math)
        {
            if (formula.Range.End > formula.Range.Start)
            {
                ranges.Add((formula.Range.Start, formula.Range.End, formula, null));
            }
        }

        ranges.Sort(static (left, right) => left.Start != right.Start
            ? left.Start.CompareTo(right.Start)
            : left.End.CompareTo(right.End));

        // Пересечения (теоретически невозможные) отбрасываем, чтобы не
        // учитывать один и тот же диапазон дважды.
        var result = new List<(int Start, int End, MarkdownMathSpan?, MarkdownTextStyleSpan?)>(ranges.Count);
        var lastEnd = 0;
        foreach (var range in ranges)
        {
            if (range.Start < lastEnd)
            {
                continue;
            }

            result.Add(range);
            lastEnd = range.End;
        }

        return result;
    }

    /// <summary>
    /// Учитывает текстовый диапазон [start, end) в накоплении единиц:
    /// каждое слово продолжает текущую единицу, каждый пробельный разрыв
    /// закрывает её (min-content строки = самая широкая единица).
    /// </summary>
    private static void AccumulateTableCellTextUnits(
        string text,
        int start,
        int end,
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight,
        double letterSpacing,
        ref double current,
        ref double best)
    {
        var index = start;
        while (index < end)
        {
            var whitespaceStart = index;
            while (index < end && IsCellBreakWhitespace(text[index]))
            {
                index++;
            }

            if (index > whitespaceStart && current > 0)
            {
                best = Math.Max(best, current);
                current = 0;
            }

            if (index >= end)
            {
                break;
            }

            var wordStart = index;
            while (index < end && !IsCellBreakWhitespace(text[index]))
            {
                index++;
            }

            current += MeasureProbeTextWidth(
                text[wordStart..index],
                new Typeface(fontFamily, FontStyle.Normal, fontWeight),
                fontSize,
                letterSpacing);
        }
    }

    /// <summary>
    /// Пробельные символы, по которым строка переносится. Неразрывный пробел
    /// (\u00A0) переносов не даёт, поэтому к пробельным не относится.
    /// </summary>
    private static bool IsCellBreakWhitespace(char character)
        => character is ' ' or '\t' or '\r' or '\n';

    /// <summary>
    /// Ширина inline-формулы в ячейке. Формула не разрывается (ADR-0009):
    /// min- и max-content совпадают и равны полной ширине из
    /// MarkdownMathRenderer.Measure — с теми же параметрами, что и
    /// MarkdownMathTextRun (inline: isDisplay false, Math.Max(10, fontSize)).
    /// Невалидный LaTeX меряется как текстовый фолбэк (ADR-0011 §2.4):
    /// TeX-текст моноширинным — то же деградационное поведение, что и
    /// MarkdownMathTextRun.ResolveFallbackSize.
    /// </summary>
    private static double MeasureTableCellMathAtom(MarkdownMathSpan formula, double fontSize, IBrush foreground)
    {
        var layout = MarkdownMathRenderer.Measure(
            formula.Latex,
            Math.Max(10, fontSize),
            foreground,
            isDisplay: false);
        if (layout is { IsValid: true } valid)
        {
            return Math.Ceiling(valid.Size.Width);
        }

        return MeasureProbeTextWidth(
            formula.Latex.Length == 0 ? " " : formula.Latex,
            new Typeface(new FontFamily("Consolas, Menlo, monospace")),
            Math.Max(9, fontSize * 0.85),
            letterSpacing: 0);
    }

    /// <summary>
    /// Ширина inline-кода в ячейке: код моноширинным со scale 0.92 (как
    /// MarkdownTextRunPropertiesFactory.InlineCodeFontScale) плюс фоновые
    /// паддинги код-«пилюли» (4 слева + 4 справа, MarkdownInlineCodePadMetrics).
    /// </summary>
    private static double MeasureTableCellCodeAtom(
        MarkdownStyledText styled,
        MarkdownTextStyleSpan codeSpan,
        FontFamily monoFontFamily,
        double fontSize,
        double letterSpacing)
    {
        var text = styled.Text;
        var start = Math.Clamp(codeSpan.Range.Start, 0, text.Length);
        var end = Math.Clamp(codeSpan.Range.End, start, text.Length);
        var width = MeasureProbeTextWidth(
            text[start..end],
            new Typeface(monoFontFamily, FontStyle.Normal, FontWeight.Normal),
            fontSize * MarkdownTextRunPropertiesFactory.InlineCodeFontScale,
            letterSpacing);
        return Math.Ceiling(width + 8);
    }

    /// <summary>Извлекает текст стилизованного диапазона из canonical-строки.</summary>
    internal static double MeasureProbeTextWidthForTest(string text, double fontSize)
        => MeasureProbeTextWidth(
            text,
            new Typeface(new FontFamily("Segoe UI"), FontStyle.Normal, FontWeight.Normal),
            fontSize,
            letterSpacing: 0);

    /// <summary>
    /// Пробельный замер строки одним TextLayout без переноса — пробный
    /// лейаут, не живой контрол (ADR-0011 §1).
    /// </summary>
    private static double MeasureProbeTextWidth(
        string text,
        Typeface typeface,
        double fontSize,
        double letterSpacing)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        using var layout = new TextLayout(
            text,
            typeface,
            Math.Max(1, fontSize),
            Brushes.Black,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            textTrimming: null,
            textDecorations: null,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: double.NaN,
            letterSpacing: letterSpacing,
            maxLines: 1,
            textStyleOverrides: null);

        return Math.Max(1, layout.WidthIncludingTrailingWhitespace);
    }
}

/// <summary>
/// Результат замера ячейки таблицы (ADR-0011 §1), в ширинах контента
/// без горизонтального паддинга ячейки.
/// </summary>
internal readonly record struct MarkdownTableCellMeasure(
    double MinContentWidth,
    double MaxContentWidth);

/// <summary>
/// Ключ кэша замеров ячеек: текст (включает TeX формул — они часть
/// canonical-текста), параметры шрифта и шрифтовые семейства.
/// </summary>
internal readonly record struct MarkdownTableCellMeasureKey(
    string Text,
    double FontSize,
    double LetterSpacing,
    FontWeight FontWeight,
    string FontFamily,
    string MonoFontFamily);

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Замер одной колонки таблицы (ADR-0011 §1) в ширинах контента,
/// без паддинга; <see cref="HorizontalPadding"/> добавляется при расчёте
/// ширин, так как зависит от активной ступени деградации.
/// </summary>
/// <param name="MinContentWidth">Минимальная ширина без переполнения:
/// самое длинное слово текста либо вся ширина формулы (формула не разрывается).</param>
/// <param name="MaxContentWidth">Ширина контента в одну строку без переноса.</param>
/// <param name="HorizontalPadding">Суммарный горизонтальный паддинг ячейки
/// на активной ступени (левый + правый).</param>
internal readonly record struct MarkdownTableColumnMetrics(
    double MinContentWidth,
    double MaxContentWidth,
    double HorizontalPadding)
{
    /// <summary>Пол ширины колонки: min-content + паддинг (ADR-0011 §2.3).</summary>
    public double MinColumnWidth => MinContentWidth + HorizontalPadding;

    /// <summary>Ширина колонки под контент в одну строку: max-content + паддинг.</summary>
    public double MaxColumnWidth => MaxContentWidth + HorizontalPadding;
}

/// <summary>
/// ADR-0011 §2: content-based распределение ширин колонок таблицы
/// («auto table layout» в духе CSS). Чистая логика без Avalonia —
/// тестируется без UI (handoff T3).
///
/// Правила:
/// 1. колонка, чей max-content не превышает «честной доли» доступной
///    ширины (available / количество колонок), получает max-content —
///    короткие колонки не разбухают;
/// 2. остаток делится между «широкими» колонками пропорционально их
///    избытку (max-content − min-content), с потолком max-content;
/// 3. пол каждой колонки — её min-content (+ паддинг);
/// 4. таблица по-прежнему растягивается на всю ширину контента: остаток,
///    который никто не потребил, делится поровну.
///
/// Возвращает <c>null</c>, если таблица не помещается даже по сумме
/// min-content — тогда вступает лестница деградации (ADR-0011 §3).
/// </summary>
internal static class MarkdownTableColumnSizing
{
    /// <summary>Допуск на сравнение ширин: дробные расхождения меньше
    /// половины пикселя считаются равенством.</summary>
    private const double Epsilon = 0.5;

    /// <summary>
    /// Распределяет доступную ширину между колонками. Возвращает полные
    /// ширины колонок (контент + паддинг) в пикселях или <c>null</c>,
    /// если сумма min-content не помещается в <paramref name="availableWidth"/>.
    /// </summary>
    public static double[]? TryAssign(
        IReadOnlyList<MarkdownTableColumnMetrics> columns,
        double availableWidth)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0
            || availableWidth <= 0
            || !double.IsFinite(availableWidth))
        {
            return null;
        }

        var totalMin = 0d;
        foreach (var column in columns)
        {
            totalMin += column.MinColumnWidth;
        }

        if (totalMin > availableWidth + Epsilon)
        {
            // Переносом текста таблица уже не спасается — лестница деградации.
            return null;
        }

        var widths = new double[columns.Count];
        var assigned = 0d;
        for (var index = 0; index < columns.Count; index++)
        {
            widths[index] = columns[index].MinColumnWidth;
            assigned += widths[index];
        }

        // 1) «Честная доля»: короткие колонки получают max-content целиком.
        var fairShare = availableWidth / columns.Count;
        for (var index = 0; index < columns.Count; index++)
        {
            var max = columns[index].MaxColumnWidth;
            if (max <= fairShare + Epsilon && max > widths[index])
            {
                assigned += max - widths[index];
                widths[index] = max;
            }
        }

        // 2) Остаток — широким колонкам пропорционально избытку
        //    (max-content − min-content), с потолком max-content.
        DistributeProportionallyToCapacity(columns, widths, availableWidth - assigned);

        // 3) Таблица тянется на всю ширину: неиспользованный остаток делится
        //    поровну (все колонки уже на max-content либо потолок не достигнут).
        StretchRemainingEqually(columns, widths, availableWidth, widths.Sum());

        return widths;
    }

    /// <summary>
    /// Распределяет <paramref name="remaining"/> между колонками
    /// пропорционально запасу до max-content; доли, выходящие за потолок,
    /// обрезаются, а остаток перераспределяется следующей итерацией.
    /// </summary>
    private static void DistributeProportionallyToCapacity(
        IReadOnlyList<MarkdownTableColumnMetrics> columns,
        double[] widths,
        double remaining)
    {
        while (remaining > Epsilon)
        {
            var totalCapacity = 0d;
            for (var index = 0; index < columns.Count; index++)
            {
                totalCapacity += Math.Max(0, columns[index].MaxColumnWidth - widths[index]);
            }

            if (totalCapacity <= Epsilon)
            {
                return; // все колонки на max-content
            }

            var distributed = 0d;
            var clampedAny = false;
            for (var index = 0; index < columns.Count; index++)
            {
                var capacity = Math.Max(0, columns[index].MaxColumnWidth - widths[index]);
                if (capacity <= Epsilon)
                {
                    continue;
                }

                var share = remaining * capacity / totalCapacity;
                var target = Math.Min(widths[index] + share, columns[index].MaxColumnWidth);
                if (target < widths[index] + share - Epsilon)
                {
                    clampedAny = true;
                }

                distributed += target - widths[index];
                widths[index] = target;
            }

            remaining -= distributed;
            if (!clampedAny)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Докидывает неиспользованный излишек поровну, чтобы таблица растянулась
    /// на всю ширину контента (ADR-0011 §2 «таблица по-прежнему растягивается»).
    /// </summary>
    private static void StretchRemainingEqually(
        IReadOnlyList<MarkdownTableColumnMetrics> columns,
        double[] widths,
        double availableWidth,
        double assigned)
    {
        var remaining = availableWidth - assigned;
        if (remaining <= Epsilon || columns.Count == 0)
        {
            return;
        }

        var each = remaining / columns.Count;
        for (var index = 0; index < columns.Count; index++)
        {
            widths[index] += each;
        }
    }
}

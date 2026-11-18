namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// ADR-0011: все числа лестницы деградации таблиц и параметры пайплайна
/// ширин — в одном месте (handoff T1). Комментарии ссылаются на разделы
/// ADR-0011.
/// </summary>
internal static class MarkdownTableLayoutConstants
{
    /// <summary>
    /// ADR-0011 §3: множители шрифта ячеек от <c>ReadingPreferences.FontSize</c>.
    /// Индекс 0 — базовое состояние (текущий дизайн: body 0.92em), индексы
    /// 1..3 — ступени деградации. Header деградирует синхронно с body
    /// (решение владельца §5.4) с сохранением дизайн-пропорции
    /// <see cref="HeaderToBodyFontSizeRatio"/>.
    /// </summary>
    public static readonly double[] FontLadderMultipliers = [0.92, 0.85, 0.80, 0.75];

    /// <summary>
    /// ADR-0011 §3: горизонтальный паддинг ячеек по ступеням (14 → 8 → 4);
    /// индекс = min(ступень деградации, длина-1). Вертикальный паддинг не
    /// трогается (базовые «14,10» из Controls.axaml остаются).
    /// Значения применяются классами ступеней в Controls.axaml
    /// (решение владельца §5.3: <c>.d1/.d2/.d3</c>); числа здесь дублируют
    /// их только для расчёта ширин и должны совпадать с axaml.
    /// </summary>
    public static readonly double[] HorizontalPaddingLadder = [14, 8, 4];

    /// <summary>
    /// Пол шрифта ячеек (ADR-0011 §3) — консистентно с полом код-блоков
    /// <c>Math.Max(12, …)</c> в BuildCodeBlock.
    /// </summary>
    public const double MinimumCellFontSize = 12;

    /// <summary>
    /// Дизайн-пропорция header к body (0.85em header / 0.92em body,
    /// MarkdownDocumentView.Tables.cs). Header деградирует синхронно с body.
    /// </summary>
    public const double HeaderToBodyFontSizeRatio = 0.85 / 0.92;

    /// <summary>
    /// Вертикальный паддинг ячеек: не меняется при деградации
    /// (ADR-0011 §3 «вертикальный не трогаем»); зеркалит вертикальную часть
    /// базового Padding="14,10" из Controls.axaml.
    /// </summary>
    public const double VerticalCellPadding = 10;

    /// <summary>
    /// ADR-0011 §6: гистерезис пересчёта ширин — игнорировать дрожание
    /// доступной ширины меньше этого порога.
    /// </summary>
    public const double AvailableWidthHysteresis = 0.5;

    /// <summary>
    /// Возвращает размер шрифта body-ячейки на ступени лестницы.
    /// </summary>
    public static double GetBodyFontSize(int step, double baseFontSize)
        => Math.Max(MinimumCellFontSize, baseFontSize * FontLadderMultipliers[Math.Clamp(step, 0, FontLadderMultipliers.Length - 1)]);

    /// <summary>
    /// Возвращает размер шрифта header-ячейки на ступени (деградирует
    /// синхронно с body, решение владельца §5.4).
    /// </summary>
    public static double GetHeaderFontSize(int step, double baseFontSize)
        => Math.Max(MinimumCellFontSize, GetBodyFontSize(step, baseFontSize) * HeaderToBodyFontSizeRatio);

    /// <summary>
    /// Горизонтальный паддинг ячейки на ступени (ADR-0011 §3).
    /// </summary>
    public static double GetHorizontalPadding(int step)
        => HorizontalPaddingLadder[Math.Clamp(step, 0, HorizontalPaddingLadder.Length - 1)];
}

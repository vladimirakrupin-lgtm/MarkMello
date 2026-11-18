using Avalonia.Controls;
using Avalonia.Media;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Состояние пайплайна вёрстки одной таблицы (ADR-0011, handoff T4).
/// Живёт столько же, сколько контрол таблицы: при rebuild документа таблица
/// перестраивается со своим состоянием (или переиспользуется вместе с ним —
/// неизменённая таблица сохраняет применённые ширины, ADR-0011 §6).
/// Данные без логики: логика — в partial-файлах MarkdownDocumentView.Tables.*.
/// </summary>
internal sealed class MarkdownTableLayoutState
{
    public MarkdownTableLayoutState(
        MarkdownTableBlock table,
        Grid grid,
        int columnCount,
        FontFamily sansFontFamily)
    {
        Table = table;
        Grid = grid;
        ColumnCount = columnCount;
        SansFontFamily = sansFontFamily;
    }

    /// <summary>Таблица, для которой считаются ширины.</summary>
    public MarkdownTableBlock Table { get; }

    /// <summary>Border.mm-md-table — внешний контрол таблицы. Его ширина
    /// и есть доступная ширина для пайплайна (известна только на layout,
    /// решение владельца §5.1). Заполняется в BuildTable после построения
    /// ячеек.</summary>
    public Border Host { get; set; } = null!;

    /// <summary>Грид строк/колонок таблицы.</summary>
    public Grid Grid { get; }

    public int ColumnCount { get; }

    /// <summary>Sans-стек ячеек (замер и реальная вёрстка совпадают).</summary>
    public FontFamily SansFontFamily { get; }

    /// <summary>Ячейки таблицы с их контролов — для деградации шрифтов,
    /// классов ступеней и тултипов.</summary>
    public List<MarkdownTableCellLayout> Cells { get; } = [];

    /// <summary>Последняя ширина, на которой работал пайплайн
    /// (гистерезис ADR-0011 §6).</summary>
    public double LastPipelineWidth { get; set; } = double.NaN;

    /// <summary>Применённая ступень шрифтов: -1 — ничего, 0 — базовая,
    /// 1..N — деградация.</summary>
    public int AppliedStep { get; set; } = -1;

    /// <summary>Применённые полные ширины колонок (или null — нейтральные 1*).</summary>
    public double[]? AppliedWidths { get; set; }

    /// <summary>Скролл-режим (ADR-0011 §4) — запасной сценарий.</summary>
    public bool ScrollMode { get; set; }

    /// <summary>Скроллер скролл-режима (для подписки/отписки колёсика).</summary>
    public ScrollViewer? ScrollViewer { get; set; }
}

/// <summary>Одна построенная ячейка таблицы: контролы и данные для
/// деградации (ступени шрифтов/паддингов) и тултипов.</summary>
internal sealed record MarkdownTableCellLayout(
    Border Border,
    Control Content,
    MarkdownTableCell Cell,
    bool IsHeader,
    int Column);

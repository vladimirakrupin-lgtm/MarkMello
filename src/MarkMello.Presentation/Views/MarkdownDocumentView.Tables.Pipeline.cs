using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    /// <summary>
    /// ADR-0011 §6 (решение владельца §5.1): доступная ширина таблицы
    /// неизвестна в момент синхронного BuildTable, поэтому пайплайн ширин
    /// запускается отложенно — по фактическому bounds хоста после первого
    /// layout-прохода (стартовые колонки — нейтральные 1*). Одноразовый
    /// reflow при открытии — принятое следствие решения.
    /// Подписка живёт вместе с таблицей и снимается при detach контрола.
    /// </summary>
    private void AttachTableLayoutPipeline(MarkdownTableLayoutState state)
    {
        IDisposable? boundsSubscription = null;

        void OnHostDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            boundsSubscription?.Dispose();
            state.Host.DetachedFromVisualTree -= OnHostDetached;
        }

        state.Host.DetachedFromVisualTree += OnHostDetached;
        boundsSubscription = state.Host.GetObservable(Control.BoundsProperty)
            .Subscribe(new Avalonia.Reactive.AnonymousObserver<Rect>(bounds =>
                RunTableLayoutPipeline(state, bounds.Width)));
    }

    /// <summary>
    /// Пайплайн ADR-0011: замер → content-based распределение →
    /// лестница деградации → горизонтальный скролл. Первая ступень, на
    /// которой таблица помещается, — рабочая. При расширении окна пайплайн
    /// перезапускается с базовой ступени — таблица «возвращает» деградацию.
    /// </summary>
    private void RunTableLayoutPipeline(MarkdownTableLayoutState state, double availableWidth)
    {
        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            return;
        }

        // Гистерезис (ADR-0011 §6): пересчёт не на каждое слово/каждый пиксель,
        // а при фактическом изменении доступной ширины или структуры
        // (структурные изменения = rebuild → новое состояние таблицы).
        if (!double.IsNaN(state.LastPipelineWidth)
            && Math.Abs(availableWidth - state.LastPipelineWidth)
                <= MarkdownTableLayoutConstants.AvailableWidthHysteresis)
        {
            return;
        }

        state.LastPipelineWidth = availableWidth;

        var baseFontSize = ReadingPreferences.FontSize;
        var maxStep = MarkdownTableLayoutConstants.FontLadderMultipliers.Length - 1;
        for (var step = 0; step <= maxStep; step++)
        {
            var metrics = MeasureTableColumns(state, step, baseFontSize);
            if (MarkdownTableColumnSizing.TryAssign(metrics, availableWidth) is { } widths)
            {
                ExitTableScrollMode(state);
                ApplyTableCellStep(state, step, baseFontSize);
                ApplyTableColumnWidths(state, widths);
                return;
            }
        }

        // Ни одна ступень лестницы не спасла — горизонтальный скролл (ADR-0011 §4).
        EnterTableScrollMode(state, baseFontSize);
    }

    /// <summary>
    /// ADR-0011 §1: замер колонок таблицы на ступени лестницы — max по ячейкам
    /// каждой колонки. Замер идёт через кэш MeasureTableCell, поэтому перемер
    /// на новой ступени (новый fontSize) — новая запись кэша, на прежних
    /// ступенях (ресайз) — попадание в кэш.
    /// </summary>
    private MarkdownTableColumnMetrics[] MeasureTableColumns(
        MarkdownTableLayoutState state,
        int step,
        double baseFontSize)
    {
        var headerFontSize = MarkdownTableLayoutConstants.GetHeaderFontSize(step, baseFontSize);
        var bodyFontSize = MarkdownTableLayoutConstants.GetBodyFontSize(step, baseFontSize);
        var padding = 2 * MarkdownTableLayoutConstants.GetHorizontalPadding(step);

        var minPerColumn = new double[state.ColumnCount];
        var maxPerColumn = new double[state.ColumnCount];

        foreach (var cell in state.Cells)
        {
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

            var column = Math.Clamp(cell.Column, 0, state.ColumnCount - 1);
            minPerColumn[column] = Math.Max(minPerColumn[column], measure.MinContentWidth);
            maxPerColumn[column] = Math.Max(maxPerColumn[column], measure.MaxContentWidth);
        }

        var metrics = new MarkdownTableColumnMetrics[state.ColumnCount];
        for (var index = 0; index < metrics.Length; index++)
        {
            metrics[index] = new MarkdownTableColumnMetrics(
                minPerColumn[index],
                Math.Max(minPerColumn[index], maxPerColumn[index]),
                padding);
        }

        return metrics;
    }

    /// <summary>
    /// ADR-0011 §3: применение ступени деградации — шрифты ячеек (header и body
    /// синхронно, решение владельца §5.4) и классы ступеней паддингов
    /// (решение владельца §5.3: значения живут в Controls.axaml). Деградация
    /// локальна для таблицы и не трогает ReadingPreferences.FontSize.
    /// </summary>
    private void ApplyTableCellStep(MarkdownTableLayoutState state, int step, double baseFontSize)
    {
        if (state.AppliedStep == step)
        {
            return;
        }

        state.AppliedStep = step;
        var headerFontSize = MarkdownTableLayoutConstants.GetHeaderFontSize(step, baseFontSize);
        var bodyFontSize = MarkdownTableLayoutConstants.GetBodyFontSize(step, baseFontSize);
        var stepClass = step switch
        {
            1 => "d1",
            2 => "d2",
            3 => "d3",
            _ => null
        };

        foreach (var cell in state.Cells)
        {
            for (var previous = 1; previous <= 3; previous++)
            {
                cell.Border.Classes.Remove($"d{previous}");
            }

            if (stepClass is not null)
            {
                cell.Border.Classes.Add(stepClass);
            }

            ApplyTableCellTypography(cell, headerFontSize, bodyFontSize);
        }
    }

    /// <summary>
    /// Переставляет размер шрифта/lineHeight/letterSpacing у уже построенных
    /// фрагментов ячейки. Контролы ячеек продолжают строиться через
    /// BuildSelectionFragment — выделение и копирование сохраняются.
    /// </summary>
    private void ApplyTableCellTypography(MarkdownTableCellLayout cell, double headerFontSize, double bodyFontSize)
    {
        var fontSize = cell.IsHeader ? headerFontSize : bodyFontSize;
        var lineHeight = cell.IsHeader
            ? Math.Max(fontSize * 1.45, fontSize + 4)
            : Math.Max(fontSize * 1.55, fontSize + 4);
        var letterSpacing = (cell.IsHeader ? fontSize * 0.05 : 0) + ReadingPreferences.LetterSpacing;

        switch (cell.Content)
        {
            case MarkdownSelectionTextFragment fragment:
                fragment.BaseFontSize = fontSize;
                fragment.BaseLineHeight = lineHeight;
                fragment.BaseLetterSpacing = letterSpacing;
                break;

            case MarkdownImageFlowFragment flow:
                flow.BaseFontSize = fontSize;
                flow.BaseLineHeight = lineHeight;
                break;

            case TextBlock textBlock:
                textBlock.FontSize = fontSize;
                textBlock.LineHeight = lineHeight;
                textBlock.LetterSpacing = letterSpacing;
                break;
        }
    }

    /// <summary>
    /// ADR-0011 §2/§4: фиксация пиксельных ширин колонок. Округление вверх,
    /// чтобы накопленная дробная ширина не съедала контент последней колонки.
    /// </summary>
    private static void ApplyTableColumnWidths(MarkdownTableLayoutState state, double[] widths)
    {
        var definitions = state.Grid.ColumnDefinitions;
        for (var index = 0; index < definitions.Count && index < widths.Length; index++)
        {
            definitions[index].Width = new GridLength(Math.Ceiling(widths[index]), GridUnitType.Pixel);
        }

        state.AppliedWidths = widths;
    }

    /// <summary>
    /// ADR-0011 §4: горизонтальный скролл — запасной сценарий после
    /// минимальной ступени лестницы. По образцу код-блоков (BuildCodeBlock):
    /// ScrollViewer Auto/Disabled с резервом под скроллбар. Ширина таблицы =
    /// сумма min-content колонок (без сохранения content-based пропорций).
    /// </summary>
    private void EnterTableScrollMode(MarkdownTableLayoutState state, double baseFontSize)
    {
        var step = MarkdownTableLayoutConstants.FontLadderMultipliers.Length - 1;
        ApplyTableCellStep(state, step, baseFontSize);

        var metrics = MeasureTableColumns(state, step, baseFontSize);
        var widths = new double[metrics.Length];
        for (var index = 0; index < metrics.Length; index++)
        {
            widths[index] = Math.Ceiling(metrics[index].MinColumnWidth);
        }

        ApplyTableColumnWidths(state, widths);

        if (!state.ScrollMode)
        {
            state.ScrollMode = true;
            // Avalonia не отвязывает контрол автоматически: грид нужно
            // снять с хоста ДО помещения во внутренний Border скроллера,
            // иначе повторное родительство бросит InvalidOperationException.
            state.Host.Child = null;
            // В скролл-режиме переполнение обрезается по границе ячейки:
            // базовый стиль Border.mm-md-table стоит ClipToBounds=False.
            state.Host.ClipToBounds = true;
            foreach (var cell in state.Cells)
            {
                cell.Border.ClipToBounds = true;
            }

            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border
                {
                    Padding = new Thickness(0, 0, 0, CodeBlockHorizontalScrollBarReserve),
                    Child = state.Grid
                }
            };

            scroller.PointerWheelChanged += OnTableScrollWheel;
            state.ScrollViewer = scroller;
            state.Host.Child = scroller;
        }

        AttachTableCellTooltips(state);
    }

    /// <summary>
    /// Выход из скролл-режима: таблица снова помещается после ресайза —
    /// грид возвращается напрямую, обрезка и тултипы снимаются.
    /// </summary>
    private void ExitTableScrollMode(MarkdownTableLayoutState state)
    {
        if (!state.ScrollMode)
        {
            return;
        }

        state.ScrollMode = false;
        state.Host.ClipToBounds = false;
        foreach (var cell in state.Cells)
        {
            cell.Border.ClipToBounds = false;
            ToolTip.SetTip(cell.Border, null);
        }

        if (state.ScrollViewer is { } scroller)
        {
            scroller.PointerWheelChanged -= OnTableScrollWheel;
            var innerBorder = (Border)scroller.Content!;
            innerBorder.Child = null;
            scroller.Content = null;
            state.ScrollViewer = null;
        }

        foreach (var cell in state.Cells)
        {
            ToolTip.SetTip(cell.Border, null);
        }

        state.Host.Child = state.Grid;
    }

    /// <summary>
    /// Решение владельца §5.2: вертикальный ввод не должен перехватываться
    /// скроллером таблицы — пробрасываем колёсико документному скроллу.
    /// Горизонтальный ввод (тачпад dx, shift+колесо) остаётся таблице.
    /// </summary>
    private void OnTableScrollWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0 || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return; // чисто горизонтальный ввод — таблица скроллит сама
        }

        if (sender is not Visual source)
        {
            return;
        }

        var documentScroll = FindDocumentScrollViewer(source.GetVisualParent());
        if (documentScroll is null)
        {
            return;
        }

        // Полный конструктор PointerWheelEventArgs: событие создаётся заново
        // с позиционированием на документный скролл и поднимается там.
        var forwarded = new PointerWheelEventArgs(
            documentScroll,
            e.Pointer,
            documentScroll,
            e.GetPosition(documentScroll),
            e.Timestamp,
            e.Properties,
            e.KeyModifiers,
            e.Delta)
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent,
            Route = RoutingStrategies.Bubble
        };
        documentScroll.RaiseEvent(forwarded);

        e.Handled = true;
    }

    /// <summary>
    /// Ближайший документный скролл над таблицей (DocScroll вьюера или
    /// PreviewScrollViewer редактора).
    /// </summary>
    private static ScrollViewer? FindDocumentScrollViewer(Visual? start)
    {
        for (var visual = start; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ScrollViewer scroller)
            {
                return scroller;
            }
        }

        return null;
    }
}

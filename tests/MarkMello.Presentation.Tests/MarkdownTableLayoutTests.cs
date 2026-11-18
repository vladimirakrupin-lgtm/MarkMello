using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// T9.2–T9.5 (ADR-0011): замер ячеек, пайплайн ширин, лестница деградации,
/// скролл-режим и регресс существующих сценариев таблиц.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownTableLayoutTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownTableLayoutTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task MinContentOfTextIsWidestWord()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument([]));
            var cell = new MarkdownTableCell([new MarkdownTextInline("short verylongword")]);
            var measure = view.MeasureTableCell(
                cell, isHeader: false,
                fontFamily: new FontFamily("Segoe UI"),
                fontSize: 16, fontWeight: FontWeight.Normal, letterSpacing: 0,
                lineHeight: 24, foreground: Brushes.Black);

            var widestWord = MarkdownDocumentView.MeasureProbeTextWidthForTest("verylongword", 16);
            var shortWord = MarkdownDocumentView.MeasureProbeTextWidthForTest("short", 16);

            // min-content — самое длинное слово, а не вся строка;
            // 'short' — 80px, 'verylongword' — 192px.
            Assert.Equal(widestWord, measure.MinContentWidth, 0);
            Assert.True(measure.MinContentWidth > shortWord);
            // max-content — вся строка без переноса.
            Assert.True(measure.MaxContentWidth > measure.MinContentWidth);
        }, CancellationToken.None);

    [Fact]
    public Task MinContentOfFormulaIsFullFormulaWidth()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument([]));
            var latex = @"\frac{a+b+c+d+e}{x+y+z}";
            var cell = new MarkdownTableCell([new MarkdownMathInline(latex, IsDisplay: false)]);
            var measure = view.MeasureTableCell(
                cell, isHeader: false,
                fontFamily: new FontFamily("Segoe UI"),
                fontSize: 16, fontWeight: FontWeight.Normal, letterSpacing: 0,
                lineHeight: 24, foreground: Brushes.Black);

            var rendered = MarkdownMathRenderer.Measure(latex, 16, Brushes.Black, isDisplay: false);
            Assert.NotNull(rendered);

            Assert.Equal(Math.Ceiling(rendered!.Size.Width), measure.MinContentWidth, 0);
            Assert.Equal(Math.Ceiling(rendered.Size.Width), measure.MaxContentWidth, 0);
        }, CancellationToken.None);

    [Fact]
    public Task MeasureCacheReturnsSameValueForSameKey()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument([]));
            var cell = new MarkdownTableCell([new MarkdownTextInline("cached value")]);
            var parameters = (isHeader: false,
                fontFamily: new FontFamily("Segoe UI"),
                fontSize: 16d, fontWeight: FontWeight.Normal, letterSpacing: 0d,
                lineHeight: 24d, foreground: (IBrush)Brushes.Black);

            var first = view.MeasureTableCell(cell, parameters.isHeader, parameters.fontFamily,
                parameters.fontSize, parameters.fontWeight, parameters.letterSpacing,
                parameters.lineHeight, parameters.foreground);
            var second = view.MeasureTableCell(cell, parameters.isHeader, parameters.fontFamily,
                parameters.fontSize, parameters.fontWeight, parameters.letterSpacing,
                parameters.lineHeight, parameters.foreground);

            Assert.Equal(first, second);
        }, CancellationToken.None);

    [Fact]
    public Task ContentBasedWidthsAreNotEqualColumns()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, "Второй заголовок"),
                    DataRow(Yes, "Длинный текст второй колонки, который переносится")),
            ]), contentWidth: 800);

            UpdateLayout(view);
            var grid = GetTableGrid(view);

            Assert.Equal(GridUnitType.Pixel, grid.ColumnDefinitions[0].Width.GridUnitType);
            Assert.Equal(GridUnitType.Pixel, grid.ColumnDefinitions[1].Width.GridUnitType);

            // Колонка с коротким содержимым уже, чем колонка с длинным текстом,
            // и не равна «слепой» 1/N половине ширины таблицы.
            Assert.True(grid.ColumnDefinitions[0].Width.Value < grid.ColumnDefinitions[1].Width.Value);
            Assert.True(grid.ColumnDefinitions[0].Width.Value < grid.Bounds.Width / 2 - 1);
        }, CancellationToken.None);

    [Fact]
    public Task TableThatFitsDoesNotDegrade()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, "Второй заголовок"),
                    DataRow(Yes, "Длинный текст второй колонки, который переносится")),
            ]), contentWidth: 800);

            UpdateLayout(view);

            var cells = GetTableCellBorders(view).ToList();
            Assert.NotEmpty(cells);
            Assert.All(cells, cell => Assert.DoesNotContain("d1", cell.Classes));
            Assert.All(cells, cell => Assert.DoesNotContain("d2", cell.Classes));
            Assert.All(cells, cell => Assert.DoesNotContain("d3", cell.Classes));
            // Деградация не трогает пользовательскую настройку.
            Assert.Equal(ReadingPreferences.Default.FontSize, view.ReadingPreferences.FontSize);
        }, CancellationToken.None);

    [Fact]
    public Task TooWideTableDegradesThroughLadder()
        => _fixture.Session.Dispatch(() =>
        {
            // «достопримечательность»: Σmin на базовой ступени 438 > 400
            // (лестница запускается), на минимальной 327 ≤ 400 (ступень
            // спасает, скролл не нужен).
            const string wideWord = "достопримечательность";
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells("Да", wideWord),
                    DataRow("Да", wideWord)),
            ]), contentWidth: 400);

            UpdateLayout(view);

            // Таблица спасается лестницей деградации (не скроллом): у ячеек
            // есть класс активной ступени.
            var degraded = GetTableCellBorders(view)
                .Any(cell => cell.Classes.Any(c => c is "d1" or "d2" or "d3"));
            Assert.True(degraded, "ожидается класс ступени деградации на ячейках");
            var grid = GetTableGrid(view);
            var hostWidth = view.GetVisualDescendants().OfType<Border>()
                .First(border => border.Classes.Contains("mm-md-table")).Bounds.Width;
            Assert.True(grid.ColumnDefinitions.Sum(d => d.Width.Value) <= hostWidth + 1,
                $"сумма колонок {grid.ColumnDefinitions.Sum(d => d.Width.Value)} при ширине хоста {hostWidth}");
            Assert.Equal(ReadingPreferences.Default.FontSize, view.ReadingPreferences.FontSize);
        }, CancellationToken.None);

    [Fact]
    public Task TwoTablesDegradeIndependently()
        => _fixture.Session.Dispatch(() =>
        {
            // Вторая таблица — та же деградирующая из TooWideTableDegradesThroughLadder.
            const string wideWord = "достопримечательность";
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, "Второй заголовок"),
                    DataRow(Yes, "Длинный текст второй колонки, который переносится")),
                CreateTable(
                    HeaderCells("Да", wideWord),
                    DataRow("Да", wideWord)),
            ]), contentWidth: 400);

            UpdateLayout(view);

            var hosts = view.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("mm-md-table"))
                .ToList();
            Assert.Equal(2, hosts.Count);

            var fittingCells = hosts[0].GetVisualDescendants().OfType<Border>()
                .Where(IsTableCell).ToList();
            var degradedCells = hosts[1].GetVisualDescendants().OfType<Border>()
                .Where(IsTableCell).ToList();

            // Помещающаяся таблица не деградирует, несмотря на соседку.
            Assert.All(fittingCells, cell => Assert.DoesNotContain(cell.Classes, c => c is "d1" or "d2" or "d3"));
            Assert.True(degradedCells.Any(cell => cell.Classes.Any(c => c is "d1" or "d2" or "d3")),
                "вторая таблица деградирует независимо от первой");
        }, CancellationToken.None);

    [Fact]
    public Task TableWiderThanAnyLadderStepFallsBackToHorizontalScroll()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, new string('M', 200)),
                    DataRow(Yes, new string('M', 200))),
            ]), contentWidth: 400);

            UpdateLayout(view);

            // Запасной сценарий ADR-0011 §4: грид завёрнут в ScrollViewer
            // по образцу код-блоков.
            var scrollViewer = view.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.VerticalScrollBarVisibility);

            var grid = GetTableGrid(view);
            Assert.Equal(GridUnitType.Pixel, grid.ColumnDefinitions[0].Width.GridUnitType);
            // Ширина таблицы = сумма min-content колонок: больше доступной
            // ширины, поэтому грид обёрнут в ScrollViewer.
            var hostWidth = view.GetVisualDescendants().OfType<Border>()
                .First(border => border.Classes.Contains("mm-md-table")).Bounds.Width;
            Assert.True(grid.ColumnDefinitions.Sum(d => d.Width.Value) > hostWidth,
                $"ширина таблицы {grid.ColumnDefinitions.Sum(d => d.Width.Value)} при ширине хоста {hostWidth}");
        }, CancellationToken.None);

    [Fact]
    public Task EmptyTableFallsBackToTextBlock()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                new MarkdownTableBlock([], []),
            ]), contentWidth: 800);

            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("mm-md-table"));
        }, CancellationToken.None);

    [Fact]
    public Task HeaderOnlyTableKeepsHeaderStyling()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(HeaderCells(Yes, "Второй заголовок")),
            ]), contentWidth: 800);

            UpdateLayout(view);

            var grid = GetTableGrid(view);
            Assert.Single(grid.RowDefinitions);
            var headers = view.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("mm-md-table-header-cell"))
                .ToList();
            Assert.Equal(2, headers.Count);
        }, CancellationToken.None);

    [Fact]
    public Task LastDataRowSuppressesTrailingDivider()
        => _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, "Второй заголовок"),
                    DataRow(Yes, "первая строка"),
                    DataRow(Yes, "вторая строка")),
            ]), contentWidth: 800);

            UpdateLayout(view);

            var lastCells = view.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("mm-md-table-cell-last"))
                .ToList();
            // По одной ячейке на каждую из двух колонок последней строки.
            Assert.Equal(2, lastCells.Count);
        }, CancellationToken.None);

    [Fact]
    public Task UnchangedTableKeepsWidthsAcrossRebuild()
        => _fixture.Session.Dispatch(() =>
        {
            // T8: неизменённая таблица переиспользуется (MarkdownBlockStructuralComparer)
            // вместе с применёнными ширинами — повторный rebuild без изменений
            // не сбрасывает колонки в 1*.
            var view = CreateView(new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, "Второй заголовок"),
                    DataRow(Yes, "Длинный текст второй колонки, который переносится")),
            ]), contentWidth: 800);

            UpdateLayout(view);
            var before = GetTableGrid(view).ColumnDefinitions
                .Select(definition => definition.Width).ToList();
            Assert.All(before, width => Assert.Equal(GridUnitType.Pixel, width.GridUnitType));

            view.Document = new RenderedMarkdownDocument(
            [
                CreateTable(
                    HeaderCells(Yes, "Второй заголовок"),
                    DataRow(Yes, "Длинный текст второй колонки, который переносится")),
            ]);
            UpdateLayout(view);

            var after = GetTableGrid(view).ColumnDefinitions
                .Select(definition => definition.Width).ToList();
            Assert.Equal(before, after);
        }, CancellationToken.None);

    private const string Yes = "Да";

    private static MarkdownTableBlock CreateTable(
        List<MarkdownTableCell> header,
        params IReadOnlyList<MarkdownTableCell>[] rows)
        => new(header, rows);

    private static List<MarkdownTableCell> HeaderCells(params string[] texts)
        => texts.Select(text => new MarkdownTableCell([new MarkdownTextInline(text)])).ToList();

    private static List<MarkdownTableCell> DataRow(params string[] texts)
        => texts.Select(text => new MarkdownTableCell([new MarkdownTextInline(text)])).ToList();

    private static MarkdownDocumentView CreateView(RenderedMarkdownDocument document, double contentWidth = 800)
    {
        var view = new MarkdownDocumentView
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default,
        };
        var window = new Window { Width = contentWidth, Height = 600, Content = view };
        window.Show();
        window.UpdateLayout();
        return view;
    }

    /// <summary>Двойной проход layout: пайплайн стартово ставит нейтральные
    /// 1* и фиксирует пиксельные ширины на отложенном пересчёте
    /// (решение владельца §5.1).</summary>
    private static void UpdateLayout(MarkdownDocumentView view)
    {
        var window = TopLevel.GetTopLevel(view)!;
        window.UpdateLayout();
        window.UpdateLayout();
    }

    private static Grid GetTableGrid(MarkdownDocumentView view)
    {
        var host = view.GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Classes.Contains("mm-md-table"));
        return host.Child as Grid
            ?? host.GetVisualDescendants().OfType<Grid>().First()
            ?? throw new InvalidOperationException("таблица без грида");
    }

    private static bool IsTableCell(Border border)
        => border.Classes.Contains("mm-md-table-cell")
            || border.Classes.Contains("mm-md-table-header-cell");

    private static IEnumerable<Border> GetTableCellBorders(MarkdownDocumentView view)
        => view.GetVisualDescendants().OfType<Border>().Where(IsTableCell);
}

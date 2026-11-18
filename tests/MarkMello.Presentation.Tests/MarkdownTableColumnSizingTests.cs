using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// T9.1 (ADR-0011 §2): чистая логика content-based распределения ширин
/// колонок — тестируется без UI.
/// </summary>
public sealed class MarkdownTableColumnSizingTests
{
    private static MarkdownTableColumnMetrics Column(double min, double max, double padding = 0)
        => new(min, max, padding);

    [Fact]
    public void ShortColumnGetsMaxContentAndDoesNotSwell()
    {
        // Короткая колонка («да/нет») не должна занимать слепую 1/N долю.
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(20, 30), Column(100, 400)],
            availableWidth: 430);

        Assert.NotNull(widths);
        Assert.Equal(30, widths![0], 3);
        Assert.Equal(400, widths[1], 3);
    }

    [Fact]
    public void WideColumnsShareRemainingProportionallyToExcess()
    {
        // Обе колонки шире «честной доли» (200): остаток после min-content
        // делится пропорционально избытку (max − min): 290 : 390.
        const double availableWidth = 400;
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(10, 300), Column(10, 400)],
            availableWidth);

        Assert.NotNull(widths);
        var first = 10 + (availableWidth - 20) * 290 / 680;
        var second = 10 + (availableWidth - 20) * 390 / 680;
        Assert.Equal(first, widths![0], 3);
        Assert.Equal(second, widths[1], 3);
    }

    [Fact]
    public void ColumnFloorIsMinContent()
    {
        // Пол каждой колонки — её min-content: широкая колонка не может
        // быть сжата ниже него.
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(300, 500), Column(50, 100)],
            availableWidth: 400);

        Assert.NotNull(widths);
        Assert.True(widths![0] >= 300 - 0.5);
        Assert.Equal(100, widths[1], 3);
        Assert.Equal(400, widths.Sum(), 1);
    }

    [Fact]
    public void SurplusIsDistributedSoTableStretchesToFullWidth()
    {
        // Таблица по-прежнему растягивается на всю ширину контента:
        // излишек после потолка max-content делится поровну.
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(10, 50), Column(10, 50)],
            availableWidth: 300);

        Assert.NotNull(widths);
        Assert.Equal(150, widths![0], 3);
        Assert.Equal(150, widths[1], 3);
    }

    [Fact]
    public void ColumnThatDoesNotFitFairShareStaysWithinMaxContent()
    {
        // Колонка шире «честной доли» получает не более max-content,
        // а остаток уходит остальным (здесь — второй колонке до её max).
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(0, 300), Column(0, 100)],
            availableWidth: 400);

        Assert.NotNull(widths);
        Assert.Equal(300, widths![0], 3);
        Assert.Equal(100, widths[1], 3);
    }

    [Fact]
    public void MinContentOverflowReturnsNullForLadder()
    {
        // Сумма min-content не помещается — распределение невозможно,
        // вступает лестница деградации (ADR-0011 §3).
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(300, 600), Column(300, 600)],
            availableWidth: 500);

        Assert.Null(widths);
    }

    [Fact]
    public void PaddingIsIncludedInColumnWidths()
    {
        // Пол и потолок учитывают суммарный горизонтальный паддинг ячейки.
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(20, 30, padding: 28), Column(100, 400, padding: 28)],
            availableWidth: 458);

        Assert.NotNull(widths);
        Assert.Equal(58, widths![0], 3);
        Assert.Equal(400, widths[1], 3);
    }

    [Fact]
    public void DegenerateInputsReturnNull()
    {
        Assert.Null(MarkdownTableColumnSizing.TryAssign([], 400));
        Assert.Null(MarkdownTableColumnSizing.TryAssign([Column(10, 20)], 0));
        Assert.Null(MarkdownTableColumnSizing.TryAssign([Column(10, 20)], double.PositiveInfinity));
    }

    [Fact]
    public void SingleWideColumnGrowsByRemainingButNotBeyondMaxContent()
    {
        // Одна широкая колонка получает max-content (колонка ≤ «честной доли»),
        // но таблица по-прежнему растягивается: остаток докидывается поровну.
        var widths = MarkdownTableColumnSizing.TryAssign(
            [Column(50, 200)],
            availableWidth: 400);

        Assert.NotNull(widths);
        Assert.Equal(400, widths![0], 3);
    }
}

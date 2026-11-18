using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

public sealed class ReadingZoomTests
{
    [Theory]
    [InlineData(1, 19)]
    [InlineData(3, 19)]
    [InlineData(-1, 17)]
    [InlineData(-3, 17)]
    public void ApplyReadingZoomStepsFontSizeByOnePerGesture(double deltaY, int expected)
    {
        var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();

        var applied = shell.ApplyReadingZoom(deltaY);

        Assert.True(applied);
        Assert.Equal(expected, shell.ReadingPreferences.FontSize);
    }

    [Fact]
    public void ApplyReadingZoomClampsToMinAndMax()
    {
        var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();
        shell.ReadingPreferences = ReadingPreferences.Default with { FontSize = ReadingPreferences.MinFontSize };

        var belowMin = shell.ApplyReadingZoom(-1);

        Assert.False(belowMin);
        Assert.Equal(ReadingPreferences.MinFontSize, shell.ReadingPreferences.FontSize);

        shell.ReadingPreferences = ReadingPreferences.Default with { FontSize = ReadingPreferences.MaxFontSize };

        var aboveMax = shell.ApplyReadingZoom(1);

        Assert.False(aboveMax);
        Assert.Equal(ReadingPreferences.MaxFontSize, shell.ReadingPreferences.FontSize);
    }

    [Fact]
    public void ApplyReadingZoomIgnoresZeroAndNonFiniteDelta()
    {
        var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();

        Assert.False(shell.ApplyReadingZoom(0));
        Assert.False(shell.ApplyReadingZoom(double.NaN));
        Assert.Equal(ReadingPreferences.Default.FontSize, shell.ReadingPreferences.FontSize);
    }

    [Fact]
    public void ApplyReadingZoomAccumulatesFractionalTouchpadDeltas()
    {
        var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();

        Assert.False(shell.ApplyReadingZoom(0.4));
        Assert.False(shell.ApplyReadingZoom(0.4));
        Assert.Equal(ReadingPreferences.Default.FontSize, shell.ReadingPreferences.FontSize);

        var applied = shell.ApplyReadingZoom(0.4);

        Assert.True(applied);
        Assert.Equal(ReadingPreferences.Default.FontSize + 1, shell.ReadingPreferences.FontSize);
    }

    [Fact]
    public void ApplyReadingZoomUpdatesDocumentPreferencesAndNotifiesSlider()
    {
        var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();
        var names = new List<string?>();
        shell.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        shell.ApplyReadingZoom(1);

        Assert.Equal(ReadingPreferences.Default.FontSize + 1, shell.DocumentReadingPreferences.FontSize);
        Assert.Contains(nameof(ShellViewModel.FontSizeSetting), names);
        Assert.Contains(nameof(ShellViewModel.FontSizeLabel), names);
        Assert.Contains(nameof(ShellViewModel.DocumentReadingPreferences), names);
    }

    [Fact]
    public void ApplyReadingZoomResetsRemainderAfterStep()
    {
        var shell = ShellViewModelTests.CreateAdvancedReadingTestShell();

        Assert.True(shell.ApplyReadingZoom(1));
        Assert.Equal(ReadingPreferences.Default.FontSize + 1, shell.ReadingPreferences.FontSize);

        // Remainder was consumed by the step above: a small fraction alone
        // must not trigger another step.
        Assert.False(shell.ApplyReadingZoom(0.4));
        Assert.Equal(ReadingPreferences.Default.FontSize + 1, shell.ReadingPreferences.FontSize);
    }
}

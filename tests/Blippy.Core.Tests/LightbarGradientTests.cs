using Blippy.Core;

namespace Blippy.Core.Tests;

public class LightbarGradientTests
{
    private static readonly Rgb Blue = Rgb.Parse("#0000FF");
    private static readonly Rgb Green = Rgb.Parse("#00FF00");

    [Fact]
    public void Full_battery_is_the_chosen_color()
    {
        Assert.Equal(Blue, LightbarGradient.Default(Blue).ColorFor(100));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(5)]
    [InlineData(0)]
    public void Nearly_empty_is_red(int level)
    {
        Assert.Equal(Rgb.Red, LightbarGradient.Default(Blue).ColorFor(level));
    }

    [Fact]
    public void Green_drains_through_yellow_not_brown()
    {
        var mid = LightbarGradient.Default(Green).ColorFor(55);
        // Halfway between green (120°) and red (0°) is yellow (60°), still fully bright.
        Assert.Equal(new Rgb(255, 255, 0), mid);
    }

    [Fact]
    public void Blue_drains_through_purple_the_short_way_round()
    {
        var (h, _, _) = LightbarGradient.Default(Blue).ColorFor(55).ToHsv();
        Assert.InRange(h, 295, 305);
    }

    [Fact]
    public void Redness_rises_steadily_as_the_battery_drains()
    {
        var g = LightbarGradient.Default(Green);
        var greens = Enumerable.Range(0, 11).Select(i => g.ColorFor(i * 10).G).ToList();
        Assert.Equal(greens.OrderBy(x => x), greens);
    }

    [Fact]
    public void A_gray_choice_fades_to_red_without_a_rainbow()
    {
        var gray = Rgb.Parse("#808080");
        var mid = LightbarGradient.Blend(Rgb.Red, gray, 0.5);
        Assert.Equal(0, mid.ToHsv().H, 3);
    }

    [Theory]
    [InlineData("#6E64E8", 0x6E, 0x64, 0xE8)]
    [InlineData("ff8800", 0xFF, 0x88, 0x00)]
    public void Parses_hex(string text, byte r, byte g, byte b)
    {
        Assert.Equal(new Rgb(r, g, b), Rgb.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData(null)]
    public void Rejects_bad_hex(string? text)
    {
        Assert.False(Rgb.TryParse(text, out _));
    }

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#6E64E8")]
    [InlineData("#2F9E78")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void Hsv_round_trips(string hex)
    {
        var c = Rgb.Parse(hex);
        var (h, s, v) = c.ToHsv();
        Assert.Equal(c, Rgb.FromHsv(h, s, v));
    }
}

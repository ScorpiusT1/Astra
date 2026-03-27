using ScottPlot;
using System.Windows;
using MediaColor = System.Windows.Media.Color;

namespace Astra.UI.Helpers;

public sealed class ScottPlotStyleOptions
{
    public string FontName { get; set; } = "微软雅黑";

    public Alignment LegendAlignment { get; set; } = Alignment.UpperRight;

    public bool TransparentBackground { get; set; } = true;

    public Color? FrameColor { get; set; }

    public Color? TextColor { get; set; }
}

public static class ScottPlotStyleHelper
{
    public static ScottPlotStyleOptions CreateThemeStyleOptions(
        string fontName = "微软雅黑",
        Alignment legendAlignment = Alignment.UpperRight,
        bool transparentBackground = true,
        string frameColorResourceKey = "SecondaryBorderColor",
        string frameColorFallbackHex = "#B8C2CC",
        string textColorResourceKey = "SecondaryTextColor",
        string textColorFallbackHex = "#757575")
    {
        return new ScottPlotStyleOptions
        {
            FontName = fontName,
            LegendAlignment = legendAlignment,
            TransparentBackground = transparentBackground,
            FrameColor = ResolvePlotColorFromResource(frameColorResourceKey, frameColorFallbackHex),
            TextColor = ResolvePlotColorFromResource(textColorResourceKey, textColorFallbackHex)
        };
    }

    public static void Apply(Plot plot, ScottPlotStyleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(plot);
        options ??= new ScottPlotStyleOptions();

        if (!string.IsNullOrWhiteSpace(options.FontName))
            plot.Font.Set(options.FontName);

        plot.Legend.Alignment = options.LegendAlignment;

        if (options.TransparentBackground)
        {
            plot.FigureBackground.Color = Colors.Transparent;
            plot.DataBackground.Color = Colors.Transparent;
        }

        if (options.FrameColor is Color frameColor)
        {
            plot.Axes.Left.FrameLineStyle.Color = frameColor;
            plot.Axes.Bottom.FrameLineStyle.Color = frameColor;
            plot.Axes.Right.FrameLineStyle.Color = frameColor;
            plot.Axes.Top.FrameLineStyle.Color = frameColor;
        }

        if (options.TextColor is Color textColor)
        {
            ApplyAxisTextColor(plot, textColor);
        }
    }

    public static void ApplyToPlotAndSubplots(Plot plot, IMultiplot? multiplot, ScottPlotStyleOptions? options = null)
    {
        Apply(plot, options);

        if (multiplot?.Subplots == null)
            return;

        for (int i = 0; i < multiplot.Subplots.Count; i++)
        {
            Apply(multiplot.GetPlot(i), options);
        }
    }

    public static Color ResolvePlotColorFromResource(string colorResourceKey, string fallbackHex)
    {
        var resources = Application.Current?.Resources;
        if (resources != null && resources[colorResourceKey] is MediaColor mediaColor)
        {
            return ToPlotColor(mediaColor);
        }

        return Color.FromHex(fallbackHex);
    }

    public static Color ToPlotColor(MediaColor color)
        => Color.FromHex($"#{color.R:X2}{color.G:X2}{color.B:X2}");

    private static void ApplyAxisTextColor(Plot plot, Color textColor)
    {
        // ScottPlot 版本接口存在差异，这里通过反射做兼容设置（存在则设置，不存在则忽略）。
        foreach (var axis in new object[] { plot.Axes.Left, plot.Axes.Bottom, plot.Axes.Right, plot.Axes.Top })
        {
            TrySetNestedProperty(axis, "LabelStyle.ForeColor", textColor);
            TrySetNestedProperty(axis, "TickLabelStyle.ForeColor", textColor);
            TrySetNestedProperty(axis, "MajorTickStyle.Color", textColor);
            TrySetNestedProperty(axis, "MinorTickStyle.Color", textColor);
        }

        TrySetNestedProperty(plot.Legend, "LabelStyle.ForeColor", textColor);
        TrySetNestedProperty(plot.Legend, "FontColor", textColor);
    }

    private static void TrySetNestedProperty(object target, string propertyPath, object value)
    {
        var current = target;
        var segments = propertyPath.Split('.');

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var property = current.GetType().GetProperty(segments[i]);
            if (property?.CanRead != true)
                return;

            current = property.GetValue(current);
            if (current == null)
                return;
        }

        var leaf = current.GetType().GetProperty(segments[^1]);
        if (leaf?.CanWrite != true)
            return;

        if (leaf.PropertyType.IsInstanceOfType(value))
        {
            leaf.SetValue(current, value);
        }
    }
}

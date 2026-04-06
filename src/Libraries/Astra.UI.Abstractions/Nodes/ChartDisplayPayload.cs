using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Astra.UI.Abstractions.Nodes;

/// <summary>图表角标：标量名称、数值与单位（由节点写入 <see cref="ChartDisplayPayload.ScalarAnnotations"/>）。</summary>
public sealed class ChartScalarAnnotation
{
    public string Name { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
}

/// <summary>
/// 主页测试项图表可展示的载荷类型（与 <see cref="NodeUiOutputKeys.ChartPayloadSnapshot"/> / Raw 产物中的同类型对象对应）。
/// </summary>
public enum ChartPayloadKind
{
    /// <summary>Y 随样本序号递增，等间隔 <see cref="ChartDisplayPayload.SamplePeriod"/>（对应 NVH 单通道波形）。</summary>
    Signal1D,

    /// <summary>按 X、Y 数组顺序折线连接。</summary>
    XYLine,

    /// <summary>X、Y 散点，不强制连线（仅标记）。</summary>
    XYScatter,

    /// <summary>二维强度栅格；<see cref="ChartDisplayPayload.HeatmapZ"/>[row, col] 行对应 <see cref="ChartDisplayPayload.HeatmapYCoordinates"/>，列对应 <see cref="ChartDisplayPayload.HeatmapXCoordinates"/>。</summary>
    Heatmap,

    /// <summary>多段线段，每行 (x0,y0,x1,y1)。</summary>
    LineSegments,

    /// <summary>垂直柱状图。</summary>
    Bar,

    /// <summary>水平条形图。</summary>
    HorizontalBar,

    /// <summary>分组柱状图（多系列并排）。</summary>
    GroupedBar,

    /// <summary>堆叠柱状图。</summary>
    StackedBar,

    /// <summary>饼图。</summary>
    Pie,

    /// <summary>环形图（饼图变体，中心留空）。</summary>
    Donut,

    /// <summary>雷达图 / 蛛网图。</summary>
    Radar
}

/// <summary>多系列图表显示布局。</summary>
public enum ChartLayoutMode
{
    /// <summary>所有系列叠加到一个 Plot。</summary>
    SinglePlot = 0,
    /// <summary>每个系列独立显示在一个子图。</summary>
    SubPlots = 1
}

/// <summary>
/// 图表快照（执行结束时由节点写入 OutputData 或 Raw 存储，由 Hydrator 拷贝进 UI 缓存）。
/// </summary>
public sealed class ChartDisplayPayload
{
    public ChartPayloadKind Kind { get; init; }

    /// <summary>底部轴标题（如时间、频率）。</summary>
    public string BottomAxisLabel { get; init; } = string.Empty;

    /// <summary>底部轴单位（可选，与 <see cref="BottomAxisLabel"/> 在 UI 中组合显示）。</summary>
    public string BottomAxisUnit { get; init; } = string.Empty;

    /// <summary>左侧轴标题（如幅值）。</summary>
    public string LeftAxisLabel { get; init; } = string.Empty;

    /// <summary>左侧轴单位（可选）。</summary>
    public string LeftAxisUnit { get; init; } = string.Empty;

    // --- Signal1D ---
    public double[]? SignalY { get; init; }

    /// <summary>相邻样本间隔（用于 Signal 的周期，默认 1）。</summary>
    public double SamplePeriod { get; init; } = 1.0;

    // --- XY ---
    public double[]? X { get; init; }
    public double[]? Y { get; init; }

    // --- Heatmap ---
    public double[,]? HeatmapZ { get; init; }

    /// <summary>列方向坐标，长度 = HeatmapZ 列数。</summary>
    public double[]? HeatmapXCoordinates { get; init; }

    /// <summary>行方向坐标，长度 = HeatmapZ 行数。</summary>
    public double[]? HeatmapYCoordinates { get; init; }

    /// <summary>
    /// 为 true 时：<see cref="HeatmapYCoordinates"/> 表示 log10(物理量)，纵轴刻度应按 10^y 格式化（如 LMS Morlet 对数频率轴，物理单位为 Hz）。
    /// </summary>
    public bool HeatmapYAxisIsLog10OfQuantity { get; init; }

    // --- Segments [segmentIndex, 0..3] = x0,y0,x1,y1 ---
    public double[,]? SegmentLines { get; init; }

    /// <summary>可选：在图上绘制水平参考线（卡控下限）。</summary>
    public double? HorizontalLimitLower { get; init; }

    /// <summary>可选：在图上绘制水平参考线（卡控上限）。</summary>
    public double? HorizontalLimitUpper { get; init; }

    // --- Bar / Pie / Radar ---

    /// <summary>分类项（Bar 每根柱子 / Pie 每个扇区 / Radar 每个轴）。</summary>
    public List<ChartCategoryItem>? Categories { get; init; }

    /// <summary>分组 / 堆叠柱状图：多系列数据。</summary>
    public List<ChartBarSeries>? BarGroups { get; init; }

    /// <summary>环形图中空比例 (0~1)，0 = 实心饼图。</summary>
    public double DonutFraction { get; init; }

    /// <summary>饼图扇区外扩比例，0 = 不外扩。</summary>
    public double ExplodeFraction { get; init; }

    /// <summary>雷达图各轴最大刻度值（null 则自动计算）。</summary>
    public double[]? RadarAxisMaxValues { get; init; }

    // --- 多系列（通用：每个 Entry 内嵌一个完整的 ChartDisplayPayload，支持任意图表类型混合） ---

    /// <summary>
    /// 多系列模式（可选）：每个 <see cref="ChartSeriesEntry"/> 内嵌一个完整的 <see cref="ChartDisplayPayload"/>，
    /// 支持任意图表类型混合（多曲线、多柱状图、多 Heatmap 等），每个系列可独立控制可见性。
    /// 为 null 或空表示单图模式，使用上方的 Kind + 数据字段渲染。
    /// </summary>
    public List<ChartSeriesEntry>? Series { get; init; }

    /// <summary>多系列显示布局（仅 <see cref="Series"/> 非空时有效）。运行时可由用户在图表窗口切换。</summary>
    public ChartLayoutMode LayoutMode { get; set; } = ChartLayoutMode.SinglePlot;

    /// <summary>可选：在图表上展示的标量文本（单图模式写在根载荷；多系列可与各 <see cref="ChartSeriesEntry.Data"/> 对齐）。</summary>
    public List<ChartScalarAnnotation>? ScalarAnnotations { get; init; }

    /// <summary>
    /// 将运行时标量注入载荷副本：多系列且条数与系列数一致时写入各子 <see cref="ChartSeriesEntry.Data"/>，否则写入根载荷。
    /// </summary>
    public static ChartDisplayPayload EmbedScalarsForDisplay(
        ChartDisplayPayload payload,
        IReadOnlyList<(string Name, double Value, string Unit)> scalars)
    {
        if (scalars == null || scalars.Count == 0)
            return payload;

        static ChartScalarAnnotation M((string Name, double Value, string Unit) s) =>
            new()
            {
                Name = s.Name ?? string.Empty,
                Value = s.Value,
                Unit = s.Unit ?? string.Empty
            };

        var copy = payload.Clone();
        if (copy.Series is { Count: > 0 } ser && ser.Count == scalars.Count)
        {
            var newSeries = new List<ChartSeriesEntry>(ser.Count);
            for (var i = 0; i < ser.Count; i++)
            {
                var ann = new List<ChartScalarAnnotation> { M(scalars[i]) };
                newSeries.Add(new ChartSeriesEntry
                {
                    Name = ser[i].Name,
                    Color = ser[i].Color,
                    IsVisibleByDefault = ser[i].IsVisibleByDefault,
                    Data = ser[i].Data.WithScalarAnnotations(ann)
                });
            }

            return new ChartDisplayPayload
            {
                Kind = copy.Kind,
                BottomAxisLabel = copy.BottomAxisLabel,
                BottomAxisUnit = copy.BottomAxisUnit,
                LeftAxisLabel = copy.LeftAxisLabel,
                LeftAxisUnit = copy.LeftAxisUnit,
                SignalY = copy.SignalY,
                SamplePeriod = copy.SamplePeriod,
                X = copy.X,
                Y = copy.Y,
                HeatmapZ = copy.HeatmapZ,
                HeatmapXCoordinates = copy.HeatmapXCoordinates,
                HeatmapYCoordinates = copy.HeatmapYCoordinates,
                HeatmapYAxisIsLog10OfQuantity = copy.HeatmapYAxisIsLog10OfQuantity,
                SegmentLines = copy.SegmentLines,
                HorizontalLimitLower = copy.HorizontalLimitLower,
                HorizontalLimitUpper = copy.HorizontalLimitUpper,
                Categories = copy.Categories,
                BarGroups = copy.BarGroups,
                DonutFraction = copy.DonutFraction,
                ExplodeFraction = copy.ExplodeFraction,
                RadarAxisMaxValues = copy.RadarAxisMaxValues,
                Series = newSeries,
                LayoutMode = copy.LayoutMode,
                ScalarAnnotations = copy.ScalarAnnotations
            };
        }

        return copy.WithScalarAnnotations(scalars.Select(M).ToList());
    }

    /// <summary>复制除 <see cref="ScalarAnnotations"/> 外的字段，并设置标量列表。</summary>
    public ChartDisplayPayload WithScalarAnnotations(IReadOnlyList<ChartScalarAnnotation>? annotations)
    {
        return new ChartDisplayPayload
        {
            Kind = Kind,
            BottomAxisLabel = BottomAxisLabel,
            BottomAxisUnit = BottomAxisUnit,
            LeftAxisLabel = LeftAxisLabel,
            LeftAxisUnit = LeftAxisUnit,
            SignalY = SignalY,
            SamplePeriod = SamplePeriod,
            X = X,
            Y = Y,
            HeatmapZ = HeatmapZ,
            HeatmapXCoordinates = HeatmapXCoordinates,
            HeatmapYCoordinates = HeatmapYCoordinates,
            HeatmapYAxisIsLog10OfQuantity = HeatmapYAxisIsLog10OfQuantity,
            SegmentLines = SegmentLines,
            HorizontalLimitLower = HorizontalLimitLower,
            HorizontalLimitUpper = HorizontalLimitUpper,
            Categories = Categories,
            BarGroups = BarGroups,
            DonutFraction = DonutFraction,
            ExplodeFraction = ExplodeFraction,
            RadarAxisMaxValues = RadarAxisMaxValues,
            Series = Series,
            LayoutMode = LayoutMode,
            ScalarAnnotations = annotations?.ToList()
        };
    }

    /// <summary>
    /// 根据 <paramref name="series"/> 中各子系列的 Kind 自动推断默认布局模式：
    /// 同类型 → SinglePlot，混合类型 → SubPlots。
    /// </summary>
    public static ChartLayoutMode InferDefaultLayout(IReadOnlyList<ChartSeriesEntry>? series)
    {
        if (series == null || series.Count <= 1)
            return ChartLayoutMode.SinglePlot;

        var firstKind = series[0].Data.Kind;
        for (var i = 1; i < series.Count; i++)
        {
            if (series[i].Data.Kind != firstKind)
                return ChartLayoutMode.SubPlots;
        }

        return ChartLayoutMode.SinglePlot;
    }

    /// <summary>
    /// 将 <paramref name="outputData"/> 中的轴标题/单位（若存在键）覆盖到 <paramref name="payload"/>，用于执行结果与 Raw/内联快照合并；
    /// 若存在 <see cref="NodeUiOutputKeys.LowerLimit"/> / <see cref="NodeUiOutputKeys.UpperLimit"/> 且可解析为数值，则同步到
    /// <see cref="HorizontalLimitLower"/> / <see cref="HorizontalLimitUpper"/>（供报告与主页绘制合格带参考线）。
    /// </summary>
    public static ChartDisplayPayload MergeAxisMetadata(ChartDisplayPayload payload, IDictionary<string, object>? outputData)
    {
        if (outputData == null || outputData.Count == 0)
        {
            return payload;
        }

        var bottomLabel = payload.BottomAxisLabel;
        var bottomUnit = payload.BottomAxisUnit;
        var leftLabel = payload.LeftAxisLabel;
        var leftUnit = payload.LeftAxisUnit;

        if (outputData.TryGetValue(NodeUiOutputKeys.ChartXAxisLabel, out var xl))
        {
            bottomLabel = xl?.ToString() ?? string.Empty;
        }

        if (outputData.TryGetValue(NodeUiOutputKeys.ChartXAxisUnit, out var xu))
        {
            bottomUnit = xu?.ToString() ?? string.Empty;
        }

        if (outputData.TryGetValue(NodeUiOutputKeys.ChartYAxisLabel, out var yl))
        {
            leftLabel = yl?.ToString() ?? string.Empty;
        }

        if (outputData.TryGetValue(NodeUiOutputKeys.ChartYAxisUnit, out var yu))
        {
            leftUnit = yu?.ToString() ?? string.Empty;
        }

        var horizLo = payload.HorizontalLimitLower;
        var horizHi = payload.HorizontalLimitUpper;
        if (outputData.TryGetValue(NodeUiOutputKeys.LowerLimit, out var loObj) && loObj != null)
        {
            if (TryMergeLimitDouble(loObj, out var loParsed))
                horizLo = loParsed;
        }

        if (outputData.TryGetValue(NodeUiOutputKeys.UpperLimit, out var hiObj) && hiObj != null)
        {
            if (TryMergeLimitDouble(hiObj, out var hiParsed))
                horizHi = hiParsed;
        }

        return new ChartDisplayPayload
        {
            Kind = payload.Kind,
            BottomAxisLabel = bottomLabel,
            BottomAxisUnit = bottomUnit,
            LeftAxisLabel = leftLabel,
            LeftAxisUnit = leftUnit,
            SignalY = payload.SignalY,
            SamplePeriod = payload.SamplePeriod,
            X = payload.X,
            Y = payload.Y,
            HeatmapZ = payload.HeatmapZ,
            HeatmapXCoordinates = payload.HeatmapXCoordinates,
            HeatmapYCoordinates = payload.HeatmapYCoordinates,
            HeatmapYAxisIsLog10OfQuantity = payload.HeatmapYAxisIsLog10OfQuantity,
            SegmentLines = payload.SegmentLines,
            HorizontalLimitLower = horizLo,
            HorizontalLimitUpper = horizHi,
            Categories = payload.Categories,
            BarGroups = payload.BarGroups,
            DonutFraction = payload.DonutFraction,
            ExplodeFraction = payload.ExplodeFraction,
            RadarAxisMaxValues = payload.RadarAxisMaxValues,
            Series = payload.Series,
            LayoutMode = payload.LayoutMode,
            ScalarAnnotations = payload.ScalarAnnotations?.Select(a => new ChartScalarAnnotation
            {
                Name = a.Name,
                Value = a.Value,
                Unit = a.Unit
            }).ToList()
        };
    }

    /// <summary>组合轴标题与单位，用于 ScottPlot 轴标签（单位为空则只显示标题）。</summary>
    public static string FormatAxisTitle(string label, string unit)
    {
        var l = label?.Trim() ?? string.Empty;
        var u = unit?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(l) && string.IsNullOrEmpty(u))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(u))
        {
            return l;
        }

        if (string.IsNullOrEmpty(l))
        {
            return u;
        }

        return $"{l} ({u})";
    }

    /// <summary>深拷贝，避免缓存与执行上下文共享数组引用。</summary>
    public ChartDisplayPayload Clone()
    {
        return new ChartDisplayPayload
        {
            Kind = Kind,
            BottomAxisLabel = BottomAxisLabel,
            BottomAxisUnit = BottomAxisUnit,
            LeftAxisLabel = LeftAxisLabel,
            LeftAxisUnit = LeftAxisUnit,
            SignalY = Clone1D(SignalY),
            SamplePeriod = SamplePeriod,
            X = Clone1D(X),
            Y = Clone1D(Y),
            HeatmapZ = Clone2D(HeatmapZ),
            HeatmapXCoordinates = Clone1D(HeatmapXCoordinates),
            HeatmapYCoordinates = Clone1D(HeatmapYCoordinates),
            HeatmapYAxisIsLog10OfQuantity = HeatmapYAxisIsLog10OfQuantity,
            SegmentLines = Clone2D(SegmentLines),
            HorizontalLimitLower = HorizontalLimitLower,
            HorizontalLimitUpper = HorizontalLimitUpper,
            Categories = Categories?.Select(c => new ChartCategoryItem { Label = c.Label, Value = c.Value, Color = c.Color }).ToList(),
            BarGroups = BarGroups?.Select(g => new ChartBarSeries { SeriesName = g.SeriesName, Values = Clone1D(g.Values)!, Color = g.Color }).ToList(),
            DonutFraction = DonutFraction,
            ExplodeFraction = ExplodeFraction,
            RadarAxisMaxValues = Clone1D(RadarAxisMaxValues),
            Series = Series?.Select(e => new ChartSeriesEntry
            {
                Name = e.Name,
                Color = e.Color,
                IsVisibleByDefault = e.IsVisibleByDefault,
                Data = e.Data.Clone()
            }).ToList(),
            LayoutMode = LayoutMode,
            ScalarAnnotations = ScalarAnnotations?.Select(a => new ChartScalarAnnotation
            {
                Name = a.Name,
                Value = a.Value,
                Unit = a.Unit
            }).ToList()
        };
    }

    /// <summary>与 <see cref="Clone"/> 相同数据，但清除 <see cref="HorizontalLimitLower"/> / <see cref="HorizontalLimitUpper"/>（主页不画合格带参考线）。</summary>
    public ChartDisplayPayload WithoutHorizontalLimitLines()
    {
        var c = Clone();
        return new ChartDisplayPayload
        {
            Kind = c.Kind,
            BottomAxisLabel = c.BottomAxisLabel,
            BottomAxisUnit = c.BottomAxisUnit,
            LeftAxisLabel = c.LeftAxisLabel,
            LeftAxisUnit = c.LeftAxisUnit,
            SignalY = c.SignalY,
            SamplePeriod = c.SamplePeriod,
            X = c.X,
            Y = c.Y,
            HeatmapZ = c.HeatmapZ,
            HeatmapXCoordinates = c.HeatmapXCoordinates,
            HeatmapYCoordinates = c.HeatmapYCoordinates,
            HeatmapYAxisIsLog10OfQuantity = c.HeatmapYAxisIsLog10OfQuantity,
            SegmentLines = c.SegmentLines,
            HorizontalLimitLower = null,
            HorizontalLimitUpper = null,
            Categories = c.Categories,
            BarGroups = c.BarGroups,
            DonutFraction = c.DonutFraction,
            ExplodeFraction = c.ExplodeFraction,
            RadarAxisMaxValues = c.RadarAxisMaxValues,
            Series = c.Series,
            LayoutMode = c.LayoutMode,
            ScalarAnnotations = c.ScalarAnnotations
        };
    }

    private static double[]? Clone1D(double[]? a)
    {
        if (a == null || a.Length == 0)
        {
            return null;
        }

        var c = new double[a.Length];
        Array.Copy(a, c, a.Length);
        return c;
    }

    private static double[,]? Clone2D(double[,]? m)
    {
        if (m == null)
        {
            return null;
        }

        var r0 = m.GetLength(0);
        var r1 = m.GetLength(1);
        var c = new double[r0, r1];
        Array.Copy(m, c, m.Length);
        return c;
    }

    private static bool TryMergeLimitDouble(object raw, out double value)
    {
        try
        {
            value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }
}

/// <summary>
/// 多系列中的单个条目：包含显示元数据（名称、颜色、默认可见性）和一个完整的 <see cref="ChartDisplayPayload"/>，
/// 支持任意图表类型。
/// </summary>
public sealed class ChartSeriesEntry
{
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public bool IsVisibleByDefault { get; init; } = true;
    public ChartDisplayPayload Data { get; init; } = new();
}

/// <summary>
/// 单个分类项：Bar 的每根柱子、Pie 的每个扇区、Radar 的每个轴。
/// </summary>
public sealed class ChartCategoryItem
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }

    /// <summary>十六进制颜色（如 "#FF6384"），为空时由渲染器自动分配。</summary>
    public string? Color { get; init; }
}

/// <summary>
/// 分组 / 堆叠柱状图中的一个系列。
/// </summary>
public sealed class ChartBarSeries
{
    public string SeriesName { get; init; } = string.Empty;
    public double[] Values { get; init; } = Array.Empty<double>();
    public string? Color { get; init; }
}

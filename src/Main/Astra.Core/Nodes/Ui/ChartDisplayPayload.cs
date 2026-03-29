using System.Collections.Generic;

namespace Astra.Core.Nodes.Ui
{
    /// <summary>
    /// 主页测试项图表可展示的载荷类型（与 <see cref="NodeUiOutputKeys.ChartPayloadSnapshot"/> / Raw 产物中的同类型对象对应）。
    /// </summary>
    public enum ChartPayloadKind
    {
        /// <summary>Y 随样本序号递增，等间隔 <see cref="SamplePeriod"/>（对应 NVH 单通道波形）。</summary>
        Signal1D,

        /// <summary>按 X、Y 数组顺序折线连接。</summary>
        XYLine,

        /// <summary>X、Y 散点，不强制连线（仅标记）。</summary>
        XYScatter,

        /// <summary>二维强度栅格；<see cref="HeatmapZ"/>[row, col] 行对应 <see cref="HeatmapYCoordinates"/>，列对应 <see cref="HeatmapXCoordinates"/>。</summary>
        Heatmap,

        /// <summary>多段线段，每行 (x0,y0,x1,y1)。</summary>
        LineSegments
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

        // --- Segments [segmentIndex, 0..3] = x0,y0,x1,y1 ---
        public double[,]? SegmentLines { get; init; }

        /// <summary>可选：在图上绘制水平参考线（卡控下限）。</summary>
        public double? HorizontalLimitLower { get; init; }

        /// <summary>可选：在图上绘制水平参考线（卡控上限）。</summary>
        public double? HorizontalLimitUpper { get; init; }

        /// <summary>
        /// 将 <paramref name="outputData"/> 中的轴标题/单位（若存在键）覆盖到 <paramref name="payload"/>，用于执行结果与 Raw/内联快照合并。
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
                SegmentLines = payload.SegmentLines,
                HorizontalLimitLower = payload.HorizontalLimitLower,
                HorizontalLimitUpper = payload.HorizontalLimitUpper
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
                SegmentLines = Clone2D(SegmentLines),
                HorizontalLimitLower = HorizontalLimitLower,
                HorizontalLimitUpper = HorizontalLimitUpper
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
    }
}

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

        /// <summary>左侧轴标题（如幅值）。</summary>
        public string LeftAxisLabel { get; init; } = string.Empty;

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

        /// <summary>深拷贝，避免缓存与执行上下文共享数组引用。</summary>
        public ChartDisplayPayload Clone()
        {
            return new ChartDisplayPayload
            {
                Kind = Kind,
                BottomAxisLabel = BottomAxisLabel,
                LeftAxisLabel = LeftAxisLabel,
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

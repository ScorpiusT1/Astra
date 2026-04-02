using Astra.UI.Abstractions.Nodes;

namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>
    /// 根据横轴坐标是否等间隔，选择 <see cref="ChartPayloadKind.Signal1D"/>、<see cref="ChartPayloadKind.XYLine"/> 或 <see cref="ChartPayloadKind.XYScatter"/>。
    /// </summary>
    internal static class SpectralChartPayloadHelper
    {
        /// <summary>判断步长是否与参考步长在相对容差内一致。</summary>
        private const double RelativeTolerance = 1e-9;

        /// <summary>
        /// 横轴从 0 开始且步长恒定 → <see cref="ChartDisplayPayloadFactory.Signal1D"/>；
        /// 横轴步长恒定但起点非零 → <see cref="ChartDisplayPayloadFactory.XYLine"/>（显式 X）；
        /// 步长不一致 → <see cref="ChartDisplayPayloadFactory.XYScatter"/>。
        /// </summary>
        public static ChartDisplayPayload CreateAdaptive(
            double[] x,
            double[] y,
            string bottomAxisLabel,
            string leftAxisLabel,
            double? horizontalLower = null,
            double? horizontalUpper = null)
        {
            if (x == null || y == null || x.Length != y.Length)
            {
                return ChartDisplayPayloadFactory.XYLine(
                    x ?? Array.Empty<double>(),
                    y ?? Array.Empty<double>(),
                    bottomAxisLabel,
                    leftAxisLabel,
                    horizontalLower,
                    horizontalUpper);
            }

            if (x.Length == 0)
            {
                return ChartDisplayPayloadFactory.XYLine(x, y, bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
            }

            if (x.Length == 1)
            {
                return ChartDisplayPayloadFactory.XYScatter(x, y, bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
            }

            if (!TryGetUniformStep(x, out var step))
            {
                return ChartDisplayPayloadFactory.XYScatter(x, y, bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
            }

            if (IsApproximatelyUniformGridFromZero(x, step))
            {
                return ChartDisplayPayloadFactory.Signal1D(
                    y,
                    step > 0 ? step : 1.0,
                    bottomAxisLabel,
                    leftAxisLabel,
                    horizontalLower,
                    horizontalUpper);
            }

            return ChartDisplayPayloadFactory.XYLine(x, y, bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
        }

        /// <summary>
        /// 检测严格递增且相邻间隔在容差内相等。
        /// </summary>
        private static bool TryGetUniformStep(double[] x, out double step)
        {
            step = x[1] - x[0];
            var scale = Math.Max(Math.Abs(step), 1.0);
            if (step <= 0)
            {
                return false;
            }

            for (var i = 2; i < x.Length; i++)
            {
                var d = x[i] - x[i - 1];
                if (Math.Abs(d - step) > RelativeTolerance * scale)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 是否满足 <see cref="ChartPayloadKind.Signal1D"/>：已判定等步长，且横轴起点接近 0（与 ScottPlot Signal 的 x=i·Δ 一致）。
        /// 不逐项校验 x[i]≈i·step，避免长序列浮点累加误差误判。
        /// </summary>
        private static bool IsApproximatelyUniformGridFromZero(double[] x, double step)
        {
            var scale = Math.Max(Math.Abs(step), 1.0);
            var x0Tol = RelativeTolerance * Math.Max(Math.Abs(x[^1]), scale);
            return Math.Abs(x[0]) <= x0Tol;
        }
    }
}

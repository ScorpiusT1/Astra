namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>
    /// 将算法输出的时间坐标平移到「原始波形」时间轴（截取后首样本在全长信号中的时刻为 <paramref name="originSeconds"/>）。
    /// </summary>
    internal static class AlgorithmTimeAxisHelper
    {
        internal static void ApplyAnalysisOriginInPlace(double[]? axis, double originSeconds)
        {
            if (axis == null || originSeconds == 0)
                return;
            for (var i = 0; i < axis.Length; i++)
                axis[i] += originSeconds;
        }
    }
}

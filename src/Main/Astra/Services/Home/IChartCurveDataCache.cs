using System.Collections.Generic;

namespace Astra.Services.Home
{
    /// <summary>
    /// 缓存最近一次执行中各节点可供图表展示的曲线采样（执行结束后 Raw 存储已回收，故在节点完成时快照）。
    /// </summary>
    public interface IChartCurveDataCache
    {
        void SetSamplesForNode(string nodeId, IReadOnlyList<double> samples);

        bool TryGetSamplesForNode(string nodeId, out IReadOnlyList<double>? samples);
    }
}

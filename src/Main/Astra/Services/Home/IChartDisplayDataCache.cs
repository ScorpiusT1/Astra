using Astra.Core.Nodes.Ui;

namespace Astra.Services.Home
{
    /// <summary>
    /// 缓存各测试项节点最近一次执行的可视化图表载荷（支持一维信号、XY、热力图、线段等）。
    /// </summary>
    public interface IChartDisplayDataCache
    {
        void SetPayload(string nodeId, ChartDisplayPayload payload);

        bool TryGetPayload(string nodeId, out ChartDisplayPayload? payload);
    }
}

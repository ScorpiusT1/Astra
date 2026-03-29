using Astra.Core.Nodes.Models;

namespace Astra.Core.Nodes.Ui
{
    /// <summary>
    /// 在节点完成时基于 <see cref="NodeContext"/> 与 <see cref="ExecutionResult"/> 做 UI 侧后处理（如图表数据快照）。
    /// </summary>
    public interface INodeExecutionUiHydrator
    {
        void OnNodeExecutionCompleted(NodeContext? context, string nodeId, ExecutionResult? result);
    }
}

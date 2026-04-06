using Astra.Core.Nodes.Models;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 单次工作流执行内的成块日志会话（流程块嵌套节点块，子流程内容挂入父流程节点块）。
    /// </summary>
    public interface IExecutionRunLogSession : System.IAsyncDisposable
    {
        string ExecutionId { get; }

        /// <summary>写入当前节点缓冲；无活动节点时直接落盘。</summary>
        void Write(string level, string message);

        void PushWorkflowScope(string workflowId, string workflowName, string workFlowKey);

        void PopWorkflowScope();

        void PushNodeScope(Node node);

        void PopNodeScope();

        /// <summary>在已知 SN 后尝试重命名日志文件（占位符 → 实际 SN）。</summary>
        void TryRenameFileWithSerialNumber(string serialNumber);
    }
}

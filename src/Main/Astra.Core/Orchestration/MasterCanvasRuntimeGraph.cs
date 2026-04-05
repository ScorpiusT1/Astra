using System.Collections.Generic;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Orchestration
{
    /// <summary>
    /// 主流程画布运行时拓扑（与编辑器中的节点/连线实例一致），供编排器按连线与插件节点交错执行。
    /// </summary>
    public sealed class MasterCanvasRuntimeGraph
    {
        /// <summary>当前主流程模型 Id（与脚本中主流程数据 Id 一致），用于 UI 事件与首页测试项关联。</summary>
        public string? MasterWorkflowId { get; init; }

        public IReadOnlyList<Node> Nodes { get; init; } = System.Array.Empty<Node>();
        public IReadOnlyList<Edge> Edges { get; init; } = System.Array.Empty<Edge>();
    }
}

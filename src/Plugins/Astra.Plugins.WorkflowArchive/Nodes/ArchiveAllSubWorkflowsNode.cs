namespace Astra.Plugins.WorkflowArchive.Nodes
{
    /// <summary>
    /// 与 <see cref="WorkflowArchiveNode"/> 使用同一归档服务与报告选项；语义上强调报告白名单可涵盖全部子流程内节点。
    /// 请放在子流程末尾执行；多流程编辑器会在切换标签、加载工程、打开节点属性等时机刷新「全部子流程」节点列表。
    /// </summary>
    public sealed class ArchiveAllSubWorkflowsNode : WorkflowArchiveNode
    {
        public ArchiveAllSubWorkflowsNode()
        {
            NodeType = "ArchiveAllSubWorkflowsNode";
            Name = "归档（全部子流程）";
        }
    }
}

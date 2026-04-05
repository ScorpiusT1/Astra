using System;
using System.Collections.Generic;
using Astra.UI.Controls;

namespace Astra.UI.Helpers
{
    /// <summary>
    /// 主流程画布上与流程引用块控件使用相同外框的节点类型（插件类型名为字符串，避免 UI 引用插件程序集）。
    /// </summary>
    public static class MasterWorkflowCanvasChrome
    {
        public static readonly HashSet<string> PluginTypesUsingWorkflowReferenceChrome = new(StringComparer.Ordinal)
        {
            "UpstreamTestAggregationNode",
            "MasterWorkflowDelayNode",
        };

        public static bool UsesWorkflowReferenceChrome(string? nodeTypeName)
        {
            if (string.IsNullOrEmpty(nodeTypeName))
                return false;
            if (string.Equals(nodeTypeName, "WorkflowReferenceNode", StringComparison.Ordinal))
                return true;
            // 工具箱清单常用完整类型名（如 Astra.Plugins.Logic.Nodes.UpstreamTestAggregationNode），与节点上 Type.Name 对齐
            var shortName = GetNodeTypeShortName(nodeTypeName);
            return PluginTypesUsingWorkflowReferenceChrome.Contains(nodeTypeName)
                || PluginTypesUsingWorkflowReferenceChrome.Contains(shortName);
        }

        private static string GetNodeTypeShortName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeName;
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot < typeName.Length - 1
                ? typeName[(lastDot + 1)..]
                : typeName;
        }

        public static bool ToolItemUsesWorkflowReferenceChrome(IToolItem? tool) =>
            UsesWorkflowReferenceChrome(ResolveNodeTypeName(tool?.NodeType));

        /// <summary>
        /// 工具箱拖拽预览是否使用 <see cref="WorkflowReferenceNodeControl"/> 式大块预览。
        /// 延时、上游聚合等插件节点在主/子流程工具箱拖出时均使用与子流程一致的 NodeControl 式紧凑预览；
        /// 仅真正的流程引用块类型保留大块预览（若将来从工具箱拖出）。
        /// </summary>
        public static bool ToolItemUsesWorkflowReferenceDragPreview(IToolItem? tool)
        {
            var name = ResolveNodeTypeName(tool?.NodeType);
            if (string.IsNullOrEmpty(name))
                return false;
            if (string.Equals(name, "WorkflowReferenceNode", StringComparison.Ordinal))
                return true;
            var shortName = GetNodeTypeShortName(name);
            return string.Equals(shortName, "WorkflowReferenceNode", StringComparison.Ordinal);
        }

        private static string? ResolveNodeTypeName(object? nodeType)
        {
            return nodeType switch
            {
                Type t => t.Name,
                string s => s,
                _ => null
            };
        }
    }
}

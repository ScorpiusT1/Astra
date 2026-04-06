using System;
using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 子流程 <see cref="WorkFlowNode.PersistedParameters"/> 中允许写入脚本的键名（与运行期 <see cref="WorkFlowNode.Variables"/> 分离）。
    /// 新增可持久化项时在此登记，并在旧脚本 <c>Variables</c> 迁移逻辑中生效。
    /// </summary>
    public static class WorkFlowPersistedParameterKeys
    {
        /// <summary>流程编辑器画布缩放（百分比）。</summary>
        public const string ZoomPercentage = "ZoomPercentage";

        private static readonly HashSet<string> PersistedKeys = new(StringComparer.Ordinal)
        {
            ZoomPercentage
        };

        public static bool IsPersistedKey(string? key) =>
            !string.IsNullOrEmpty(key) && PersistedKeys.Contains(key);
    }
}

using System.Collections.Generic;

namespace Astra.Core.Orchestration
{
    public sealed class MasterExecutionOptions
    {
        public string? Sn { get; init; }
        public Dictionary<string, object>? InitialGlobalVariables { get; init; }

        /// <summary>
        /// 主流程画布完整拓扑。非 null 且包含非引用类节点时，编排器在主阶段按连线拓扑执行子流程与插件节点。
        /// </summary>
        public MasterCanvasRuntimeGraph? MasterCanvasRuntime { get; init; }
    }
}

using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using Astra.UI.Services;

namespace Astra.Services.Startup
{
    /// <summary>
    /// 默认工作流引擎提供器（组合根负责绑定具体实现）。
    /// </summary>
    public sealed class DefaultWorkflowEngineProvider : IWorkflowEngineProvider
    {
        public IWorkFlowEngine Create()
        {
            return WorkFlowEngineFactory.CreateDefault();
        }
    }
}

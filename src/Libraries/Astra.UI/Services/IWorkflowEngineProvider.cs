using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;

namespace Astra.UI.Services
{
    /// <summary>
    /// 工作流引擎提供器（依赖倒置），用于替代 UI 层反射创建引擎。
    /// </summary>
    public interface IWorkflowEngineProvider
    {
        IWorkFlowEngine Create();
    }
}

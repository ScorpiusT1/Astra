using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using System;

namespace Astra.UI.Services
{
    /// <summary>
    /// 工作流引擎提供器（依赖倒置），用于替代 UI 层反射创建引擎。
    /// </summary>
    public interface IWorkflowEngineProvider
    {
        IWorkFlowEngine Create();

        /// <summary>
        /// 创建带节点事件桥接的引擎：引擎的 NodeExecutionStarted/Completed 将自动转发到
        /// <see cref="NodeExecutionChanged"/> 事件。
        /// </summary>
        IWorkFlowEngine CreateWithNodeEventBridge();

        /// <summary>
        /// 由 <see cref="CreateWithNodeEventBridge"/> 创建的引擎在节点执行时触发此事件。
        /// </summary>
        event EventHandler<WorkflowNodeExecutionChangedEventArgs>? NodeExecutionChanged;
    }
}

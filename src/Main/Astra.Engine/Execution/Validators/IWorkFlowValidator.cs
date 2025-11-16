using Astra.Core.Nodes.Models;

namespace Astra.Engine.Execution.Validators
{
    /// <summary>
    /// 工作流验证器接口
    /// 定义工作流验证的抽象接口
    /// </summary>
    public interface IWorkFlowValidator
    {
        /// <summary>
        /// 验证工作流
        /// </summary>
        /// <param name="workflow">要验证的工作流</param>
        /// <returns>验证结果</returns>
        ValidationResult Validate(WorkFlowNode workflow);
    }
}


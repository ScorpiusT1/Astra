using Astra.Core.Nodes.Models;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Engine.Execution.Validators
{
    /// <summary>
    /// 默认工作流验证器
    /// 验证工作流的节点和连接的有效性
    /// </summary>
    public class DefaultWorkFlowValidator : IWorkFlowValidator
    {
        /// <summary>
        /// 验证工作流
        /// </summary>
        public ValidationResult Validate(WorkFlowNode workflow)
        {
            var errors = new List<string>();

            if (!workflow.Nodes.Any())
            {
                errors.Add("工作流中没有节点");
            }

            foreach (var node in workflow.Nodes)
            {
                var nodeValidation = node.Validate();
                if (!nodeValidation.IsValid)
                {
                    errors.Add($"节点 '{node.Name}' 验证失败: {string.Join(", ", nodeValidation.Errors)}");
                }
            }

            foreach (var conn in workflow.Connections)
            {
                var connValidation = conn.Validate();
                if (!connValidation.IsValid)
                {
                    errors.Add($"连接 {conn.Id} 验证失败: {string.Join(", ", connValidation.Errors)}");
                }
            }

            return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
        }
    }
}


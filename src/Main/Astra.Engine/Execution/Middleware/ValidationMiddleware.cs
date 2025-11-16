using Astra.Core.Nodes.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 验证中间件
    /// 在执行节点前验证节点配置的有效性
    /// </summary>
    public class ValidationMiddleware : INodeMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public async Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next)
        {
            // 执行节点验证
            var validation = node.Validate();
            if (!validation.IsValid)
            {
                return ExecutionResult.Failed($"节点验证失败: {string.Join(", ", validation.Errors)}");
            }

            // 验证通过，继续执行
            return await next(cancellationToken);
        }
    }
}


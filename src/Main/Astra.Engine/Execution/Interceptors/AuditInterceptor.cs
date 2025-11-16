using Astra.Core.Nodes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Interceptors
{
    /// <summary>
    /// 审计拦截器
    /// 记录节点执行的审计信息，用于追踪和审计
    /// </summary>
    public class AuditInterceptor : INodeInterceptor
    {
        /// <summary>
        /// 节点执行前调用
        /// </summary>
        public Task OnBeforeExecuteAsync(Node node, NodeContext context, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📝 [审计] 节点开始执行: {node.Name}, 时间: {DateTime.Now:HH:mm:ss.fff}");
            // 记录到审计日志
            return Task.CompletedTask;
        }

        /// <summary>
        /// 节点执行后调用
        /// </summary>
        public Task OnAfterExecuteAsync(Node node, ExecutionResult result, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📝 [审计] 节点执行完成: {node.Name}, 结果: {result.Success}, 时间: {DateTime.Now:HH:mm:ss.fff}");
            // 记录到审计日志
            return Task.CompletedTask;
        }

        /// <summary>
        /// 节点执行异常时调用
        /// </summary>
        public Task OnExceptionAsync(Node node, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📝 [审计] 节点执行异常: {node.Name}, 异常: {exception.Message}");
            // 记录到审计日志
            return Task.CompletedTask;
        }
    }
}


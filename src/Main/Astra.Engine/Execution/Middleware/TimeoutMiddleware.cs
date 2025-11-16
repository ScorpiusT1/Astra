using Astra.Core.Nodes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 超时中间件
    /// 为节点执行设置超时限制，防止长时间阻塞
    /// </summary>
    public class TimeoutMiddleware : INodeMiddleware
    {
        private readonly int _timeoutSeconds;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        public TimeoutMiddleware(int timeoutSeconds)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public async Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                return await next(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                return ExecutionResult.Failed($"节点 {node.Name} 执行超时（{_timeoutSeconds}秒）");
            }
        }
    }
}


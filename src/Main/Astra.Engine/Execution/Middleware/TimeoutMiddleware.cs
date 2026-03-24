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
        private readonly int _timeoutMilliseconds;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="timeoutMilliseconds">超时时间（毫秒）</param>
        public TimeoutMiddleware(int timeoutMilliseconds)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
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
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_timeoutMilliseconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                return await next(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                return ExecutionResult.Timeout($"节点 {node.Name} 执行超时（{_timeoutMilliseconds}ms）", _timeoutMilliseconds / 1000);
            }
        }
    }
}


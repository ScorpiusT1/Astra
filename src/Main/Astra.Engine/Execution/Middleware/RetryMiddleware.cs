using Astra.Core.Nodes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 重试中间件
    /// 在节点执行失败时自动重试，提高系统的容错能力
    /// </summary>
    public class RetryMiddleware : INodeMiddleware
    {
        private readonly int _maxRetries;
        private readonly int _delayMs;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayMs">重试间隔（毫秒）</param>
        public RetryMiddleware(int maxRetries = 3, int delayMs = 1000)
        {
            _maxRetries = maxRetries;
            _delayMs = delayMs;
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
            int attempt = 0;
            Exception lastException = null;

            while (attempt < _maxRetries)
            {
                try
                {
                    attempt++;
                    Console.WriteLine($"🔄 [重试] 节点 {node.Name} 第 {attempt} 次尝试");

                    var result = await next(cancellationToken);

                    if (result.Success)
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"✅ [重试成功] 节点 {node.Name} 在第 {attempt} 次尝试后成功");
                        }
                        return result;
                    }

                    lastException = result.Exception;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine($"❌ [重试失败] 节点 {node.Name} 第 {attempt} 次尝试失败: {ex.Message}");
                }

                if (attempt < _maxRetries)
                {
                    await Task.Delay(_delayMs, cancellationToken);
                }
            }

            return ExecutionResult.Failed(
                $"节点 {node.Name} 在 {_maxRetries} 次重试后仍然失败",
                lastException);
        }
    }
}


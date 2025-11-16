using Astra.Core.Nodes.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 性能监控中间件
    /// 监控节点执行时间，并在超过阈值时发出警告
    /// </summary>
    public class PerformanceMiddleware : INodeMiddleware
    {
        private readonly int _warningThresholdMs;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="warningThresholdMs">警告阈值（毫秒），超过此时间将输出警告</param>
        public PerformanceMiddleware(int warningThresholdMs = 1000)
        {
            _warningThresholdMs = warningThresholdMs;
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
            var sw = Stopwatch.StartNew();

            var result = await next(cancellationToken);

            sw.Stop();

            if (sw.ElapsedMilliseconds > _warningThresholdMs)
            {
                Console.WriteLine($"⚠️  [性能警告] 节点 {node.Name} 执行时间过长: {sw.ElapsedMilliseconds}ms");
            }

            // 将性能数据添加到结果中
            result.OutputData["ExecutionTimeMs"] = sw.ElapsedMilliseconds;

            return result;
        }
    }
}


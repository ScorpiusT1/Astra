using Astra.Core.Nodes.Models;
using Astra.Core.Logs;
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
        private readonly Action<Node, long> _onWarning;
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="warningThresholdMs">警告阈值（毫秒），超过此时间将输出警告</param>
        /// <param name="onWarning">警告处理器，当执行超时时调用</param>
        /// <param name="logger">日志记录器</param>
        public PerformanceMiddleware(
            int warningThresholdMs = 1000,
            Action<Node, long> onWarning = null,
            ILogger logger = null)
        {
            _warningThresholdMs = warningThresholdMs;
            _onWarning = onWarning;
            _logger = logger;
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

            // 将性能数据添加到结果中
            result.OutputData["ExecutionTimeMs"] = sw.ElapsedMilliseconds;

            // 检查是否超过阈值
            if (sw.ElapsedMilliseconds > _warningThresholdMs)
            {
                if (_onWarning != null)
                {
                    _onWarning(node, sw.ElapsedMilliseconds);
                }
                else
                {
                    DefaultWarningHandler(node, sw.ElapsedMilliseconds, context);
                }
            }

            return result;
        }

        /// <summary>
        /// 默认警告处理器
        /// </summary>
        private void DefaultWarningHandler(Node node, long elapsedMs, NodeContext context)
        {
            var logger = _logger ?? ResolveLogger(context);
            if (logger != null)
            {
                logger.Warn($"节点 {node.Name} 执行时间过长: {elapsedMs}ms");
            }
        }

        /// <summary>
        /// 从上下文解析日志记录器
        /// </summary>
        private ILogger ResolveLogger(NodeContext context)
        {
            try
            {
                return context?.ServiceProvider?.GetService(typeof(Logger)) as Logger;
            }
            catch
            {
                return null;
            }
        }
    }
}


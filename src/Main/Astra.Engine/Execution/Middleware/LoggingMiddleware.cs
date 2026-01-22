using Astra.Core.Logs.Extensions;
using Astra.Core.Nodes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 日志中间件
    /// 负责记录节点执行的开始、完成和错误信息
    /// </summary>
    public class LoggingMiddleware : INodeMiddleware
    {
        private readonly ILogger _logger;
        private readonly string _fallbackLoggerName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器实例，如果为null则从上下文解析</param>
        /// <param name="fallbackLoggerName">备用日志记录器名称，当无法从上下文解析时使用</param>
        public LoggingMiddleware(ILogger logger = null, string fallbackLoggerName = "NodePipeline")
        {
            _logger = logger;
            _fallbackLoggerName = fallbackLoggerName;
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
            var logger = ResolveLogger(context) ?? NullLogger.Instance;

            logger.LogNodeStart(node);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await next(cancellationToken);
                stopwatch.Stop();

                logger.LogNodeComplete(node, stopwatch.Elapsed, result);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogNodeError(node, ex, stopwatch.Elapsed);
                throw;
            }
        }

        /// <summary>
        /// 解析日志记录器
        /// 优先从DI容器解析，如果失败则使用备用记录器
        /// </summary>
        private ILogger ResolveLogger(NodeContext context)
        {
            try
            {
                // 优先从DI容器解析 ILoggerFactory
                var sp = context?.ServiceProvider;
                if (sp != null)
                {
                    var loggerFactory = sp.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                    if (loggerFactory != null)
                    {
                        return loggerFactory.CreateLogger(_fallbackLoggerName);
                    }
                    
                    // 尝试直接解析 ILogger<LoggingMiddleware>
                    var logger = sp.GetService(typeof(ILogger<LoggingMiddleware>)) as ILogger;
                    if (logger != null)
                    {
                        return logger;
                    }
                }
            }
            catch
            {
                // 解析失败时降级为 fallback
            }
            return _logger ?? NullLogger.Instance;
        }
    }
}


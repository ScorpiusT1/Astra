using Astra.Core.Nodes.Models;
using Astra.Core.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 重试中间件
    /// 在节点执行失败时自动重试，提高系统的容错能力
    /// 支持自定义延迟策略和条件重试
    /// </summary>
    public class RetryMiddleware : INodeMiddleware
    {
        private readonly int _maxRetries;
        private readonly Func<int, int> _delayStrategy;
        private readonly Func<Exception, bool> _retryPredicate;
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayStrategy">延迟策略函数，输入尝试次数，返回延迟毫秒数。默认为固定 1000ms</param>
        /// <param name="retryPredicate">重试谓词，判断是否应该对特定异常进行重试。默认所有异常都重试</param>
        /// <param name="logger">日志记录器</param>
        public RetryMiddleware(
            int maxRetries = 3,
            Func<int, int> delayStrategy = null,
            Func<Exception, bool> retryPredicate = null,
            ILogger logger = null)
        {
            _maxRetries = maxRetries;
            _delayStrategy = delayStrategy ?? (attempt => 1000); // 默认固定延迟
            _retryPredicate = retryPredicate ?? (ex => true); // 默认所有异常都重试
            _logger = logger;
        }

        /// <summary>
        /// 创建指数退避策略的重试中间件
        /// </summary>
        public static RetryMiddleware WithExponentialBackoff(int maxRetries = 3, int initialDelayMs = 1000, ILogger logger = null)
        {
            return new RetryMiddleware(
                maxRetries,
                attempt => initialDelayMs * (int)Math.Pow(2, attempt - 1),
                logger: logger
            );
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
            var logger = _logger ?? ResolveLogger(context);

            while (attempt < _maxRetries)
            {
                try
                {
                    attempt++;
                    logger?.Info($"重试节点 {node.Name} 第 {attempt} 次尝试");

                    var result = await next(cancellationToken);

                    if (result.Success)
                    {
                        if (attempt > 1)
                        {
                            logger?.Info($"节点 {node.Name} 在第 {attempt} 次尝试后成功");
                        }
                        return result;
                    }

                    lastException = result.Exception;
                    
                    // 检查是否应该重试
                    if (lastException != null && !_retryPredicate(lastException))
                    {
                        logger?.Warn($"节点 {node.Name} 异常不支持重试: {lastException.GetType().Name}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    logger?.Warn($"节点 {node.Name} 第 {attempt} 次尝试失败: {ex.Message}");
                    
                    // 检查是否应该重试
                    if (!_retryPredicate(ex))
                    {
                        logger?.Warn($"节点 {node.Name} 异常不支持重试: {ex.GetType().Name}");
                        throw;
                    }
                }

                if (attempt < _maxRetries)
                {
                    var delay = _delayStrategy(attempt);
                    logger?.Debug($"等待 {delay}ms 后进行下一次重试");
                    await Task.Delay(delay, cancellationToken);
                }
            }

            logger?.Error($"节点 {node.Name} 在 {_maxRetries} 次重试后仍然失败");
            return ExecutionResult.Failed(
                $"节点 {node.Name} 在 {_maxRetries} 次重试后仍然失败",
                lastException,
                "RETRY_EXHAUSTED"
            );
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


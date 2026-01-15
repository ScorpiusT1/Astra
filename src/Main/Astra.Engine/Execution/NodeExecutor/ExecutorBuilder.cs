using Astra.Core.Logs;
using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.Interceptors;
using Astra.Engine.Execution.Middleware;
using System;
using System.Collections.Generic;

namespace Astra.Engine.Execution.NodeExecutor
{
    /// <summary>
    /// 节点执行器构建器
    /// 提供流式API来配置节点执行器，提高易用性
    /// 符合建造者模式
    /// </summary>
    public class ExecutorBuilder
    {
        private readonly List<INodeMiddleware> _middlewares = new List<INodeMiddleware>();
        private readonly List<INodeInterceptor> _interceptors = new List<INodeInterceptor>();

        /// <summary>
        /// 添加验证中间件
        /// </summary>
        public ExecutorBuilder WithValidation()
        {
            _middlewares.Add(new ValidationMiddleware());
            return this;
        }

        /// <summary>
        /// 添加日志中间件
        /// </summary>
        /// <param name="logger">日志记录器，如果为null则从上下文解析</param>
        /// <param name="fallbackLoggerName">备用日志记录器名称</param>
        public ExecutorBuilder WithLogging(Logger logger = null, string fallbackLoggerName = "NodePipeline")
        {
            _middlewares.Add(new LoggingMiddleware(logger, fallbackLoggerName));
            return this;
        }

        /// <summary>
        /// 添加重试中间件（固定延迟）
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayMs">重试延迟（毫秒）</param>
        /// <param name="logger">日志记录器</param>
        public ExecutorBuilder WithRetry(int maxRetries = 3, int delayMs = 1000, ILogger logger = null)
        {
            _middlewares.Add(new RetryMiddleware(
                maxRetries,
                delayStrategy: attempt => delayMs,
                logger: logger
            ));
            return this;
        }

        /// <summary>
        /// 添加重试中间件（指数退避）
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="initialDelayMs">初始延迟（毫秒）</param>
        /// <param name="logger">日志记录器</param>
        public ExecutorBuilder WithExponentialBackoffRetry(int maxRetries = 3, int initialDelayMs = 1000, ILogger logger = null)
        {
            _middlewares.Add(RetryMiddleware.WithExponentialBackoff(maxRetries, initialDelayMs, logger));
            return this;
        }

        /// <summary>
        /// 添加自定义重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayStrategy">延迟策略函数</param>
        /// <param name="retryPredicate">重试谓词</param>
        /// <param name="logger">日志记录器</param>
        public ExecutorBuilder WithCustomRetry(
            int maxRetries,
            Func<int, int> delayStrategy,
            Func<Exception, bool> retryPredicate = null,
            ILogger logger = null)
        {
            _middlewares.Add(new RetryMiddleware(maxRetries, delayStrategy, retryPredicate, logger));
            return this;
        }

        /// <summary>
        /// 添加性能监控中间件
        /// </summary>
        /// <param name="thresholdMs">警告阈值（毫秒）</param>
        /// <param name="onWarning">警告处理器</param>
        /// <param name="logger">日志记录器</param>
        public ExecutorBuilder WithPerformanceMonitoring(
            int thresholdMs = 1000,
            Action<Node, long> onWarning = null,
            ILogger logger = null)
        {
            _middlewares.Add(new PerformanceMiddleware(thresholdMs, onWarning, logger));
            return this;
        }

        /// <summary>
        /// 添加超时中间件
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        public ExecutorBuilder WithTimeout(int timeoutMs = 30000)
        {
            _middlewares.Add(new TimeoutMiddleware(timeoutMs));
            return this;
        }

        /// <summary>
        /// 添加缓存中间件
        /// </summary>
        /// <param name="cacheSeconds">缓存有效期（秒）</param>
        public ExecutorBuilder WithCache(int cacheSeconds = 60)
        {
            _middlewares.Add(new CacheMiddleware(cacheSeconds));
            return this;
        }

        /// <summary>
        /// 添加条件执行中间件
        /// </summary>
        public ExecutorBuilder WithConditional()
        {
            _middlewares.Add(new ConditionalMiddleware());
            return this;
        }

        /// <summary>
        /// 添加自定义中间件
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        public ExecutorBuilder WithMiddleware(INodeMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Add(middleware);
            return this;
        }

        /// <summary>
        /// 添加审计拦截器
        /// </summary>
        public ExecutorBuilder WithAudit()
        {
            _interceptors.Add(new AuditInterceptor());
            return this;
        }

        /// <summary>
        /// 添加权限拦截器
        /// </summary>
        /// <param name="permissionService">权限服务实例</param>
        public ExecutorBuilder WithPermission(IPermissionService permissionService)
        {
            if (permissionService == null)
                throw new ArgumentNullException(nameof(permissionService));
            
            _interceptors.Add(new PermissionInterceptor(permissionService));
            return this;
        }

        /// <summary>
        /// 添加自定义拦截器
        /// </summary>
        /// <param name="interceptor">拦截器实例</param>
        public ExecutorBuilder WithInterceptor(INodeInterceptor interceptor)
        {
            if (interceptor == null) throw new ArgumentNullException(nameof(interceptor));
            _interceptors.Add(interceptor);
            return this;
        }

        /// <summary>
        /// 构建节点执行器
        /// </summary>
        /// <returns>配置好的节点执行器实例</returns>
        public INodeExecutor Build()
        {
            var executor = new DefaultNodeExecutor();

            // 按顺序添加中间件
            foreach (var middleware in _middlewares)
            {
                executor.Use(middleware);
            }

            // 添加拦截器
            foreach (var interceptor in _interceptors)
            {
                executor.AddInterceptor(interceptor);
            }

            return executor;
        }

        /// <summary>
        /// 创建标准配置（用于生产环境）
        /// 包含：验证、条件、日志、性能监控、审计
        /// </summary>
        public static ExecutorBuilder CreateStandard()
        {
            return new ExecutorBuilder()
                .WithValidation()
                .WithConditional()
                .WithLogging()
                .WithPerformanceMonitoring(500)
                .WithAudit();
        }

        /// <summary>
        /// 创建开发配置
        /// 包含：验证、日志、详细性能监控
        /// </summary>
        public static ExecutorBuilder CreateDevelopment()
        {
            return new ExecutorBuilder()
                .WithValidation()
                .WithLogging()
                .WithPerformanceMonitoring(200); // 更严格的性能阈值
        }

        /// <summary>
        /// 创建高可用配置
        /// 包含：验证、重试（指数退避）、超时、日志、性能监控、审计
        /// </summary>
        public static ExecutorBuilder CreateHighAvailability()
        {
            return new ExecutorBuilder()
                .WithValidation()
                .WithTimeout(30000)
                .WithExponentialBackoffRetry(maxRetries: 5, initialDelayMs: 1000)
                .WithLogging()
                .WithPerformanceMonitoring(1000)
                .WithAudit();
        }
    }
}

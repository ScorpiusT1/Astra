using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.Interceptors;
using Astra.Engine.Execution.Middleware;
using Astra.Engine.Execution.WorkFlowEngine;
using System;

namespace Astra.Engine.Execution.NodeExecutor
{
    /// <summary>
    /// 节点执行器工厂
    /// 提供创建和配置节点执行器的便捷方法
    /// </summary>
    public static class NodeExecutorFactory
    {
        private static INodeExecutor _defaultExecutor;

        /// <summary>
        /// 获取默认执行器（单例）
        /// </summary>
        /// <returns>默认节点执行器实例</returns>
        public static INodeExecutor GetDefaultExecutor()
        {
            if (_defaultExecutor == null)
            {
                _defaultExecutor = CreateStandardExecutor();
            }
            return _defaultExecutor;
        }

        /// <summary>
        /// 设置默认执行器
        /// </summary>
        /// <param name="executor">要设置为默认的执行器实例</param>
        public static void SetDefaultExecutor(INodeExecutor executor)
        {
            _defaultExecutor = executor;
        }

        /// <summary>
        /// 创建标准执行器（带常用中间件）
        /// 包含：验证、条件判断、日志、性能监控和审计拦截器
        /// </summary>
        /// <returns>配置好的标准执行器</returns>
        public static INodeExecutor CreateStandardExecutor()
        {
            return ExecutorBuilder.CreateStandard().Build();
        }

        /// <summary>
        /// 创建带重试的执行器
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>配置了重试功能的执行器</returns>
        public static INodeExecutor CreateRetryableExecutor(int maxRetries = 3)
        {
            return new ExecutorBuilder()
                .WithValidation()
                .WithLogging()
                .WithRetry(maxRetries)
                .WithPerformanceMonitoring()
                .Build();
        }

        /// <summary>
        /// 创建带缓存的执行器
        /// </summary>
        /// <param name="cacheSeconds">缓存有效期（秒）</param>
        /// <returns>配置了缓存功能的执行器</returns>
        public static INodeExecutor CreateCachedExecutor(int cacheSeconds = 60)
        {
            return new ExecutorBuilder()
                .WithValidation()
                .WithCache(cacheSeconds)
                .WithLogging()
                .WithPerformanceMonitoring()
                .Build();
        }

        /// <summary>
        /// 创建自定义执行器（使用 ExecutorBuilder）
        /// </summary>
        /// <param name="configure">配置委托</param>
        /// <returns>自定义配置的执行器</returns>
        public static INodeExecutor CreateExecutor(Action<ExecutorBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            
            var builder = new ExecutorBuilder();
            configure(builder);
            return builder.Build();
        }

        /// <summary>
        /// 创建自定义执行器（旧版API，保留以兼容）
        /// </summary>
        /// <param name="configure">配置委托</param>
        /// <returns>自定义配置的执行器</returns>
        [Obsolete("请使用 CreateExecutor(Action<ExecutorBuilder> configure) 代替")]
        public static INodeExecutor CreateCustomExecutor(Action<INodeExecutor> configure)
        {
            var executor = new DefaultNodeExecutor();
            configure(executor);
            return executor;
        }

        /// <summary>
        /// 创建工作流节点执行器
        /// 专门用于执行 WorkFlowNode 类型的节点
        /// </summary>
        /// <param name="workFlowEngine">工作流引擎实例，如果为null则使用默认引擎</param>
        /// <returns>工作流节点执行器实例</returns>
        public static INodeExecutor CreateWorkFlowNodeExecutor(IWorkFlowEngine workFlowEngine = null)
        {
            return new WorkFlowNodeExecutor(workFlowEngine);
        }

        /// <summary>
        /// 创建带中间件的工作流节点执行器
        /// </summary>
        /// <param name="workFlowEngine">工作流引擎实例，如果为null则使用默认引擎</param>
        /// <param name="configure">配置委托，用于添加中间件和拦截器</param>
        /// <returns>配置好的工作流节点执行器</returns>
        public static INodeExecutor CreateWorkFlowNodeExecutor(
            IWorkFlowEngine workFlowEngine,
            Action<INodeExecutor> configure)
        {
            var executor = new WorkFlowNodeExecutor(workFlowEngine);
            configure(executor);
            return executor;
        }
    }
}


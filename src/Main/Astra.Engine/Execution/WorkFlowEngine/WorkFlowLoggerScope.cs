using Astra.Core.Logs.Extensions;
using Astra.Core.Nodes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 工作流日志作用域
    /// 管理工作流执行期间的日志生命周期，自动处理日志器的创建和关闭
    /// 符合单一职责原则：专门负责日志资源管理
    /// </summary>
    public class WorkFlowLoggerScope : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly bool _shouldDispose;

        /// <summary>
        /// 日志记录器实例
        /// </summary>
        public ILogger Logger => _logger;

        /// <summary>
        /// 创建工作流日志作用域
        /// </summary>
        /// <param name="context">节点执行上下文</param>
        /// <param name="workflow">工作流节点</param>
        /// <returns>日志作用域实例</returns>
        public static WorkFlowLoggerScope Create(NodeContext context, WorkFlowNode workflow)
        {
            ILogger logger = null;
            
            try
            {
                // 尝试从上下文获取 ILoggerFactory
                var sp = context?.ServiceProvider;
                if (sp != null)
                {
                    var loggerFactory = sp.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                    if (loggerFactory != null)
                    {
                        // 使用工作流名称创建日志器
                        var loggerName = !string.IsNullOrEmpty(workflow.Name) 
                            ? $"Workflow.{workflow.Name}" 
                            : $"Workflow.{workflow.Id}";
                        logger = loggerFactory.CreateLogger(loggerName);
                        return new WorkFlowLoggerScope(logger, shouldDispose: false);
                    }
                }
            }
            catch
            {
                // 解析失败，使用空日志器
            }

            // 如果无法创建日志器，使用空日志器
            logger = logger ?? NullLogger.Instance;
            return new WorkFlowLoggerScope(logger, shouldDispose: false);
        }

        /// <summary>
        /// 构造函数（私有）
        /// </summary>
        private WorkFlowLoggerScope(ILogger logger, bool shouldDispose)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _shouldDispose = shouldDispose;
        }

        /// <summary>
        /// 异步释放资源
        /// Microsoft.Extensions.Logging.ILogger 不需要手动关闭
        /// </summary>
        public ValueTask DisposeAsync()
        {
            // Microsoft.Extensions.Logging.ILogger 由 ILoggerFactory 管理生命周期，不需要手动关闭
            return ValueTask.CompletedTask;
        }
    }
}

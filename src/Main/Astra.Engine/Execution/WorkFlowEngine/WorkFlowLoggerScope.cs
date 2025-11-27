using Astra.Core.Logs;
using Astra.Core.Nodes.Models;
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
            try
            {
                // 尝试从上下文获取现有日志器
                var existing = context?.ServiceProvider?.GetService(typeof(Logger)) as Logger;
                if (existing != null)
                {
                    return new WorkFlowLoggerScope(existing, shouldDispose: false);
                }
            }
            catch
            {
                // 解析失败，创建新日志器
            }

            // 创建新日志器
            var newLogger = Logger.CreateForWorkflow(workflow.Id, workflow.Name);
            return new WorkFlowLoggerScope(newLogger, shouldDispose: true);
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
        /// 如果日志器是本作用域创建的，则关闭它
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_shouldDispose && _logger != null)
            {
                await _logger.ShutdownAsync();
            }
        }
    }
}

using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Management
{
    /// <summary>
    /// 工作流管理器接口
    /// 负责管理工作流的注册、存储、检索和执行
    /// </summary>
    public interface IWorkFlowManager : IDisposable
    {
        #region 工作流注册

        /// <summary>
        /// 注册工作流
        /// </summary>
        /// <param name="workflow">要注册的工作流</param>
        /// <returns>操作结果</returns>
        OperationResult RegisterWorkFlow(WorkFlowNode workflow);

        /// <summary>
        /// 注册工作流（使用自定义键）
        /// </summary>
        /// <param name="key">工作流键（用于标识工作流，如果为null则使用工作流的Id）</param>
        /// <param name="workflow">要注册的工作流</param>
        /// <returns>操作结果</returns>
        OperationResult RegisterWorkFlow(string key, WorkFlowNode workflow);

        /// <summary>
        /// 批量注册工作流
        /// </summary>
        /// <param name="workflows">要注册的工作流集合</param>
        /// <returns>操作结果，包含成功注册的数量</returns>
        OperationResult<int> RegisterWorkFlows(IEnumerable<WorkFlowNode> workflows);

        /// <summary>
        /// 注销工作流
        /// </summary>
        /// <param name="key">工作流键或ID</param>
        /// <returns>操作结果</returns>
        OperationResult UnregisterWorkFlow(string key);

        /// <summary>
        /// 注销所有工作流
        /// </summary>
        /// <returns>操作结果</returns>
        OperationResult UnregisterAllWorkFlows();

        #endregion

        #region 工作流查询

        /// <summary>
        /// 获取工作流
        /// </summary>
        /// <param name="key">工作流键或ID</param>
        /// <returns>工作流对象，如果不存在则返回null</returns>
        OperationResult<WorkFlowNode> GetWorkFlow(string key);

        /// <summary>
        /// 获取所有工作流
        /// </summary>
        /// <returns>所有已注册的工作流列表</returns>
        OperationResult<List<WorkFlowNode>> GetAllWorkFlows();

        /// <summary>
        /// 按名称查找工作流
        /// </summary>
        /// <param name="name">工作流名称</param>
        /// <returns>匹配的工作流列表</returns>
        OperationResult<List<WorkFlowNode>> FindWorkFlowsByName(string name);

        /// <summary>
        /// 检查工作流是否存在
        /// </summary>
        /// <param name="key">工作流键或ID</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        bool WorkFlowExists(string key);

        /// <summary>
        /// 获取工作流数量
        /// </summary>
        /// <returns>已注册的工作流数量</returns>
        int GetWorkFlowCount();

        #endregion

        #region 工作流执行

        /// <summary>
        /// 执行工作流
        /// </summary>
        /// <param name="key">工作流键或ID</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        Task<OperationResult<ExecutionResult>> ExecuteWorkFlowAsync(
            string key,
            NodeContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行工作流（使用自定义引擎）
        /// </summary>
        /// <param name="key">工作流键或ID</param>
        /// <param name="engine">工作流引擎实例</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        Task<OperationResult<ExecutionResult>> ExecuteWorkFlowAsync(
            string key,
            IWorkFlowEngine engine,
            NodeContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 取消正在执行的工作流
        /// </summary>
        /// <param name="executionId">执行ID</param>
        /// <returns>操作结果</returns>
        OperationResult CancelWorkFlowExecution(string executionId);

        /// <summary>
        /// 获取正在执行的工作流列表
        /// </summary>
        /// <returns>正在执行的工作流信息列表</returns>
        OperationResult<List<WorkFlowExecutionInfo>> GetRunningWorkFlows();

        #endregion

        #region 工作流监控和统计

        /// <summary>
        /// 获取工作流执行历史
        /// </summary>
        /// <param name="key">工作流键或ID，如果为null则返回所有工作流的历史</param>
        /// <param name="limit">返回的最大记录数</param>
        /// <returns>执行历史记录列表</returns>
        OperationResult<List<WorkFlowExecutionInfo>> GetExecutionHistory(string key = null, int limit = 100);

        /// <summary>
        /// 获取工作流执行统计信息
        /// </summary>
        /// <param name="key">工作流键或ID，如果为null则返回所有工作流的统计</param>
        /// <returns>执行统计信息</returns>
        OperationResult<WorkFlowExecutionStatistics> GetExecutionStatistics(string key = null);

        /// <summary>
        /// 清除执行历史
        /// </summary>
        /// <param name="key">工作流键或ID，如果为null则清除所有工作流的历史</param>
        /// <returns>操作结果</returns>
        OperationResult ClearExecutionHistory(string key = null);

        #endregion

        #region 事件

        /// <summary>
        /// 工作流注册事件
        /// </summary>
        event EventHandler<WorkFlowRegisteredEventArgs> WorkFlowRegistered;

        /// <summary>
        /// 工作流注销事件
        /// </summary>
        event EventHandler<WorkFlowUnregisteredEventArgs> WorkFlowUnregistered;

        /// <summary>
        /// 工作流执行开始事件
        /// </summary>
        event EventHandler<WorkFlowExecutionStartedEventArgs> WorkFlowExecutionStarted;

        /// <summary>
        /// 工作流执行完成事件
        /// </summary>
        event EventHandler<WorkFlowExecutionCompletedEventArgs> WorkFlowExecutionCompleted;

        #endregion
    }

    /// <summary>
    /// 工作流执行信息
    /// </summary>
    public class WorkFlowExecutionInfo
    {
        /// <summary>
        /// 执行ID（唯一标识一次执行）
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 工作流键或ID
        /// </summary>
        public string WorkFlowKey { get; set; }

        /// <summary>
        /// 工作流名称
        /// </summary>
        public string WorkFlowName { get; set; }

        /// <summary>
        /// 执行开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 执行结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 执行状态
        /// </summary>
        public WorkFlowExecutionStatus Status { get; set; }

        /// <summary>
        /// 执行结果
        /// </summary>
        public ExecutionResult Result { get; set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    }

    /// <summary>
    /// 工作流执行状态
    /// </summary>
    public enum WorkFlowExecutionStatus
    {
        /// <summary>
        /// 等待执行
        /// </summary>
        Pending,

        /// <summary>
        /// 正在执行
        /// </summary>
        Running,

        /// <summary>
        /// 执行成功
        /// </summary>
        Completed,

        /// <summary>
        /// 执行失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// 工作流注册事件参数
    /// </summary>
    public class WorkFlowRegisteredEventArgs : EventArgs
    {
        /// <summary>
        /// 工作流键
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 工作流对象
        /// </summary>
        public WorkFlowNode WorkFlow { get; set; }
    }

    /// <summary>
    /// 工作流注销事件参数
    /// </summary>
    public class WorkFlowUnregisteredEventArgs : EventArgs
    {
        /// <summary>
        /// 工作流键
        /// </summary>
        public string Key { get; set; }
    }

    /// <summary>
    /// 工作流执行开始事件参数
    /// </summary>
    public class WorkFlowExecutionStartedEventArgs : EventArgs
    {
        /// <summary>
        /// 执行ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 工作流键
        /// </summary>
        public string WorkFlowKey { get; set; }

        /// <summary>
        /// 工作流对象
        /// </summary>
        public WorkFlowNode WorkFlow { get; set; }
    }

    /// <summary>
        /// 工作流执行完成事件参数
        /// </summary>
    public class WorkFlowExecutionCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 执行ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 工作流键
        /// </summary>
        public string WorkFlowKey { get; set; }

        /// <summary>
        /// 执行结果
        /// </summary>
        public ExecutionResult Result { get; set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}


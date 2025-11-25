using Astra.Core.Devices;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.WorkFlowEngine.Management
{
    /// <summary>
    /// 工作流管理器实现
    /// 负责管理工作流的注册、存储、检索和执行
    /// 支持并发执行多个工作流，提供执行历史和统计功能
    /// </summary>
    public class WorkFlowManager : IWorkFlowManager
    {
        private readonly ConcurrentDictionary<string, WorkFlowNode> _workflows;
        private readonly ConcurrentDictionary<string, WorkFlowExecutionInfo> _runningExecutions;
        private readonly ConcurrentQueue<WorkFlowExecutionInfo> _executionHistory;
        private readonly ConcurrentDictionary<string, List<WorkFlowExecutionInfo>> _workflowHistory;
        private readonly ConcurrentDictionary<string, WorkFlowExecutionStatistics> _workflowStatistics;
        private readonly IWorkFlowEngine _defaultEngine;
        private readonly int _maxHistorySize;

        /// <summary>
        /// 工作流注册事件
        /// </summary>
        public event EventHandler<WorkFlowRegisteredEventArgs> WorkFlowRegistered;

        /// <summary>
        /// 工作流注销事件
        /// </summary>
        public event EventHandler<WorkFlowUnregisteredEventArgs> WorkFlowUnregistered;

        /// <summary>
        /// 工作流执行开始事件
        /// </summary>
        public event EventHandler<WorkFlowExecutionStartedEventArgs> WorkFlowExecutionStarted;

        /// <summary>
        /// 工作流执行完成事件
        /// </summary>
        public event EventHandler<WorkFlowExecutionCompletedEventArgs> WorkFlowExecutionCompleted;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="defaultEngine">默认工作流引擎，如果为null则使用默认引擎</param>
        /// <param name="maxHistorySize">最大历史记录数，默认1000</param>
        public WorkFlowManager(IWorkFlowEngine defaultEngine = null, int maxHistorySize = 1000)
        {
            _workflows = new ConcurrentDictionary<string, WorkFlowNode>();
            _runningExecutions = new ConcurrentDictionary<string, WorkFlowExecutionInfo>();
            _executionHistory = new ConcurrentQueue<WorkFlowExecutionInfo>();
            _workflowHistory = new ConcurrentDictionary<string, List<WorkFlowExecutionInfo>>();
            _workflowStatistics = new ConcurrentDictionary<string, WorkFlowExecutionStatistics>();
            _defaultEngine = defaultEngine ?? WorkFlowEngineFactory.CreateDefault();
            _maxHistorySize = maxHistorySize;
        }

        #region 工作流注册

        /// <summary>
        /// 注册工作流
        /// </summary>
        public OperationResult RegisterWorkFlow(WorkFlowNode workflow)
        {
            if (workflow == null)
                return OperationResult.Failure("工作流不能为空", ErrorCodes.InvalidData);

            if (string.IsNullOrWhiteSpace(workflow.Id))
                return OperationResult.Failure("工作流ID不能为空", ErrorCodes.InvalidData);

            return RegisterWorkFlow(workflow.Id, workflow);
        }

        /// <summary>
        /// 注册工作流（使用自定义键）
        /// </summary>
        public OperationResult RegisterWorkFlow(string key, WorkFlowNode workflow)
        {
            if (string.IsNullOrWhiteSpace(key))
                return OperationResult.Failure("工作流键不能为空", ErrorCodes.InvalidData);

            if (workflow == null)
                return OperationResult.Failure("工作流不能为空", ErrorCodes.InvalidData);

            if (_workflows.ContainsKey(key))
                return OperationResult.Failure($"工作流 '{key}' 已存在", ErrorCodes.InvalidData);

            if (_workflows.TryAdd(key, workflow))
            {
                // 初始化统计信息
                _workflowStatistics[key] = new WorkFlowExecutionStatistics();
                _workflowHistory[key] = new List<WorkFlowExecutionInfo>();

                // 触发注册事件
                OnWorkFlowRegistered(new WorkFlowRegisteredEventArgs
                {
                    Key = key,
                    WorkFlow = workflow
                });

                return OperationResult.Succeed($"工作流 '{key}' 注册成功");
            }

            return OperationResult.Failure($"工作流 '{key}' 注册失败", ErrorCodes.InvalidData);
        }

        /// <summary>
        /// 批量注册工作流
        /// </summary>
        public OperationResult<int> RegisterWorkFlows(IEnumerable<WorkFlowNode> workflows)
        {
            if (workflows == null)
                return OperationResult<int>.Failure("工作流列表不能为空", ErrorCodes.InvalidData);

            int successCount = 0;
            var errors = new List<string>();

            foreach (var workflow in workflows)
            {
                var result = RegisterWorkFlow(workflow);
                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"{workflow?.Id ?? "未知"}: {result.Message}");
                }
            }

            if (errors.Any())
            {
                return OperationResult<int>.PartialSuccess(
                    $"成功注册 {successCount} 个工作流，{errors.Count} 个失败",
                    successCount,
                    string.Join("; ", errors));
            }

            return OperationResult<int>.Succeed(successCount, $"成功注册 {successCount} 个工作流");
        }

        /// <summary>
        /// 注销工作流
        /// </summary>
        public OperationResult UnregisterWorkFlow(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return OperationResult.Failure("工作流键不能为空", ErrorCodes.InvalidData);

            if (_workflows.TryRemove(key, out var workflow))
            {
                // 如果工作流正在执行，取消执行
                if (_runningExecutions.ContainsKey(key))
                {
                    // 注意：这里只能标记为取消，实际取消需要CancellationToken
                    // 实际应用中可以通过executionId来取消
                }

                // 清理相关数据
                _workflowStatistics.TryRemove(key, out _);
                _workflowHistory.TryRemove(key, out _);

                // 触发注销事件
                OnWorkFlowUnregistered(new WorkFlowUnregisteredEventArgs
                {
                    Key = key
                });

                return OperationResult.Succeed($"工作流 '{key}' 注销成功");
            }

            return OperationResult.Failure($"工作流 '{key}' 不存在", ErrorCodes.DeviceNotFound);
        }

        /// <summary>
        /// 注销所有工作流
        /// </summary>
        public OperationResult UnregisterAllWorkFlows()
        {
            var keys = _workflows.Keys.ToList();
            int successCount = 0;

            foreach (var key in keys)
            {
                var result = UnregisterWorkFlow(key);

                if (result.Success)
                {
                    successCount++;
                }
            }

            return OperationResult.Succeed($"成功注销 {successCount} 个工作流");
        }

        #endregion

        #region 工作流查询

        /// <summary>
        /// 获取工作流
        /// </summary>
        public OperationResult<WorkFlowNode> GetWorkFlow(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return OperationResult<WorkFlowNode>.Failure("工作流键不能为空", ErrorCodes.InvalidData);

            if (_workflows.TryGetValue(key, out var workflow))
            {
                return OperationResult<WorkFlowNode>.Succeed(workflow);
            }

            // 尝试通过ID查找
            var found = _workflows.Values.FirstOrDefault(w => w.Id == key);
            if (found != null)
            {
                return OperationResult<WorkFlowNode>.Succeed(found);
            }

            return OperationResult<WorkFlowNode>.Failure($"工作流 '{key}' 不存在", ErrorCodes.NotFound);
        }

        /// <summary>
        /// 获取所有工作流
        /// </summary>
        public OperationResult<List<WorkFlowNode>> GetAllWorkFlows()
        {
            return OperationResult<List<WorkFlowNode>>.Succeed(_workflows.Values.ToList());
        }

        /// <summary>
        /// 按名称查找工作流
        /// </summary>
        public OperationResult<List<WorkFlowNode>> FindWorkFlowsByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return OperationResult<List<WorkFlowNode>>.Failure("工作流名称不能为空", ErrorCodes.InvalidData);

            var workflows = _workflows.Values
                .Where(w => w.Name != null && w.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return OperationResult<List<WorkFlowNode>>.Succeed(workflows);
        }

        /// <summary>
        /// 检查工作流是否存在
        /// </summary>
        public bool WorkFlowExists(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _workflows.ContainsKey(key) || _workflows.Values.Any(w => w.Id == key);
        }

        /// <summary>
        /// 获取工作流数量
        /// </summary>
        public int GetWorkFlowCount()
        {
            return _workflows.Count;
        }

        #endregion

        #region 工作流执行

        /// <summary>
        /// 执行工作流
        /// </summary>
        public async Task<OperationResult<ExecutionResult>> ExecuteWorkFlowAsync(
            string key,
            NodeContext context,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWorkFlowAsync(key, _defaultEngine, context, cancellationToken);
        }

        /// <summary>
        /// 执行工作流（使用自定义引擎）
        /// </summary>
        public async Task<OperationResult<ExecutionResult>> ExecuteWorkFlowAsync(
            string key,
            IWorkFlowEngine engine,
            NodeContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return OperationResult<ExecutionResult>.Failure("工作流键不能为空", ErrorCodes.InvalidData);

            if (engine == null)
                return OperationResult<ExecutionResult>.Failure("工作流引擎不能为空", ErrorCodes.InvalidData);

            // 获取工作流
            var workflowResult = GetWorkFlow(key);
            if (!workflowResult.Success)
            {
                return OperationResult<ExecutionResult>.Failure(workflowResult.Message, workflowResult.ErrorCode);
            }

            var workflow = workflowResult.Data;
            var executionId = Guid.NewGuid().ToString();

            // 创建执行信息
            var executionInfo = new WorkFlowExecutionInfo
            {
                ExecutionId = executionId,
                WorkFlowKey = key,
                WorkFlowName = workflow.Name,
                StartTime = DateTime.Now,
                Status = WorkFlowExecutionStatus.Running
            };

            // 记录正在执行的执行
            _runningExecutions[executionId] = executionInfo;

            // 触发执行开始事件
            OnWorkFlowExecutionStarted(new WorkFlowExecutionStartedEventArgs
            {
                ExecutionId = executionId,
                WorkFlowKey = key,
                WorkFlow = workflow
            });

            try
            {
                // 执行工作流
                var result = await engine.ExecuteAsync(workflow, context, cancellationToken);

                // 更新执行信息
                executionInfo.EndTime = DateTime.Now;
                executionInfo.Status = result.Success ? WorkFlowExecutionStatus.Completed : WorkFlowExecutionStatus.Failed;
                executionInfo.Result = result;

                // 更新统计信息
                UpdateStatistics(key, result);

                // 触发执行完成事件
                OnWorkFlowExecutionCompleted(new WorkFlowExecutionCompletedEventArgs
                {
                    ExecutionId = executionId,
                    WorkFlowKey = key,
                    Result = result,
                    Duration = executionInfo.Duration ?? TimeSpan.Zero
                });

                return OperationResult<ExecutionResult>.Succeed(result);
            }
            catch (OperationCanceledException)
            {
                executionInfo.EndTime = DateTime.Now;
                executionInfo.Status = WorkFlowExecutionStatus.Cancelled;
                executionInfo.Result = ExecutionResult.Cancel("工作流执行已取消");

                OnWorkFlowExecutionCompleted(new WorkFlowExecutionCompletedEventArgs
                {
                    ExecutionId = executionId,
                    WorkFlowKey = key,
                    Result = executionInfo.Result,
                    Duration = executionInfo.Duration ?? TimeSpan.Zero
                });

                return OperationResult<ExecutionResult>.Succeed(executionInfo.Result);
            }
            catch (Exception ex)
            {
                executionInfo.EndTime = DateTime.Now;
                executionInfo.Status = WorkFlowExecutionStatus.Failed;
                executionInfo.Result = ExecutionResult.Failed($"工作流执行异常: {ex.Message}", ex);

                OnWorkFlowExecutionCompleted(new WorkFlowExecutionCompletedEventArgs
                {
                    ExecutionId = executionId,
                    WorkFlowKey = key,
                    Result = executionInfo.Result,
                    Duration = executionInfo.Duration ?? TimeSpan.Zero
                });

                return OperationResult<ExecutionResult>.Failure($"工作流执行失败: {ex.Message}", ErrorCodes.ExecutionFailed);
            }
            finally
            {
                // 从正在执行的列表中移除
                _runningExecutions.TryRemove(executionId, out _);

                // 添加到历史记录
                AddToHistory(executionInfo);
            }
        }

        /// <summary>
        /// 取消正在执行的工作流
        /// </summary>
        public OperationResult CancelWorkFlowExecution(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return OperationResult.Failure("执行ID不能为空", ErrorCodes.InvalidData);

            if (_runningExecutions.TryGetValue(executionId, out var executionInfo))
            {
                // 注意：实际取消需要CancellationToken，这里只能标记
                // 实际应用中应该维护CancellationTokenSource
                executionInfo.Status = WorkFlowExecutionStatus.Cancelled;
                return OperationResult.Succeed($"执行 '{executionId}' 已标记为取消");
            }

            return OperationResult.Failure($"执行 '{executionId}' 不存在或已完成", ErrorCodes.NotFound);
        }

        /// <summary>
        /// 获取正在执行的工作流列表
        /// </summary>
        public OperationResult<List<WorkFlowExecutionInfo>> GetRunningWorkFlows()
        {
            var running = _runningExecutions.Values
                .Where(e => e.Status == WorkFlowExecutionStatus.Running)
                .ToList();

            return OperationResult<List<WorkFlowExecutionInfo>>.Succeed(running);
        }

        #endregion

        #region 工作流监控和统计

        /// <summary>
        /// 获取工作流执行历史
        /// </summary>
        public OperationResult<List<WorkFlowExecutionInfo>> GetExecutionHistory(string key = null, int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                // 返回所有工作流的历史
                var allHistory = _executionHistory
                    .OrderByDescending(e => e.StartTime)
                    .Take(limit)
                    .ToList();

                return OperationResult<List<WorkFlowExecutionInfo>>.Succeed(allHistory);
            }
            else
            {
                // 返回指定工作流的历史
                if (_workflowHistory.TryGetValue(key, out var history))
                {
                    var limitedHistory = history
                        .OrderByDescending(e => e.StartTime)
                        .Take(limit)
                        .ToList();

                    return OperationResult<List<WorkFlowExecutionInfo>>.Succeed(limitedHistory);
                }

                return OperationResult<List<WorkFlowExecutionInfo>>.Succeed(new List<WorkFlowExecutionInfo>());
            }
        }

        /// <summary>
        /// 获取工作流执行统计信息
        /// </summary>
        public OperationResult<WorkFlowExecutionStatistics> GetExecutionStatistics(string key = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                // 返回所有工作流的汇总统计
                var allStats = new WorkFlowExecutionStatistics();
                foreach (var stats in _workflowStatistics.Values)
                {
                    allStats.TotalNodes += stats.TotalNodes;
                    allStats.CompletedNodes += stats.CompletedNodes;
                    allStats.FailedNodes += stats.FailedNodes;
                    allStats.SkippedNodes += stats.SkippedNodes;
                }

                return OperationResult<WorkFlowExecutionStatistics>.Succeed(allStats);
            }
            else
            {
                if (_workflowStatistics.TryGetValue(key, out var stats))
                {
                    return OperationResult<WorkFlowExecutionStatistics>.Succeed(stats);
                }

                return OperationResult<WorkFlowExecutionStatistics>.Failure($"工作流 '{key}' 不存在", ErrorCodes.NotFound);
            }
        }

        /// <summary>
        /// 清除执行历史
        /// </summary>
        public OperationResult ClearExecutionHistory(string key = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                // 清除所有历史
                while (_executionHistory.TryDequeue(out _)) { }
                _workflowHistory.Clear();

                return OperationResult.Succeed("所有工作流执行历史已清除");
            }
            else
            {
                // 清除指定工作流的历史
                if (_workflowHistory.TryRemove(key, out _))
                {
                    return OperationResult.Succeed($"工作流 '{key}' 的执行历史已清除");
                }

                return OperationResult.Failure($"工作流 '{key}' 不存在", ErrorCodes.DeviceNotFound);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics(string key, ExecutionResult result)
        {
            if (!_workflowStatistics.TryGetValue(key, out var stats))
            {
                stats = new WorkFlowExecutionStatistics();
                _workflowStatistics[key] = stats;
            }

            // 注意：这里只更新基本统计，详细统计应由引擎提供
            if (result.Success)
            {
                stats.CompletedNodes++;
            }
            else
            {
                stats.FailedNodes++;
                stats.LastError = result.Message;
            }
            
            stats.TotalNodes = stats.CompletedNodes + stats.FailedNodes + stats.SkippedNodes;
        }

        /// <summary>
        /// 添加到历史记录
        /// </summary>
        private void AddToHistory(WorkFlowExecutionInfo executionInfo)
        {
            // 添加到全局历史
            _executionHistory.Enqueue(executionInfo);

            // 限制历史记录大小
            while (_executionHistory.Count > _maxHistorySize)
            {
                _executionHistory.TryDequeue(out _);
            }

            // 添加到工作流专用历史
            if (_workflowHistory.TryGetValue(executionInfo.WorkFlowKey, out var history))
            {
                history.Add(executionInfo);

                // 限制每个工作流的历史记录大小
                if (history.Count > _maxHistorySize)
                {
                    history.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 触发工作流注册事件
        /// </summary>
        protected virtual void OnWorkFlowRegistered(WorkFlowRegisteredEventArgs e)
        {
            WorkFlowRegistered?.Invoke(this, e);
        }

        /// <summary>
        /// 触发工作流注销事件
        /// </summary>
        protected virtual void OnWorkFlowUnregistered(WorkFlowUnregisteredEventArgs e)
        {
            WorkFlowUnregistered?.Invoke(this, e);
        }

        /// <summary>
        /// 触发工作流执行开始事件
        /// </summary>
        protected virtual void OnWorkFlowExecutionStarted(WorkFlowExecutionStartedEventArgs e)
        {
            WorkFlowExecutionStarted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发工作流执行完成事件
        /// </summary>
        protected virtual void OnWorkFlowExecutionCompleted(WorkFlowExecutionCompletedEventArgs e)
        {
            WorkFlowExecutionCompleted?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 取消所有正在执行的作业
            foreach (var executionId in _runningExecutions.Keys)
            {
                CancelWorkFlowExecution(executionId);
            }

            _workflows.Clear();
            _runningExecutions.Clear();
            while (_executionHistory.TryDequeue(out _)) { }
            _workflowHistory.Clear();
            _workflowStatistics.Clear();
        }

        #endregion
    }
}


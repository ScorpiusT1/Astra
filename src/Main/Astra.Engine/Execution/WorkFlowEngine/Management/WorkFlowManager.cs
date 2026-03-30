using Astra.Core.Archiving;
using Astra.Core.Constants;
using Astra.Core.Data;
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
        private static readonly TimeSpan DefaultRawDataTtl = TimeSpan.FromMinutes(AstraSharedConstants.EngineDefaults.RawDataTtlMinutes);
        private const int DefaultRawDataMaxItems = AstraSharedConstants.EngineDefaults.RawDataMaxItems;
        private const long DefaultRawDataMaxBytes = AstraSharedConstants.EngineDefaults.RawDataMaxBytes;

        private readonly ConcurrentDictionary<string, WorkFlowNode> _workflows;
        private readonly ConcurrentDictionary<string, WorkFlowExecutionInfo> _runningExecutions;
        private readonly ConcurrentDictionary<string, WorkFlowExecutionController> _executionControllers;
        private readonly ConcurrentQueue<WorkFlowExecutionInfo> _executionHistory;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<WorkFlowExecutionInfo>> _workflowHistory;
        private readonly ConcurrentDictionary<string, WorkFlowExecutionStatistics> _workflowStatistics;
        private readonly ConcurrentDictionary<string, WorkFlowRunRecord> _runRecords;
        private readonly ConcurrentDictionary<IWorkFlowEngine, byte> _wiredEngines;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _workflowExecutionLocks;
        private readonly IWorkFlowEngine _defaultEngine;
        private readonly INodeRunCollector _nodeRunCollector;
        private readonly int _maxHistorySize;
        private readonly IWorkflowArchiveService _workflowArchiveService;

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
        public WorkFlowManager(
            IWorkFlowEngine defaultEngine = null,
            int maxHistorySize = 1000,
            INodeRunCollector nodeRunCollector = null,
            IWorkflowArchiveService workflowArchiveService = null)
        {
            _workflows = new ConcurrentDictionary<string, WorkFlowNode>();
            _runningExecutions = new ConcurrentDictionary<string, WorkFlowExecutionInfo>();
            _executionControllers = new ConcurrentDictionary<string, WorkFlowExecutionController>();
            _executionHistory = new ConcurrentQueue<WorkFlowExecutionInfo>();
            _workflowHistory = new ConcurrentDictionary<string, ConcurrentQueue<WorkFlowExecutionInfo>>();
            _workflowStatistics = new ConcurrentDictionary<string, WorkFlowExecutionStatistics>();
            _runRecords = new ConcurrentDictionary<string, WorkFlowRunRecord>();
            _wiredEngines = new ConcurrentDictionary<IWorkFlowEngine, byte>();
            _workflowExecutionLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _defaultEngine = defaultEngine ?? WorkFlowEngineFactory.CreateDefault();
            _nodeRunCollector = nodeRunCollector ?? new InMemoryNodeRunCollector();
            _maxHistorySize = maxHistorySize;
            _workflowArchiveService = workflowArchiveService;
            EnsureEngineWired(_defaultEngine);
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
                _workflowHistory[key] = new ConcurrentQueue<WorkFlowExecutionInfo>();

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
                _workflowExecutionLocks.TryRemove(key, out _);

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

            EnsureEngineWired(engine);

            // 获取工作流
            var workflowResult = GetWorkFlow(key);
            if (!workflowResult.Success)
            {
                return OperationResult<ExecutionResult>.Failure(workflowResult.Message, workflowResult.ErrorCode);
            }

            var workflow = workflowResult.Data;
            var canonicalKey = ResolveCanonicalWorkflowKey(key, workflow);
            var workflowLock = _workflowExecutionLocks.GetOrAdd(canonicalKey, _ => new SemaphoreSlim(1, 1));
            await workflowLock.WaitAsync(cancellationToken).ConfigureAwait(false);
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

            try
            {
                // 记录正在执行的执行
                _runningExecutions[executionId] = executionInfo;
                var controller = new WorkFlowExecutionController();
                _executionControllers[executionId] = controller;
                var rawDataStore = new InMemoryRawDataStore(DefaultRawDataTtl, DefaultRawDataMaxItems, DefaultRawDataMaxBytes);
                context ??= new NodeContext();
                context.ExecutionId = executionId;
                context.SetMetadata(EngineConstants.MetadataKeys.WorkflowExecutionController, controller);
                context.SetMetadata(EngineConstants.MetadataKeys.ExecutionId, executionId);
                context.SetMetadata(EngineConstants.MetadataKeys.WorkFlowKey, key);
                context.SetRawDataStore(rawDataStore);
                var dataBus = new TestDataBus(executionId, rawDataStore);
                context.SetDataBus(dataBus);

                // 触发执行开始事件
                OnWorkFlowExecutionStarted(new WorkFlowExecutionStartedEventArgs
                {
                    ExecutionId = executionId,
                    WorkFlowKey = key,
                    WorkFlow = workflow
                });

                OperationResult<ExecutionResult>? completionResult = null;
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, controller.Token);

                    // 执行工作流
                    var result = await engine.ExecuteAsync(workflow, context, linkedCts.Token);

                    // 更新执行信息
                    executionInfo.EndTime = DateTime.Now;
                    executionInfo.Status = result.Success ? WorkFlowExecutionStatus.Completed : WorkFlowExecutionStatus.Failed;
                    executionInfo.Result = result;
                    BuildAndStoreRunRecord(executionInfo, result);

                    // 触发执行完成事件
                    OnWorkFlowExecutionCompleted(new WorkFlowExecutionCompletedEventArgs
                    {
                        ExecutionId = executionId,
                        WorkFlowKey = key,
                        Result = result,
                        Duration = executionInfo.Duration ?? TimeSpan.Zero
                    });

                    completionResult = OperationResult<ExecutionResult>.Succeed(result);
                }
                catch (OperationCanceledException)
                {
                    executionInfo.EndTime = DateTime.Now;
                    executionInfo.Status = WorkFlowExecutionStatus.Cancelled;
                    executionInfo.Result = ExecutionResult.Cancel("工作流执行已取消");
                    BuildAndStoreRunRecord(executionInfo, executionInfo.Result);

                    OnWorkFlowExecutionCompleted(new WorkFlowExecutionCompletedEventArgs
                    {
                        ExecutionId = executionId,
                        WorkFlowKey = key,
                        Result = executionInfo.Result,
                        Duration = executionInfo.Duration ?? TimeSpan.Zero
                    });

                    completionResult = OperationResult<ExecutionResult>.Succeed(executionInfo.Result);
                }
                catch (Exception ex)
                {
                    executionInfo.EndTime = DateTime.Now;
                    executionInfo.Status = WorkFlowExecutionStatus.Failed;
                    executionInfo.Result = ExecutionResult.Failed($"工作流执行异常: {ex.Message}", ex);
                    BuildAndStoreRunRecord(executionInfo, executionInfo.Result);

                    OnWorkFlowExecutionCompleted(new WorkFlowExecutionCompletedEventArgs
                    {
                        ExecutionId = executionId,
                        WorkFlowKey = key,
                        Result = executionInfo.Result,
                        Duration = executionInfo.Duration ?? TimeSpan.Zero
                    });

                    completionResult = OperationResult<ExecutionResult>.Failure($"工作流执行失败: {ex.Message}", ErrorCodes.ExecutionFailed);
                }

                await TryArchiveBeforeRawCleanupAsync(
                        context,
                        executionId,
                        key,
                        workflow.Name,
                        executionInfo.Status)
                    .ConfigureAwait(false);

                if (context.GetRawDataStore() is IRawDataStore store)
                {
                    // 建议业务节点使用 executionId:xxx 作为 key 前缀，便于执行结束后快速回收
                    store.RemoveByPrefix($"{executionId}:");
                }

                _runningExecutions.TryRemove(executionId, out _);
                if (_executionControllers.TryRemove(executionId, out var executionController))
                {
                    executionController.Dispose();
                }

                AddToHistory(executionInfo);

                return completionResult
                       ?? OperationResult<ExecutionResult>.Failure("工作流执行未完成", ErrorCodes.ExecutionFailed);
            }
            finally
            {
                workflowLock.Release();
            }
        }

        private async Task TryArchiveBeforeRawCleanupAsync(
            NodeContext context,
            string executionId,
            string workFlowKey,
            string workFlowName,
            WorkFlowExecutionStatus status)
        {
            if (_workflowArchiveService == null || context == null)
            {
                return;
            }

            _runRecords.TryGetValue(executionId, out var runRecord);
            var request = new WorkflowArchiveRequest
            {
                Trigger = WorkflowArchiveTrigger.EngineBeforeRawCleanup,
                ExecutionId = executionId,
                WorkFlowKey = workFlowKey,
                WorkFlowName = workFlowName ?? string.Empty,
                NodeContext = context,
                ExecutionStatus = status,
                RunRecord = runRecord
            };

            try
            {
                // 归档优先完成，避免因用户 CancellationToken 已处于取消状态而跳过落盘
                await _workflowArchiveService
                    .ArchiveAsync(request, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFlowManager] 工作流归档失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消正在执行的工作流
        /// </summary>
        public OperationResult CancelWorkFlowExecution(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return OperationResult.Failure("执行ID不能为空", ErrorCodes.InvalidData);

            if (_runningExecutions.TryGetValue(executionId, out var executionInfo) &&
                _executionControllers.TryGetValue(executionId, out var controller))
            {
                executionInfo.Status = WorkFlowExecutionStatus.Cancelling;
                controller.Cancel();
                return OperationResult.Succeed($"执行 '{executionId}' 已发出取消请求");
            }

            return OperationResult.Failure($"执行 '{executionId}' 不存在或已完成", ErrorCodes.NotFound);
        }

        /// <summary>
        /// 暂停正在执行的工作流
        /// </summary>
        public OperationResult PauseWorkFlowExecution(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return OperationResult.Failure("执行ID不能为空", ErrorCodes.InvalidData);

            if (_runningExecutions.TryGetValue(executionId, out var executionInfo) &&
                _executionControllers.TryGetValue(executionId, out var controller))
            {
                controller.Pause();
                executionInfo.Status = WorkFlowExecutionStatus.Paused;
                return OperationResult.Succeed($"执行 '{executionId}' 已暂停");
            }

            return OperationResult.Failure($"执行 '{executionId}' 不存在或已完成", ErrorCodes.NotFound);
        }

        /// <summary>
        /// 恢复已暂停的工作流
        /// </summary>
        public OperationResult ResumeWorkFlowExecution(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return OperationResult.Failure("执行ID不能为空", ErrorCodes.InvalidData);

            if (_runningExecutions.TryGetValue(executionId, out var executionInfo) &&
                _executionControllers.TryGetValue(executionId, out var controller))
            {
                controller.Resume();
                executionInfo.Status = WorkFlowExecutionStatus.Running;
                return OperationResult.Succeed($"执行 '{executionId}' 已恢复");
            }

            return OperationResult.Failure($"执行 '{executionId}' 不存在或已完成", ErrorCodes.NotFound);
        }

        /// <summary>
        /// 获取正在执行的工作流列表
        /// </summary>
        public OperationResult<List<WorkFlowExecutionInfo>> GetRunningWorkFlows()
        {
            var running = _runningExecutions.Values
                .Where(e => e.Status == WorkFlowExecutionStatus.Running ||
                            e.Status == WorkFlowExecutionStatus.Paused ||
                            e.Status == WorkFlowExecutionStatus.Cancelling)
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
                        .ToArray()
                        .OrderByDescending(e => e.StartTime)
                        .Take(limit)
                        .ToList();

                    return OperationResult<List<WorkFlowExecutionInfo>>.Succeed(limitedHistory);
                }

                return OperationResult<List<WorkFlowExecutionInfo>>.Succeed(new List<WorkFlowExecutionInfo>());
            }
        }

        /// <summary>
        /// 获取指定执行ID的完整结果链
        /// </summary>
        public OperationResult<WorkFlowRunRecord> GetWorkFlowRunRecord(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return OperationResult<WorkFlowRunRecord>.Failure("执行ID不能为空", ErrorCodes.InvalidData);

            if (_runRecords.TryGetValue(executionId, out var runRecord))
            {
                return OperationResult<WorkFlowRunRecord>.Succeed(runRecord);
            }

            return OperationResult<WorkFlowRunRecord>.Failure($"执行 '{executionId}' 的结果链不存在", ErrorCodes.NotFound);
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

        private void BuildAndStoreRunRecord(WorkFlowExecutionInfo executionInfo, ExecutionResult finalResult)
        {
            var nodeRuns = _nodeRunCollector.GetByExecutionId(executionInfo.ExecutionId);
            var workflowStatus = executionInfo.Status;

            var record = new WorkFlowRunRecord
            {
                ExecutionId = executionInfo.ExecutionId,
                WorkFlowKey = executionInfo.WorkFlowKey,
                WorkFlowName = executionInfo.WorkFlowName,
                Strategy = finalResult?.GetOutput(EngineConstants.OutputKeys.ExecutionStrategy, string.Empty),
                StartTime = executionInfo.StartTime,
                EndTime = executionInfo.EndTime ?? DateTime.Now,
                Status = workflowStatus.ToString(),
                FinalResult = finalResult,
                NodeRuns = nodeRuns.ToList()
            };

            _runRecords[executionInfo.ExecutionId] = record;
            _nodeRunCollector.Clear(executionInfo.ExecutionId);
            UpdateStatisticsFromNodeRuns(executionInfo.WorkFlowKey, record.NodeRuns);
        }

        private void UpdateStatisticsFromNodeRuns(string key, List<NodeRunRecord> nodeRuns)
        {
            if (!_workflowStatistics.TryGetValue(key, out var stats))
            {
                stats = new WorkFlowExecutionStatistics();
                _workflowStatistics[key] = stats;
            }

            if (nodeRuns == null || nodeRuns.Count == 0)
            {
                return;
            }

            stats.TotalNodes += nodeRuns.Count;
            stats.CompletedNodes += nodeRuns.Count(n => n.State == NodeExecutionState.Success);
            stats.FailedNodes += nodeRuns.Count(n => n.State == NodeExecutionState.Failed);
            stats.SkippedNodes += nodeRuns.Count(n => n.State == NodeExecutionState.Skipped);
            var failedNode = nodeRuns.LastOrDefault(n => n.State == NodeExecutionState.Failed);
            if (failedNode != null)
            {
                stats.LastError = failedNode.Message;
            }
        }

        private void WireEngineEvents(IWorkFlowEngine engine)
        {
            if (engine == null) return;

            engine.NodeExecutionStarted += (_, e) =>
            {
                if (e?.Node == null || e.Context == null) return;
                var executionId = ResolveExecutionId(e.Context);
                if (string.IsNullOrWhiteSpace(executionId)) return;
                var workflowKey = ResolveWorkFlowKey(e.Context);
                _nodeRunCollector.MarkNodeStarted(executionId, workflowKey, e.Node, e.Context, DateTime.Now);
            };

            engine.NodeExecutionCompleted += (_, e) =>
            {
                if (e?.Node == null || e.Context == null || e.Result == null) return;
                var executionId = ResolveExecutionId(e.Context);
                if (string.IsNullOrWhiteSpace(executionId)) return;
                var workflowKey = ResolveWorkFlowKey(e.Context);
                _nodeRunCollector.MarkNodeCompleted(executionId, workflowKey, e.Node, e.Context, e.Result, DateTime.Now);
            };
        }

        private void EnsureEngineWired(IWorkFlowEngine engine)
        {
            if (engine == null) return;
            if (_wiredEngines.TryAdd(engine, 0))
            {
                WireEngineEvents(engine);
            }
        }

        private static string ResolveExecutionId(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue(EngineConstants.MetadataKeys.ExecutionId, out var metadataExecutionId) &&
                metadataExecutionId is string executionId)
            {
                return executionId;
            }

            return context?.ExecutionId;
        }

        private static string ResolveWorkFlowKey(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue(EngineConstants.MetadataKeys.WorkFlowKey, out var metadataWorkFlowKey) &&
                metadataWorkFlowKey is string workFlowKey)
            {
                return workFlowKey;
            }

            return string.Empty;
        }

        private string ResolveCanonicalWorkflowKey(string requestedKey, WorkFlowNode workflow)
        {
            if (!string.IsNullOrWhiteSpace(requestedKey) && _workflows.ContainsKey(requestedKey))
            {
                return requestedKey;
            }

            foreach (var kvp in _workflows)
            {
                if (ReferenceEquals(kvp.Value, workflow))
                {
                    return kvp.Key;
                }
            }

            return workflow?.Id ?? requestedKey ?? string.Empty;
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
                history.Enqueue(executionInfo);

                // 限制每个工作流的历史记录大小
                while (history.Count > _maxHistorySize)
                {
                    history.TryDequeue(out _);
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
            foreach (var controller in _executionControllers.Values)
            {
                controller.Dispose();
            }
            _executionControllers.Clear();
            while (_executionHistory.TryDequeue(out _)) { }
            _workflowHistory.Clear();
            _workflowStatistics.Clear();
            _runRecords.Clear();
            foreach (var semaphore in _workflowExecutionLocks.Values)
            {
                semaphore.Dispose();
            }
            _workflowExecutionLocks.Clear();
        }

        #endregion
    }
}


using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.UI.Services
{
    /// <summary>
    /// 执行会话服务：在 UI 与 IWorkFlowManager 之间提供一层稳定封装。
    /// </summary>
    public sealed class WorkflowExecutionSessionService : IWorkflowExecutionSessionService
    {
        private readonly IWorkFlowManager _workFlowManager;
        private readonly IWorkflowEngineProvider _workflowEngineProvider;
        private readonly object _stateLock = new object();

        private string _currentWorkflowKey;
        private string _currentExecutionId;
        private Task<OperationResult<ExecutionResult>> _executionTask;
        private readonly HashSet<string> _startedWorkflowKeys = new HashSet<string>(StringComparer.Ordinal);

        public event EventHandler<WorkflowNodeExecutionChangedEventArgs> NodeExecutionChanged;

        public WorkflowExecutionSessionService(
            IWorkFlowManager workFlowManager,
            IWorkflowEngineProvider workflowEngineProvider)
        {
            _workFlowManager = workFlowManager ?? throw new ArgumentNullException(nameof(workFlowManager));
            _workflowEngineProvider = workflowEngineProvider ?? throw new ArgumentNullException(nameof(workflowEngineProvider));
        }

        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    if (_executionTask != null && !_executionTask.IsCompleted)
                    {
                        return true;
                    }

                    var runningResult = _workFlowManager.GetRunningWorkFlows();
                    if (!runningResult.Success || runningResult.Data == null || _startedWorkflowKeys.Count == 0)
                    {
                        return false;
                    }

                    return runningResult.Data.Any(x => !string.IsNullOrWhiteSpace(x.WorkFlowKey) && _startedWorkflowKeys.Contains(x.WorkFlowKey));
                }
            }
        }

        public string CurrentExecutionId
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentExecutionId;
                }
            }
        }

        public async Task<WorkflowExecutionSessionStartResult> StartAsync(
            string workflowKey,
            WorkFlowNode workflow,
            NodeContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workflowKey))
            {
                return new WorkflowExecutionSessionStartResult
                {
                    Success = false,
                    Message = "流程键不能为空"
                };
            }

            if (workflow == null)
            {
                return new WorkflowExecutionSessionStartResult
                {
                    Success = false,
                    Message = "流程对象不能为空"
                };
            }

            _workFlowManager.UnregisterWorkFlow(workflowKey);
            var registerResult = _workFlowManager.RegisterWorkFlow(workflowKey, workflow);
            if (!registerResult.Success)
            {
                return new WorkflowExecutionSessionStartResult
                {
                    Success = false,
                    Message = $"注册流程失败: {registerResult.Message}"
                };
            }

            context ??= new NodeContext();
            var engine = CreateEngineWithNodeEvents();
            // 把执行启动的同步开销放到后台线程，避免 UI 线程在“启动阶段”卡死。
            var runTask = Task.Run(async () =>
            {
                return engine == null
                    ? await _workFlowManager.ExecuteWorkFlowAsync(workflowKey, context, cancellationToken)
                    : await _workFlowManager.ExecuteWorkFlowAsync(workflowKey, engine, context, cancellationToken);
            });

            lock (_stateLock)
            {
                _currentWorkflowKey = workflowKey;
                _currentExecutionId = null;
                _executionTask = runTask;
                _startedWorkflowKeys.Add(workflowKey);
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var executionId = await ResolveExecutionIdAsync(workflowKey, CancellationToken.None);
                    lock (_stateLock)
                    {
                        _currentExecutionId = executionId;
                    }
                }
                catch
                {
                    // 背景同步执行ID失败时不阻塞主流程启动
                }
                finally
                {
                    try
                    {
                        await runTask;
                    }
                    catch
                    {
                        // 任务异常由上层消费，这里仅做状态清理
                    }

                    lock (_stateLock)
                    {
                        _startedWorkflowKeys.Remove(workflowKey);
                    }
                }
            });

            return new WorkflowExecutionSessionStartResult
            {
                Success = true,
                Message = "流程已启动",
                ExecutionId = null,
                ExecutionTask = runTask
            };
        }

        public OperationResult Pause()
        {
            if (!TryResolveCurrentExecutionId(out var executionId))
            {
                return OperationResult.Failure("未找到可暂停的执行实例");
            }

            return _workFlowManager.PauseWorkFlowExecution(executionId);
        }

        public OperationResult Resume()
        {
            if (!TryResolveCurrentExecutionId(out var executionId))
            {
                return OperationResult.Failure("未找到可恢复的执行实例");
            }

            return _workFlowManager.ResumeWorkFlowExecution(executionId);
        }

        public OperationResult Stop()
        {
            List<string> keysToStop;
            lock (_stateLock)
            {
                keysToStop = _startedWorkflowKeys.ToList();
            }

            if (keysToStop.Count == 0)
            {
                if (!TryResolveCurrentExecutionId(out var executionId))
                {
                    return OperationResult.Failure("未找到可停止的执行实例");
                }

                return _workFlowManager.CancelWorkFlowExecution(executionId);
            }

            var runningResult = _workFlowManager.GetRunningWorkFlows();
            if (!runningResult.Success || runningResult.Data == null)
            {
                return OperationResult.Failure("查询运行中的流程失败");
            }

            var targets = runningResult.Data
                .Where(x => !string.IsNullOrWhiteSpace(x.WorkFlowKey) && keysToStop.Contains(x.WorkFlowKey))
                .ToList();

            if (targets.Count == 0)
            {
                return OperationResult.Failure("未找到可停止的执行实例");
            }

            string lastError = null;
            int successCount = 0;
            foreach (var target in targets)
            {
                var cancelResult = _workFlowManager.CancelWorkFlowExecution(target.ExecutionId);
                if (cancelResult.Success)
                {
                    successCount++;
                }
                else
                {
                    lastError = cancelResult.Message;
                }
            }

            if (successCount > 0)
            {
                return OperationResult.Succeed($"已停止 {successCount} 个执行实例");
            }

            return OperationResult.Failure(string.IsNullOrWhiteSpace(lastError) ? "停止失败" : lastError);
        }

        private async Task<string> ResolveExecutionIdAsync(string workflowKey, CancellationToken cancellationToken)
        {
            // 启动后短时间轮询运行列表，获取本次 executionId。
            const int maxRetry = 20;
            for (int i = 0; i < maxRetry; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var runningResult = _workFlowManager.GetRunningWorkFlows();
                if (runningResult.Success && runningResult.Data != null)
                {
                    var execution = runningResult.Data
                        .Where(x => string.Equals(x.WorkFlowKey, workflowKey, StringComparison.Ordinal))
                        .OrderByDescending(x => x.StartTime)
                        .FirstOrDefault();
                    if (execution != null)
                    {
                        return execution.ExecutionId;
                    }
                }

                await Task.Delay(50, cancellationToken);
            }

            return null;
        }

        private bool TryResolveCurrentExecutionId(out string executionId)
        {
            lock (_stateLock)
            {
                executionId = _currentExecutionId;
                if (!string.IsNullOrWhiteSpace(executionId))
                {
                    return true;
                }

                var workflowKey = _currentWorkflowKey;
                if (string.IsNullOrWhiteSpace(workflowKey))
                {
                    return false;
                }

                var runningResult = _workFlowManager.GetRunningWorkFlows();
                if (!runningResult.Success || runningResult.Data == null)
                {
                    return false;
                }

                var execution = runningResult.Data
                    .Where(x => string.Equals(x.WorkFlowKey, workflowKey, StringComparison.Ordinal))
                    .OrderByDescending(x => x.StartTime)
                    .FirstOrDefault();
                if (execution == null)
                {
                    return false;
                }

                _currentExecutionId = execution.ExecutionId;
                executionId = execution.ExecutionId;
                return true;
            }
        }

        private IWorkFlowEngine CreateEngineWithNodeEvents()
        {
            try
            {
                var engine = _workflowEngineProvider.Create();
                if (engine == null)
                {
                    return null;
                }

                engine.NodeExecutionStarted += (_, e) =>
                {
                    if (e?.Node == null)
                    {
                        return;
                    }

                    NodeExecutionChanged?.Invoke(this, new WorkflowNodeExecutionChangedEventArgs
                    {
                        ExecutionId = ResolveExecutionId(e.Context),
                        WorkflowKey = ResolveWorkflowKey(e.Context),
                        NodeId = e.Node.Id,
                        State = NodeExecutionState.Running
                    });
                };

                engine.NodeExecutionCompleted += (_, e) =>
                {
                    if (e?.Node == null)
                    {
                        return;
                    }

                    NodeExecutionChanged?.Invoke(this, new WorkflowNodeExecutionChangedEventArgs
                    {
                        ExecutionId = ResolveExecutionId(e.Context),
                        WorkflowKey = ResolveWorkflowKey(e.Context),
                        NodeId = e.Node.Id,
                        State = MapState(e.Result)
                    });
                };

                return engine;
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveExecutionId(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue("ExecutionId", out var metadataExecutionId) &&
                metadataExecutionId is string executionId)
            {
                return executionId;
            }

            return context?.ExecutionId;
        }

        private static string ResolveWorkflowKey(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue("WorkFlowKey", out var metadataWorkFlowKey) &&
                metadataWorkFlowKey is string workflowKey)
            {
                return workflowKey;
            }

            return string.Empty;
        }

        private static NodeExecutionState MapState(ExecutionResult result)
        {
            if (result == null) return NodeExecutionState.Failed;
            if (result.ResultType == ExecutionResultType.Cancelled) return NodeExecutionState.Cancelled;
            if (result.IsSkipped || result.ResultType == ExecutionResultType.Skipped) return NodeExecutionState.Skipped;
            return result.Success ? NodeExecutionState.Success : NodeExecutionState.Failed;
        }
    }
}

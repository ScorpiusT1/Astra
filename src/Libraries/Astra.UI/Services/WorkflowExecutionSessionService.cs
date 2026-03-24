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
        private readonly object _stateLock = new object();

        private string _currentWorkflowKey;
        private string _currentExecutionId;
        private Task<OperationResult<ExecutionResult>> _executionTask;

        public event EventHandler<WorkflowNodeExecutionChangedEventArgs> NodeExecutionChanged;

        public WorkflowExecutionSessionService(IWorkFlowManager workFlowManager)
        {
            _workFlowManager = workFlowManager ?? throw new ArgumentNullException(nameof(workFlowManager));
        }

        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _executionTask != null && !_executionTask.IsCompleted;
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

            lock (_stateLock)
            {
                if (_executionTask != null && !_executionTask.IsCompleted)
                {
                    return new WorkflowExecutionSessionStartResult
                    {
                        Success = false,
                        Message = "当前已有流程在执行中"
                    };
                }
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
            var runTask = engine == null
                ? _workFlowManager.ExecuteWorkFlowAsync(workflowKey, context, cancellationToken)
                : _workFlowManager.ExecuteWorkFlowAsync(workflowKey, engine, context, cancellationToken);

            lock (_stateLock)
            {
                _currentWorkflowKey = workflowKey;
                _currentExecutionId = null;
                _executionTask = runTask;
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
            if (!TryResolveCurrentExecutionId(out var executionId))
            {
                return OperationResult.Failure("未找到可停止的执行实例");
            }

            return _workFlowManager.CancelWorkFlowExecution(executionId);
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
                var factoryType = Type.GetType("Astra.Engine.Execution.WorkFlowEngine.WorkFlowEngineFactory, Astra.Engine");
                var createDefaultMethod = factoryType?.GetMethod("CreateDefault");
                var engine = createDefaultMethod?.Invoke(null, null) as IWorkFlowEngine;
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

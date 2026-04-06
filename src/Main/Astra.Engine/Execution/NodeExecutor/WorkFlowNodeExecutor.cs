using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.Middleware;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.NodeExecutor
{
    /// <summary>
    /// 工作流节点执行器
    /// 专门用于执行 WorkFlowNode 类型的节点
    /// 当 WorkFlowNode 作为子节点在其他工作流中被执行时，使用此执行器
    /// 它委托给工作流引擎来执行工作流，同时支持中间件和拦截器
    /// </summary>
    public class WorkFlowNodeExecutor : INodeExecutor
    {
        private readonly IWorkFlowEngine _workFlowEngine;
        private readonly List<INodeMiddleware> _middlewares = new List<INodeMiddleware>();
        private readonly List<INodeInterceptor> _interceptors = new List<INodeInterceptor>();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workFlowEngine">工作流引擎实例，如果为null则使用默认引擎</param>
        public WorkFlowNodeExecutor(IWorkFlowEngine workFlowEngine = null)
        {
            _workFlowEngine = workFlowEngine ?? WorkFlowEngineFactory.CreateDefault();
            // 与 DefaultNodeExecutor 一致：流程节点整体也成块（子流程内容并入本块）
            _middlewares.Add(new NodeExecutionBlockLogMiddleware());
        }

        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        /// <returns>当前执行器实例，支持链式调用</returns>
        public INodeExecutor Use(INodeMiddleware middleware)
        {
            _middlewares.Add(middleware);
            return this;
        }

        /// <summary>
        /// 添加拦截器
        /// </summary>
        /// <param name="interceptor">拦截器实例</param>
        /// <returns>当前执行器实例，支持链式调用</returns>
        public INodeExecutor AddInterceptor(INodeInterceptor interceptor)
        {
            _interceptors.Add(interceptor);
            return this;
        }

        /// <summary>
        /// 执行节点
        /// 如果节点是 WorkFlowNode 类型，则使用工作流引擎执行
        /// 否则抛出异常
        /// </summary>
        /// <param name="node">要执行的节点（必须是 WorkFlowNode 类型）</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public async Task<ExecutionResult> ExecuteAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            // 检查节点类型
            if (!(node is WorkFlowNode workflow))
            {
                throw new ArgumentException($"WorkFlowNodeExecutor 只能执行 WorkFlowNode 类型的节点，当前节点类型为：{node.GetType().Name}", nameof(node));
            }

            node.ExecutionState = NodeExecutionState.Running;

            // 核心执行委托
            Func<CancellationToken, Task<ExecutionResult>> core = async (CancellationToken token) =>
            {
                // 执行拦截器：前置
                foreach (var interceptor in _interceptors)
                {
                    await interceptor.OnBeforeExecuteAsync(node, context, token);
                }

                ExecutionResult result;
                var wfLog = context.CreateExecutionLogger($"流程节点:{workflow.Name ?? workflow.Id}");
                wfLog.Info($"子流程引擎开始 Id={workflow.Id}");
                try
                {
                    // 使用工作流引擎执行工作流
                    result = await _workFlowEngine.ExecuteAsync(workflow, context, token).ConfigureAwait(false);
                    if (result == null)
                        wfLog.Warn("子流程引擎返回 null");
                    else if (result.Success)
                        wfLog.Info("子流程引擎结束: 成功");
                    else if (result.IsSkipped || result.ResultType == ExecutionResultType.Skipped)
                        wfLog.Info(string.IsNullOrWhiteSpace(result.Message) ? "子流程引擎: 已跳过" : $"子流程引擎: 已跳过 ({result.Message})");
                    else if (result.ResultType == ExecutionResultType.Cancelled)
                        wfLog.Warn(string.IsNullOrWhiteSpace(result.Message) ? "子流程引擎: 已取消" : $"子流程引擎: 已取消 ({result.Message})");
                    else
                        wfLog.Warn(string.IsNullOrWhiteSpace(result.Message) ? "子流程引擎: 失败" : $"子流程引擎: 失败 ({result.Message})");
                }
                catch (Exception ex)
                {
                    wfLog.Error($"子流程引擎异常: {ex.Message}");
                    // 执行拦截器：异常
                    foreach (var interceptor in _interceptors)
                    {
                        await interceptor.OnExceptionAsync(node, ex, token);
                    }
                    throw;
                }

                // 执行拦截器：后置
                foreach (var interceptor in _interceptors)
                {
                    await interceptor.OnAfterExecuteAsync(node, result, token);
                }

                return result;
            };

            // 反向构建中间件链
            Func<CancellationToken, Task<ExecutionResult>> pipeline = core;
            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = _middlewares[i];
                var next = pipeline;
                pipeline = (tok) => middleware.InvokeAsync(node, context, tok, next);
            }

            // 执行管道
            try
            {
                var result = await pipeline(cancellationToken);
                node.ExecutionState = MapState(result);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                node.ExecutionState = NodeExecutionState.Cancelled;
                throw;
            }
            catch
            {
                node.ExecutionState = NodeExecutionState.Failed;
                throw;
            }
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


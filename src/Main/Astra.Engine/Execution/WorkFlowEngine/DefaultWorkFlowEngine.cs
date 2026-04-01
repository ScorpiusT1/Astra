using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using Astra.Engine.Execution.WorkFlowEngine.Management;
using Astra.Engine.Execution.Strategies;
using Astra.Engine.Execution.Validators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 默认工作流执行引擎 - 自动策略检测
    /// 负责执行工作流，自动检测并选择合适的执行策略
    /// </summary>
    public class DefaultWorkFlowEngine : IWorkFlowEngine
    {
        private readonly IStrategyDetector _strategyDetector;
        private readonly IExecutionStrategyFactory _strategyFactory;
        private readonly IWorkFlowValidator _validator;
        private readonly ILogger _logger;

        /// <summary>
        /// 执行统计信息
        /// </summary>
        public WorkFlowExecutionStatistics Statistics { get; private set; }

        /// <summary>
        /// 节点开始执行事件
        /// </summary>
        public event EventHandler<NodeExecutionEventArgs> NodeExecutionStarted;

        /// <summary>
        /// 节点执行完成事件
        /// </summary>
        public event EventHandler<NodeExecutionEventArgs> NodeExecutionCompleted;

        /// <summary>
        /// 进度变化事件
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// 策略检测事件
        /// </summary>
        public event EventHandler<StrategyDetectedEventArgs> StrategyDetected;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="strategyDetector">策略检测器，如果为null则使用默认检测器</param>
        /// <param name="strategyFactory">策略工厂，如果为null则使用默认工厂</param>
        /// <param name="validator">工作流验证器，如果为null则使用默认验证器</param>
        /// <param name="logger">日志记录器，如果为null则使用空日志器</param>
        public DefaultWorkFlowEngine(
            IStrategyDetector strategyDetector = null,
            IExecutionStrategyFactory strategyFactory = null,
            IWorkFlowValidator validator = null,
            ILogger logger = null)
        {
            _strategyDetector = strategyDetector ?? new DefaultStrategyDetector();
            _strategyFactory = strategyFactory ?? new DefaultExecutionStrategyFactory();
            _validator = validator ?? new DefaultWorkFlowValidator();
            _logger = logger ?? NullLogger.Instance;
            Statistics = new WorkFlowExecutionStatistics();
        }

        /// <summary>
        /// 执行工作流
        /// </summary>
        public async Task<ExecutionResult> ExecuteAsync(
            WorkFlowNode workflow,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            Statistics = new WorkFlowExecutionStatistics();

            try
            {
                // 1. 验证
                var validation = Validate(workflow);
                if (!validation.IsValid)
                {
                    return ExecutionResult.Failed($"工作流验证失败: {string.Join(", ", validation.Errors)}");
                }

                // 反序列化旧脚本时 Configuration 可能为 null，会导致 MaxParallelism/StopOnError 等读取不一致
                if (workflow.Configuration == null)
                {
                    workflow.Configuration = new WorkFlowConfiguration();
                }

                // 1.5 清除上一轮残留的运行时状态，避免跨次执行污染
                ResetNodesRuntimeState(workflow);

                // 2. 检测策略
                var detectedStrategy = DetectStrategy(workflow);
                OnStrategyDetected(new StrategyDetectedEventArgs { Strategy = detectedStrategy });

                if (workflow.Configuration.EnableDetailedLogging)
                {
                    LogStrategy(detectedStrategy);
                }

                // 3. 创建执行策略
                var strategy = _strategyFactory.CreateStrategy(detectedStrategy.Type);

                // 4. 创建流程级 Logger 并注入到 ServiceProvider
                await using var loggerScope = WorkFlowLoggerScope.Create(context, workflow);
                var sp = new ScopedServiceProvider(context?.ServiceProvider);
                // 将 ILogger 注入到 ServiceProvider，供其他组件使用
                sp.AddService(typeof(ILogger), loggerScope.Logger);

                // 5. 创建执行上下文
                var executionContext = new WorkFlowExecutionContext
                {
                    Workflow = workflow,
                    NodeContext = PrepareContext(workflow, context),
                    CancellationToken = cancellationToken,
                    ExecutionController = ResolveExecutionController(context),
                    ExecutionId = ResolveExecutionId(context),
                    WorkFlowKey = ResolveWorkFlowKey(context),
                    StartTime = startTime,
                    Statistics = Statistics,
                    DetectedStrategy = detectedStrategy
                };

                // 注入包含流程 Logger 的 ServiceProvider
                executionContext.NodeContext.ServiceProvider = sp;

                // 传递事件处理器
                executionContext.OnNodeExecutionStarted = (node, ctx) => OnNodeExecutionStarted(new NodeExecutionEventArgs { Node = node, Context = ctx });
                executionContext.OnNodeExecutionCompleted = (node, ctx, result) => OnNodeExecutionCompleted(new NodeExecutionEventArgs { Node = node, Context = ctx, Result = result });
                executionContext.OnProgressChanged = (progress) => OnProgressChanged(new ProgressChangedEventArgs { Progress = progress });

                // 6. 主阶段执行
                var mainResult = await strategy.ExecuteAsync(executionContext);

                // 7. 最后执行阶段（与主阶段失败正交；取消则不再运行）
                ExecutionResult finalResult = mainResult;
                if (!cancellationToken.IsCancellationRequested)
                {
                    var finallyOutcome = await FinallyPhaseRunner.RunAsync(executionContext);
                    finalResult = CombineMainAndFinallyResults(mainResult, finallyOutcome);
                }

                // 8. 更新统计
                finalResult.StartTime = startTime;
                finalResult.EndTime = DateTime.Now;
                Statistics.TotalDuration = finalResult.Duration;
                Statistics.ExecutionStrategy = detectedStrategy.Type.ToString();

                return finalResult;
            }
            catch (OperationCanceledException)
            {
                return ExecutionResult.Cancel("工作流执行已取消");
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"工作流执行异常: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证工作流
        /// </summary>
        public ValidationResult Validate(WorkFlowNode workflow)
        {
            return _validator.Validate(workflow);
        }

        /// <summary>
        /// 检测执行策略
        /// </summary>
        public DetectedExecutionStrategy DetectStrategy(WorkFlowNode workflow)
        {
            return _strategyDetector.Detect(workflow);
        }

        /// <summary>
        /// 清除所有子节点上一轮残留的运行时状态（LastExecutionResult / ExecutionState），
        /// 防止跨次执行时策略逻辑误判"本轮是否已执行"或 UI 显示过期结果。
        /// </summary>
        private static void ResetNodesRuntimeState(WorkFlowNode workflow)
        {
            if (workflow?.Nodes == null) return;

            foreach (var node in workflow.Nodes)
            {
                node.LastExecutionResult = null;
                node.ExecutionState = NodeExecutionState.Idle;
            }

            workflow.LastExecutionResult = null;
            workflow.ExecutionState = NodeExecutionState.Idle;
        }

        /// <summary>
        /// 准备节点执行上下文
        /// </summary>
        private NodeContext PrepareContext(WorkFlowNode workflow, NodeContext baseContext)
        {
            var context = new NodeContext
            {
                InputData = new Dictionary<string, object>(baseContext?.InputData ?? new Dictionary<string, object>()),
                GlobalVariables = new Dictionary<string, object>(baseContext?.GlobalVariables ?? new Dictionary<string, object>()),
                ServiceProvider = baseContext?.ServiceProvider,

                // 继承执行标识与元数据，保证 UI 事件过滤时的 ExecutionId 一致
                ExecutionId = baseContext?.ExecutionId,
                ParentWorkFlow = workflow,
                Metadata = new Dictionary<string, object>(baseContext?.Metadata ?? new Dictionary<string, object>())
            };

            foreach (var kvp in workflow.Variables)
            {
                context.GlobalVariables[kvp.Key] = kvp.Value;
            }

            return context;
        }

        /// <summary>
        /// 记录策略信息
        /// </summary>
        private void LogStrategy(DetectedExecutionStrategy strategy)
        {
            // 使用结构化日志而非控制台输出
            _logger.LogInformation("=".PadRight(60, '='));
            _logger.LogInformation("检测到执行策略: {StrategyType}", strategy.Type);
            _logger.LogInformation("描述: {Description}", strategy.Description);
            _logger.LogInformation("原因: {Reason}", strategy.Reason);
            _logger.LogInformation("=".PadRight(60, '='));
        }

        private static WorkFlowExecutionController ResolveExecutionController(NodeContext context)
        {
            if (context?.Metadata == null) return null;
            if (context.Metadata.TryGetValue(EngineConstants.MetadataKeys.WorkflowExecutionController, out var value) &&
                value is WorkFlowExecutionController controller)
            {
                return controller;
            }

            return null;
        }

        private static string ResolveExecutionId(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue(EngineConstants.MetadataKeys.ExecutionId, out var value) &&
                value is string executionId &&
                !string.IsNullOrWhiteSpace(executionId))
            {
                return executionId;
            }

            return context?.ExecutionId;
        }

        private static string ResolveWorkFlowKey(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue(EngineConstants.MetadataKeys.WorkFlowKey, out var value) &&
                value is string workFlowKey)
            {
                return workFlowKey;
            }

            return string.Empty;
        }

        /// <summary>
        /// 合并主阶段与最后执行阶段结果：最后阶段失败优先返回失败；主阶段失败仍保留为主因。
        /// </summary>
        private static ExecutionResult CombineMainAndFinallyResults(ExecutionResult main, ExecutionResult finalPhase)
        {
            if (finalPhase == null)
            {
                return main;
            }

            if (finalPhase.Success && string.IsNullOrEmpty(finalPhase.Message))
            {
                return main;
            }

            if (!finalPhase.Success && !finalPhase.IsSkipped)
            {
                if (!main.Success && !main.IsSkipped)
                {
                    return ExecutionResult.Failed($"{main.Message}；最后执行阶段：{finalPhase.Message}", finalPhase.Exception ?? main.Exception);
                }

                return ExecutionResult.Failed($"最后执行阶段失败: {finalPhase.Message}", finalPhase.Exception);
            }

            if (!main.Success && !main.IsSkipped)
            {
                return main;
            }

            if (string.IsNullOrEmpty(main.Message))
            {
                return finalPhase;
            }

            return ExecutionResult.Successful($"{main.Message}；{finalPhase.Message}");
        }

        /// <summary>
        /// 触发节点执行开始事件
        /// </summary>
        protected virtual void OnNodeExecutionStarted(NodeExecutionEventArgs e) => NodeExecutionStarted?.Invoke(this, e);

        /// <summary>
        /// 触发节点执行完成事件
        /// </summary>
        protected virtual void OnNodeExecutionCompleted(NodeExecutionEventArgs e) => NodeExecutionCompleted?.Invoke(this, e);

        /// <summary>
        /// 触发进度变化事件
        /// </summary>
        protected virtual void OnProgressChanged(ProgressChangedEventArgs e) => ProgressChanged?.Invoke(this, e);

        /// <summary>
        /// 触发策略检测事件
        /// </summary>
        protected virtual void OnStrategyDetected(StrategyDetectedEventArgs e) => StrategyDetected?.Invoke(this, e);
    }
}


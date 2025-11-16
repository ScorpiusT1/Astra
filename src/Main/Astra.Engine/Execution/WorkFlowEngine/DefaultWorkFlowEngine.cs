using Astra.Core.Nodes.Models;
using Astra.Core.Logs;
using Astra.Engine.Execution.WorkFlowEngine;
using Astra.Engine.Execution.Strategies;
using Astra.Engine.Execution.Validators;
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
        public DefaultWorkFlowEngine(
            IStrategyDetector strategyDetector = null,
            IExecutionStrategyFactory strategyFactory = null,
            IWorkFlowValidator validator = null)
        {
            _strategyDetector = strategyDetector ?? new DefaultStrategyDetector();
            _strategyFactory = strategyFactory ?? new DefaultExecutionStrategyFactory();
            _validator = validator ?? new DefaultWorkFlowValidator();
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
                var createdWorkflowLogger = false;
                ILogger workflowLogger = null;

                try
                {
                    var existing = context?.ServiceProvider?.GetService(typeof(Logger)) as Logger;
                    if (existing == null)
                    {
                        workflowLogger = Logger.CreateForWorkflow(workflow.Id, workflow.Name);
                        createdWorkflowLogger = true;
                    }
                    else
                    {
                        workflowLogger = existing;
                    }
                }
                catch
                {
                    workflowLogger = Logger.CreateForWorkflow(workflow.Id, workflow.Name);
                    createdWorkflowLogger = true;
                }

                var sp = new ScopedServiceProvider(context?.ServiceProvider);
                sp.AddService(typeof(Logger), workflowLogger);

                // 5. 创建执行上下文
                var executionContext = new WorkFlowExecutionContext
                {
                    Workflow = workflow,
                    NodeContext = PrepareContext(workflow, context),
                    CancellationToken = cancellationToken,
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

                // 6. 执行
                var result = await strategy.ExecuteAsync(executionContext);

                // 7. 更新统计
                result.StartTime = startTime;
                result.EndTime = DateTime.Now;
                Statistics.TotalDuration = result.Duration;
                Statistics.ExecutionStrategy = detectedStrategy.Type.ToString();

                // 8. 若为本流程创建了 Logger，则在流程结束时关闭
                if (createdWorkflowLogger && workflowLogger != null)
                {
                    await workflowLogger.ShutdownAsync();
                }

                return result;
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
        /// 准备节点执行上下文
        /// </summary>
        private NodeContext PrepareContext(WorkFlowNode workflow, NodeContext baseContext)
        {
            var context = new NodeContext
            {
                InputData = new Dictionary<string, object>(baseContext?.InputData ?? new Dictionary<string, object>()),
                GlobalVariables = new Dictionary<string, object>(baseContext?.GlobalVariables ?? new Dictionary<string, object>()),
                ServiceProvider = baseContext?.ServiceProvider
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
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine($"检测到执行策略: {strategy.Type}");
            Console.WriteLine($"描述: {strategy.Description}");
            Console.WriteLine($"原因: {strategy.Reason}");
            Console.WriteLine("=".PadRight(60, '='));
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


﻿﻿using Astra.Core.Nodes.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace Astra.Core.Nodes.Models
{

    // ========================================
    // 第1层：数据结构（纯 POCO）
    // ========================================

    /// <summary>
    /// 工作流节点 - 纯数据结构，不包含执行逻辑
    /// 符合单一职责原则：专门负责工作流的数据组织和管理
    /// 符合开闭原则：通过Configuration支持扩展
    /// </summary>
    public class WorkFlowNode : Node
    {
        private static readonly object _relationshipLock = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        public WorkFlowNode()
        {
            NodeType = "WorkFlowNode";
            Name = "流程容器";
            Icon = "📋";
            Size = new Size2D(300, 200);

            Nodes = new List<Node>();
            Connections = new List<Connection>();
            Variables = new Dictionary<string, object>();

            // 配置
            Configuration = new WorkFlowConfiguration();
        }

        // ===== 数据：子节点 =====

        [JsonPropertyOrder(20)]
        public List<Node> Nodes { get; set; }

        [JsonPropertyOrder(21)]
        public List<Connection> Connections { get; set; }

        [JsonPropertyOrder(22)]
        public Dictionary<string, object> Variables { get; set; }

        // ===== 配置 =====

        [JsonPropertyOrder(23)]
        public WorkFlowConfiguration Configuration { get; set; }

        // ===== 辅助方法（仅数据操作，无执行逻辑） =====

        /// <summary>
        /// 添加节点
        /// 符合单一职责原则：专门负责节点的添加和验证
        /// </summary>
        /// <param name="node">要添加的节点</param>
        /// <exception cref="ArgumentNullException">节点为null时抛出</exception>
        /// <exception cref="InvalidOperationException">节点ID已存在时抛出</exception>
        public void AddNode(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (Nodes.Any(n => n.Id == node.Id))
                throw new InvalidOperationException($"节点 {node.Id} 已存在");
            Nodes.Add(node);
        }

        /// <summary>
        /// 移除节点（同时移除相关连接）
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>如果成功移除返回true，否则返回false</returns>
        public bool RemoveNode(string nodeId)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) return false;
            Connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
            return Nodes.Remove(node);
        }

        /// <summary>
        /// 添加连接
        /// 符合单一职责原则：专门负责连接的添加和验证
        /// </summary>
        /// <param name="connection">要添加的连接</param>
        /// <exception cref="ArgumentNullException">连接为null时抛出</exception>
        /// <exception cref="InvalidOperationException">源节点或目标节点不存在时抛出</exception>
        public void AddConnection(Connection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (!Nodes.Any(n => n.Id == connection.SourceNodeId))
                throw new InvalidOperationException($"源节点 {connection.SourceNodeId} 不存在");
            if (!Nodes.Any(n => n.Id == connection.TargetNodeId))
                throw new InvalidOperationException($"目标节点 {connection.TargetNodeId} 不存在");
            Connections.Add(connection);
        }

        /// <summary>
        /// 移除连接
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <returns>如果成功移除返回true，否则返回false</returns>
        public bool RemoveConnection(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            return connection != null && Connections.Remove(connection);
        }

        /// <summary>
        /// 获取节点
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点对象，如果不存在则返回null</returns>
        public Node GetNode(string nodeId)
        {
            return Nodes.FirstOrDefault(n => n.Id == nodeId);
        }

        /// <summary>
        /// 获取节点的输入连接
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>输入连接列表</returns>
        public List<Connection> GetInputConnections(string nodeId)
        {
            return Connections.Where(c => c.TargetNodeId == nodeId).ToList();
        }

        /// <summary>
        /// 获取节点的输出连接
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>输出连接列表</returns>
        public List<Connection> GetOutputConnections(string nodeId)
        {
            return Connections.Where(c => c.SourceNodeId == nodeId).ToList();
        }

        // ===== 关系重建（符合单一职责原则） =====

        /// <summary>
        /// 重建运行时关系（反序列化/克隆后调用）
        /// 符合单一职责原则：专门负责重建对象间的引用关系
        /// 线程安全：使用锁保护并发操作
        /// </summary>
        public void RebuildRelationships()
        {
            lock (_relationshipLock)
            {
                var nodeMap = Nodes.ToDictionary(n => n.Id);
                var portMap = new Dictionary<string, Port>();

                // 重建节点和端口的关系
                foreach (var node in Nodes)
                {
                    if (node.InputPorts != null)
                    {
                        foreach (var port in node.InputPorts)
                        {
                            port.ParentNode = node;
                            port.NodeId = node.Id;
                           
                            port.Connections = new List<Connection>();
                            portMap[port.Id] = port;
                        }
                    }

                    if (node.OutputPorts != null)
                    {
                        foreach (var port in node.OutputPorts)
                        {
                            port.ParentNode = node;
                            port.NodeId = node.Id;
                           
                            port.Connections = new List<Connection>();
                            portMap[port.Id] = port;
                        }
                    }
                }

                // 重建连接的关系
                foreach (var connection in Connections)
                {
                    if (nodeMap.TryGetValue(connection.SourceNodeId, out var sourceNode))
                    {
                        connection.SourceNode = sourceNode;
                    }

                    if (nodeMap.TryGetValue(connection.TargetNodeId, out var targetNode))
                    {
                        connection.TargetNode = targetNode;
                    }

                    if (portMap.TryGetValue(connection.SourcePortId, out var sourcePort))
                    {
                        connection.SourcePort = sourcePort;
                        sourcePort.Connections.Add(connection);
                    }

                    if (portMap.TryGetValue(connection.TargetPortId, out var targetPort))
                    {
                        connection.TargetPort = targetPort;
                        targetPort.Connections.Add(connection);
                    }
                }
            }
        }

        // ===== 验证（符合开闭原则：重写基类方法扩展验证逻辑） =====

        /// <summary>
        /// 验证工作流及其所有子节点和连接
        /// 符合单一职责原则：专门负责验证逻辑
        /// 符合开闭原则：扩展基类验证而不修改基类
        /// </summary>
        public override ValidationResult Validate()
        {
            var errors = new List<string>();

            // 验证基类规则
            var baseValidation = base.Validate();
            if (!baseValidation.IsValid)
                errors.AddRange(baseValidation.Errors);

            // 验证子节点
            if (Nodes == null || Nodes.Count == 0)
                errors.Add("工作流至少需要一个节点");

            var nodeIds = new HashSet<string>();
            foreach (var node in Nodes ?? new List<Node>())
            {
                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    errors.Add("存在ID为空的节点");
                    continue;
                }

                if (nodeIds.Contains(node.Id))
                    errors.Add($"存在重复的节点ID: {node.Id}");
                else
                    nodeIds.Add(node.Id);

                var nodeValidation = node.Validate();
                if (!nodeValidation.IsValid)
                    errors.Add($"节点 '{node.Name}' 验证失败: {string.Join(", ", nodeValidation.Errors)}");
            }

            // 验证连接
            var connectionIds = new HashSet<string>();
            foreach (var connection in Connections ?? new List<Connection>())
            {
                if (connectionIds.Contains(connection.Id))
                    errors.Add($"存在重复的连接ID: {connection.Id}");
                else
                    connectionIds.Add(connection.Id);

                var connValidation = connection.Validate();
                if (!connValidation.IsValid)
                    errors.Add($"连接 {connection.Id} 验证失败: {string.Join(", ", connValidation.Errors)}");

                if (!nodeIds.Contains(connection.SourceNodeId))
                    errors.Add($"连接的源节点不存在: {connection.SourceNodeId}");

                if (!nodeIds.Contains(connection.TargetNodeId))
                    errors.Add($"连接的目标节点不存在: {connection.TargetNodeId}");
            }

            // 验证配置
            if (Configuration != null)
            {
                if (Configuration.MaxParallelism <= 0)
                    errors.Add("最大并行度必须大于0");

                if (Configuration.TimeoutSeconds < 0)
                    errors.Add("超时时间不能为负数");
            }

            return errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }
        
        // ===== 执行入口 =====
        
        /// <summary>
        /// 执行工作流的核心逻辑
        /// 符合依赖倒置原则：通过反射避免Core层依赖Engine层
        /// </summary>
        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            // 当 WorkFlowNode 作为子节点被 NodeExecutor 执行时，需要调用工作流引擎
            // 使用反射来避免 Core 直接依赖 Engine，保持架构清晰
            
            // 使用默认工作流引擎执行（通过反射避免直接依赖）
            var engineType = Type.GetType("Astra.Engine.Execution.WorkFlowEngine.WorkFlowEngineFactory, Astra.Engine");
            if (engineType != null)
            {
                var createDefaultMethod = engineType.GetMethod("CreateDefault", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (createDefaultMethod != null)
                {
                    var engine = createDefaultMethod.Invoke(null, null);
                    var executeMethod = engine.GetType().GetMethod("ExecuteAsync");
                    if (executeMethod != null)
                    {
                        var task = (Task<ExecutionResult>)executeMethod.Invoke(engine, new object[] { this, context, cancellationToken });
                        return await task;
                    }
                }
            }
            
            // 如果反射失败，抛出异常提示使用扩展方法或专门的执行器
            throw new InvalidOperationException(
                "WorkFlowNode 执行失败。请确保已引用 Astra.Engine 库。\n" +
                "推荐使用以下方式之一：\n" +
                "1. 使用扩展方法：using Astra.Engine.Execution.WorkFlowEngine; await workflow.ExecuteAsync(context, cancellationToken);\n" +
                "2. 使用 WorkFlowNodeExecutor：using Astra.Engine.Execution.NodeExecutor; await workflow.ExecuteAsync(executor, context, cancellationToken);");
        }

        /// <summary>
        /// 克隆工作流节点
        /// 符合开闭原则：重写基类方法，定制克隆逻辑
        /// </summary>
        public override Node Clone()
        {
            // 1) JSON 深拷贝整个工作流（包含子节点/连接/变量/配置）
            var json = JsonSerializer.Serialize(this, GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                ReferenceHandler = ReferenceHandler.Preserve
            });
            var cloned = (WorkFlowNode)JsonSerializer.Deserialize(json, GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                ReferenceHandler = ReferenceHandler.Preserve
            });

            // 2) 重新生成工作流自身ID
            cloned.Id = Guid.NewGuid().ToString();

            // 3) 重新分配所有子节点的ID，并建立旧->新 映射
            var nodeIdMap = new Dictionary<string, string>();
            foreach (var n in cloned.Nodes)
            {
                var oldId = n.Id;
                n.Id = Guid.NewGuid().ToString();
                nodeIdMap[oldId] = n.Id;
            }

            // 4) 修补连接：重生ID，并用映射修复 SourceNodeId/TargetNodeId
            for (int i = 0; i < cloned.Connections.Count; i++)
            {
                var c = cloned.Connections[i];
                c.Id = Guid.NewGuid().ToString();
                if (nodeIdMap.TryGetValue(c.SourceNodeId, out var newSrc)) c.SourceNodeId = newSrc;
                if (nodeIdMap.TryGetValue(c.TargetNodeId, out var newDst)) c.TargetNodeId = newDst;
            }

            // 5) 重建运行时关系
            cloned.RebuildRelationships();

            // 6) 调用统一后处理钩子
            AfterClone(cloned);
            return cloned;
        }
    }

    /// <summary>
    /// 工作流配置
    /// 符合单一职责原则：专门负责工作流执行配置
    /// 符合开闭原则：通过属性扩展而不修改现有代码
    /// </summary>
    public class WorkFlowConfiguration
    {
        /// <summary>
        /// 遇到错误时是否停止
        /// </summary>
        public bool StopOnError { get; set; } = true;

        /// <summary>
        /// 最大并行度
        /// </summary>
        public int MaxParallelism { get; set; } = 4;

        /// <summary>
        /// 超时时间（秒），0表示无超时
        /// </summary>
        public int TimeoutSeconds { get; set; } = 0;

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// 是否自动检测执行策略
        /// </summary>
        public bool AutoDetectStrategy { get; set; } = true;

        /// <summary>
        /// 克隆配置
        /// </summary>
        /// <returns>克隆后的配置对象</returns>
        public WorkFlowConfiguration Clone()
        {
            return new WorkFlowConfiguration
            {
                StopOnError = this.StopOnError,
                MaxParallelism = this.MaxParallelism,
                TimeoutSeconds = this.TimeoutSeconds,
                EnableDetailedLogging = this.EnableDetailedLogging,
                AutoDetectStrategy = this.AutoDetectStrategy
            };
        }
    }

    // ========================================
    // 第2层：执行引擎接口（接口定义保留在 Core，实现移至 Engine）
    // ========================================

    /// <summary>
    /// 工作流执行引擎接口
    /// 符合单一职责：专门负责工作流执行
    /// 符合依赖倒置：高层模块依赖抽象
    /// </summary>
    public interface IWorkFlowEngine
    {
        /// <summary>
        /// 执行工作流
        /// </summary>
        /// <param name="workflow">要执行的工作流</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        Task<ExecutionResult> ExecuteAsync(
            WorkFlowNode workflow,
            NodeContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// 验证工作流
        /// </summary>
        /// <param name="workflow">要验证的工作流</param>
        /// <returns>验证结果</returns>
        ValidationResult Validate(WorkFlowNode workflow);

        /// <summary>
        /// 检测执行策略
        /// </summary>
        /// <param name="workflow">要检测的工作流</param>
        /// <returns>检测到的执行策略</returns>
        DetectedExecutionStrategy DetectStrategy(WorkFlowNode workflow);

        /// <summary>
        /// 执行统计信息
        /// </summary>
        WorkFlowExecutionStatistics Statistics { get; }

        /// <summary>
        /// 节点开始执行事件
        /// </summary>
        event EventHandler<NodeExecutionEventArgs> NodeExecutionStarted;

        /// <summary>
        /// 节点执行完成事件
        /// </summary>
        event EventHandler<NodeExecutionEventArgs> NodeExecutionCompleted;

        /// <summary>
        /// 进度变化事件
        /// </summary>
        event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// 策略检测事件
        /// </summary>
        event EventHandler<StrategyDetectedEventArgs> StrategyDetected;
    }

    // ========================================
    // 注意：所有执行相关的实现类已迁移至 Astra.Engine
    // ========================================
    // 以下类已迁移：
    // - DefaultWorkFlowEngine -> Astra.Engine.Execution.WorkFlowEngine.DefaultWorkFlowEngine
    // - WorkFlowEngineFactory -> Astra.Engine.Execution.WorkFlowEngine.WorkFlowEngineFactory
    // - IStrategyDetector, DefaultStrategyDetector -> Astra.Engine.Execution.Strategies
    // - GraphAnalyzer -> Astra.Engine.Execution.Strategies.GraphAnalyzer
    // - IExecutionStrategy, IExecutionStrategyFactory -> Astra.Engine.Execution.Strategies
    // - DefaultExecutionStrategyFactory -> Astra.Engine.Execution.Strategies.DefaultExecutionStrategyFactory
    // - ParallelExecutionStrategy, SequentialExecutionStrategy, PartiallyParallelExecutionStrategy, ComplexGraphExecutionStrategy -> Astra.Engine.Execution.Strategies
    // - IWorkFlowValidator, DefaultWorkFlowValidator -> Astra.Engine.Execution.Validators
    // - WorkFlowExecutionContext -> Astra.Engine.Execution.WorkFlowEngine.WorkFlowExecutionContext
    // - DetectedExecutionStrategy, ExecutionStrategyType -> Astra.Engine.Execution.WorkFlowEngine.Models
    // - WorkFlowExecutionStatistics -> Astra.Engine.Execution.WorkFlowEngine.Models.WorkFlowExecutionStatistics
    // - NodeExecutionEventArgs, ProgressChangedEventArgs, StrategyDetectedEventArgs -> Astra.Engine.Execution.WorkFlowEngine.Events
    // - ExecutionResultExtensions -> Astra.Engine.Execution.WorkFlowEngine.Extensions.ExecutionResultExtensions
    // 
    // 使用方式：
    // using Astra.Engine.Execution.WorkFlowEngine;  // 使用 WorkFlowExecutionExtensions.ExecuteAsync()
    // using Astra.Engine.Execution.WorkFlowEngine;  // 使用 DefaultWorkFlowEngine, WorkFlowEngineFactory
    // using Astra.Engine.Execution.Strategies;    // 使用策略检测器和执行策略
    // using Astra.Engine.Execution.Validators;    // 使用工作流验证器
    // ========================================
}

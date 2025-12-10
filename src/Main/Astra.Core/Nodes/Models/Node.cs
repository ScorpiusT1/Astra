﻿using Astra.Core.Nodes.Geometry;
using System.Diagnostics;
using Astra.Core.Logs;
using System.Text.Json.Serialization;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Astra.Core.Nodes.Models
{

    // ========================================
    // 第1层：节点数据结构（纯POCO）
    // ========================================

    /// <summary>
    /// 节点基类 - 纯数据结构，不包含执行逻辑
    /// 设计原则：
    /// 1. 单一职责：仅负责节点数据的定义和基本验证
    /// 2. 开闭原则：通过抽象方法 ExecuteCoreAsync 支持扩展
    /// 3. 里氏替换：子类可以安全替换基类
    /// </summary>
    public abstract class Node
    {
        private static readonly JsonSerializerOptions jsonCloneOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        protected Node()
        {
            Id = Guid.NewGuid().ToString();
            Parameters = new Dictionary<string, object>();
            IsEnabled = true;
            InputPorts = new List<Port>();
            OutputPorts = new List<Port>();
        }

        // ===== 基本属性 =====
        
        [JsonPropertyOrder(1)]
        public string Id { get; set; }
        
        [JsonPropertyOrder(2)]
        public string NodeType { get; set; }
        
        [JsonPropertyOrder(3)]
        public string Name { get; set; }
        
        [JsonPropertyOrder(4)]
        public string Description { get; set; }
        
        [JsonPropertyOrder(5)]
        public string Icon { get; set; }
        
        [JsonPropertyOrder(6)]
        public string Color { get; set; }

        // ===== 状态属性 =====
        
        [JsonPropertyOrder(7)]
        public bool IsEnabled { get; set; }
        
        [JsonPropertyOrder(8)]
        public bool IsReadonly { get; set; }
        
        [JsonPropertyOrder(9)]
        public bool IsLocked { get; set; }
        
        /// <summary>
        /// 节点是否被选中（用于 UI 框选等交互）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected { get; set; }

        // ===== 参数和结果 =====
        
        [JsonPropertyOrder(10)]
        public Dictionary<string, object> Parameters { get; set; }

        [JsonIgnore]
        public ExecutionResult LastExecutionResult { get; set; }

        [JsonIgnore]
        public NodeExecutionState ExecutionState { get; set; }

        // ===== 端口集合 =====
        
        [JsonPropertyOrder(11)]
        public List<Port> InputPorts { get; set; }
        
        [JsonPropertyOrder(12)]
        public List<Port> OutputPorts { get; set; }

        // ===== 布局属性 =====
        
        [JsonPropertyOrder(13)]
        public Point2D Position { get; set; }
        
        [JsonPropertyOrder(14)]
        public Size2D Size { get; set; }

        // ===== 端口管理方法（符合单一职责原则） =====

        /// <summary>
        /// 添加输入端口
        /// </summary>
        public virtual void AddInputPort(Port port)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            
            port.NodeId = this.Id;
            port.ParentNode = this;
           
            if (!InputPorts.Any(p => p.Id == port.Id))
            {
                InputPorts.Add(port);
            }
        }

        /// <summary>
        /// 添加输出端口
        /// </summary>
        public virtual void AddOutputPort(Port port)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            
            port.NodeId = this.Id;
            port.ParentNode = this;
            
            if (!OutputPorts.Any(p => p.Id == port.Id))
            {
                OutputPorts.Add(port);
            }
        }

        /// <summary>
        /// 移除端口
        /// </summary>
        public virtual bool RemovePort(string portId)
        {
            var removed = InputPorts.RemoveAll(p => p.Id == portId);
            removed += OutputPorts.RemoveAll(p => p.Id == portId);
            return removed > 0;
        }

        /// <summary>
        /// 获取端口
        /// </summary>
        public virtual Port GetPort(string portId)
        {
            return InputPorts.FirstOrDefault(p => p.Id == portId) 
                   ?? OutputPorts.FirstOrDefault(p => p.Id == portId);
        }

        /// <summary>
        /// 获取所有端口
        /// </summary>
        public virtual IEnumerable<Port> GetAllPorts()
        {
            return InputPorts.Concat(OutputPorts);
        }

        // ===== 核心方法：定义节点的业务逻辑（不包含执行基础设施） =====

        /// <summary>
        /// 节点的核心业务逻辑（由子类实现）
        /// 不需要处理日志、异常捕获、重试等基础设施逻辑
        /// </summary>
        protected abstract Task<ExecutionResult> ExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// 执行入口 - 供执行器调用
        /// 此方法由执行引擎调用，执行节点的核心逻辑（ExecuteCoreAsync）
        /// </summary>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public Task<ExecutionResult> InvokeExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(context, cancellationToken);
        }

        /// <summary>
        /// 验证节点配置
        /// 符合开闭原则：子类可以重写扩展验证逻辑
        /// </summary>
        public virtual ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                errors.Add("节点ID不能为空");

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("节点名称不能为空");

            if (string.IsNullOrWhiteSpace(NodeType))
                errors.Add("节点类型不能为空");

            // 验证端口
            foreach (var port in GetAllPorts())
            {
                if (string.IsNullOrWhiteSpace(port.Name))
                    errors.Add($"端口名称不能为空");
                
                if (port.NodeId != this.Id)
                    errors.Add($"端口 {port.Name} 的所属节点ID不匹配");
            }

            return errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }

        /// <summary>
        /// 克隆节点（默认实现：基于 System.Text.Json 的序列化/反序列化深拷贝）
        /// 符合开闭原则：提供默认实现，子类可重写定制
        /// </summary>
        public virtual Node Clone()
        {
            // 保存只读结构体属性（JSON 反序列化无法设置只读属性）
            var originalPosition = this.Position;
            var originalSize = this.Size;
            
            var json = JsonSerializer.Serialize(this, GetType(), jsonCloneOptions);
            var cloned = (Node)JsonSerializer.Deserialize(json, GetType(), jsonCloneOptions);
            cloned.Id = Guid.NewGuid().ToString();
            
            // 手动恢复只读结构体属性
            cloned.Position = originalPosition;
            cloned.Size = originalSize;
            
            RebuildPortRelationships(cloned);
            
            AfterClone(cloned);
            return cloned;
        }

        /// <summary>
        /// 重建端口关系（克隆后的后处理）
        /// 符合单一职责原则：专门负责关系重建
        /// </summary>
        protected virtual void RebuildPortRelationships(Node cloned)
        {
            foreach (var port in cloned.InputPorts ?? new List<Port>())
            {
                port.Id = Guid.NewGuid().ToString();
                port.NodeId = cloned.Id;
                port.ParentNode = cloned;
                port.Connections?.Clear();
            }
            
            foreach (var port in cloned.OutputPorts ?? new List<Port>())
            {
                port.Id = Guid.NewGuid().ToString();
                port.NodeId = cloned.Id;
                port.ParentNode = cloned;
                port.Connections?.Clear();
            }
        }

        /// <summary>
        /// 克隆后钩子：子类可覆盖以执行深拷贝或修正引用
        /// 符合开闭原则：提供扩展点
        /// </summary>
        protected virtual void AfterClone(Node cloned)
        {
        }

        /// <summary>
        /// 保留，供子类在 JSON 克隆后进行统一后处理（如 ID 重映射、关系修补）。
        /// </summary>
        // 下面保留的类型克隆计划结构已不再被默认实现使用，若未来需要切换为"表达式委托 + 缓存"的方案，可恢复使用。

        // ===== 执行入口（通过扩展方法提供，在 Astra.Engine 中实现） =====
        // 注意：ExecuteAsync 方法已移至 Astra.Engine.Execution.NodeExecutor.NodeExecutionExtensions
        // 这样可以避免 Core 直接依赖 Engine 的实现
    }

    // ========================================
    // 第2层：节点执行器接口（接口定义保留在 Core，实现移至 Engine）
    // ========================================

    /// <summary>
    /// 节点执行器接口
    /// 符合单一职责：仅负责节点执行
    /// 符合依赖倒置：高层模块依赖抽象而非具体实现
    /// </summary>
    public interface INodeExecutor
    {
        Task<ExecutionResult> ExecuteAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken);

        INodeExecutor Use(INodeMiddleware middleware);

        INodeExecutor AddInterceptor(INodeInterceptor interceptor);
    }

    /// <summary>
    /// 节点中间件接口
    /// 符合单一职责：每个中间件处理一个横切关注点
    /// </summary>
    public interface INodeMiddleware
    {
        Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next);
    }

    /// <summary>
    /// 节点拦截器接口
    /// 符合接口隔离原则：客户端只依赖需要的方法
    /// </summary>
    public interface INodeInterceptor
    {
        Task OnBeforeExecuteAsync(Node node, NodeContext context, CancellationToken cancellationToken);

        Task OnAfterExecuteAsync(Node node, ExecutionResult result, CancellationToken cancellationToken);

        Task OnExceptionAsync(Node node, Exception exception, CancellationToken cancellationToken);
    }

    // 注意：所有实现类（DefaultNodeExecutor, 各种Middleware, 各种Interceptor等）
    // 已迁移至 Astra.Engine.Execution 命名空间
    // 请使用 using Astra.Engine.Execution.NodeExecutor;
    //      using Astra.Engine.Execution.Middleware;
    //      using Astra.Engine.Execution.Interceptors;
    // 来引用这些实现类

    // ========================================
    // 批量克隆扩展方法
    // ========================================

    /// <summary>
    /// Node 批量克隆扩展，支持批量复制、批量粘贴等场景
    /// </summary>
    public static class NodeCloneExtensions
    {
        /// <summary>
        /// 强类型克隆
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="node">要克隆的节点</param>
        /// <returns>克隆后的节点实例</returns>
        public static T CloneAs<T>(this T node) where T : Node
        {
            return (T)node.Clone();
        }

        /// <summary>
        /// 克隆并修改
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="node">要克隆的节点</param>
        /// <param name="configure">配置委托</param>
        /// <returns>克隆并修改后的节点实例</returns>
        public static T CloneWith<T>(this T node, Action<T> configure) where T : Node
        {
            var cloned = node.CloneAs<T>();
            configure(cloned);
            return cloned;
        }

        /// <summary>
        /// 批量克隆
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="nodes">要克隆的节点集合</param>
        /// <returns>克隆后的节点集合</returns>
        public static List<T> CloneAll<T>(this IEnumerable<T> nodes) where T : Node
        {
            return nodes.Select(n => n.CloneAs<T>()).ToList();
        }
    }

    // ========================================
    // 注意：所有执行相关的实现类已迁移至 Astra.Engine
    // ========================================
    // 以下类已迁移：
    // - DefaultNodeExecutor -> Astra.Engine.Execution.NodeExecutor.DefaultNodeExecutor
    // - NodeExecutorFactory -> Astra.Engine.Execution.NodeExecutor.NodeExecutorFactory
    // - LoggingMiddleware -> Astra.Engine.Execution.Middleware.LoggingMiddleware
    // - PerformanceMiddleware -> Astra.Engine.Execution.Middleware.PerformanceMiddleware
    // - RetryMiddleware -> Astra.Engine.Execution.Middleware.RetryMiddleware
    // - TimeoutMiddleware -> Astra.Engine.Execution.Middleware.TimeoutMiddleware
    // - CacheMiddleware -> Astra.Engine.Execution.Middleware.CacheMiddleware
    // - ValidationMiddleware -> Astra.Engine.Execution.Middleware.ValidationMiddleware
    // - ConditionalMiddleware -> Astra.Engine.Execution.Middleware.ConditionalMiddleware
    // - AuditInterceptor -> Astra.Engine.Execution.Interceptors.AuditInterceptor
    // - PermissionInterceptor -> Astra.Engine.Execution.Interceptors.PermissionInterceptor
    // 
    // 使用方式：
    // using Astra.Engine.Execution.NodeExecutor;  // 使用 NodeExecutionExtensions.ExecuteAsync()
    // using Astra.Engine.Execution.NodeExecutor;  // 使用 DefaultNodeExecutor, NodeExecutorFactory
    // using Astra.Engine.Execution.Middleware;    // 使用各种中间件
    // using Astra.Engine.Execution.Interceptors; // 使用各种拦截器
    // ========================================
}

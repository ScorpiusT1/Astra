using Astra.Core.Nodes.Geometry;
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
    /// </summary>
    public abstract class Node
    {
        // JSON 克隆选项：
        // - Preserve：保留引用，避免循环引用导致栈溢出，并尽量恢复共享引用关系
        // - 默认遵循 [JsonIgnore]
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
        }

        // ===== 基本属性 =====
        public string Id { get; set; }
        public string NodeType { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }

        // ===== 状态属性 =====
        public bool IsEnabled { get; set; }
        public bool IsReadonly { get; set; }
        public bool IsLocked { get; set; }

        // ===== 参数和结果 =====
        public Dictionary<string, object> Parameters { get; set; }

        [JsonIgnore]
        public ExecutionResult LastExecutionResult { get; set; }

        [JsonIgnore]
        public NodeExecutionState ExecutionState { get; set; }

        // ===== 布局属性 =====
        public Point2D Position { get; set; }
        public Size2D Size { get; set; }

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
        /// </summary>
        public virtual ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("节点名称不能为空");

            return errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }

        /// <summary>
        /// 克隆节点（默认实现：基于 System.Text.Json 的序列化/反序列化深拷贝）
        /// 实现原则：
        /// 1) 按运行时具体类型进行 JSON 序列化并反序列化，保留多态；
        /// 2) 使用 ReferenceHandler.Preserve 以处理循环引用；
        /// 3) 遵循 [JsonIgnore]，运行时属性不会被复制；
        /// 4) 反序列化后重新生成 Id；
        /// 5) 调用 AfterClone(cloned) 供子类执行 ID 重新分配与关系修补（如需）。
        /// </summary>
        public virtual Node Clone()
        {
            var json = JsonSerializer.Serialize(this, GetType(), jsonCloneOptions);
            var cloned = (Node)JsonSerializer.Deserialize(json, GetType(), jsonCloneOptions);
            cloned.Id = Guid.NewGuid().ToString();
            AfterClone(cloned);
            return cloned;
        }

        /// <summary>
        /// 克隆后钩子：子类可覆盖以执行深拷贝或修正引用
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
    /// 定义节点执行的抽象接口，具体实现位于 Astra.Engine.Execution.NodeExecutor
    /// </summary>
    public interface INodeExecutor
    {
        /// <summary>
        /// 执行节点
        /// </summary>
        /// <param name="node">要执行的节点</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        Task<ExecutionResult> ExecuteAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        /// <returns>当前执行器实例，支持链式调用</returns>
        INodeExecutor Use(INodeMiddleware middleware);

        /// <summary>
        /// 添加拦截器
        /// </summary>
        /// <param name="interceptor">拦截器实例</param>
        /// <returns>当前执行器实例，支持链式调用</returns>
        INodeExecutor AddInterceptor(INodeInterceptor interceptor);
    }

    /// <summary>
    /// 节点中间件接口
    /// 定义中间件的抽象接口，具体实现位于 Astra.Engine.Execution.Middleware
    /// </summary>
    public interface INodeMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        /// <param name="node">要执行的节点</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="next">下一个中间件或核心执行逻辑</param>
        /// <returns>执行结果</returns>
        Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next);
    }

    /// <summary>
    /// 节点拦截器接口
    /// 定义拦截器的抽象接口，具体实现位于 Astra.Engine.Execution.Interceptors
    /// </summary>
    public interface INodeInterceptor
    {
        /// <summary>
        /// 节点执行前调用
        /// </summary>
        /// <param name="node">要执行的节点</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task OnBeforeExecuteAsync(Node node, NodeContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 节点执行后调用
        /// </summary>
        /// <param name="node">已执行的节点</param>
        /// <param name="result">执行结果</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task OnAfterExecuteAsync(Node node, ExecutionResult result, CancellationToken cancellationToken);

        /// <summary>
        /// 节点执行异常时调用
        /// </summary>
        /// <param name="node">执行异常的节点</param>
        /// <param name="exception">异常对象</param>
        /// <param name="cancellationToken">取消令牌</param>
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

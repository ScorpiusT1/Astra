﻿namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点执行上下文
    /// 符合单一职责原则：专门负责传递执行时所需的上下文信息
    /// 符合开闭原则：通过Metadata支持扩展而不修改类本身
    /// </summary>
    public class NodeContext
    {
        public NodeContext()
        {
            InputData = new Dictionary<string, object>();
            GlobalVariables = new Dictionary<string, object>();
            Metadata = new Dictionary<string, object>();
            ExecutionId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 输入数据（从上游节点传递的数据）
        /// </summary>
        public Dictionary<string, object> InputData { get; set; }

        /// <summary>
        /// 全局变量（工作流级别共享的变量）
        /// </summary>
        public Dictionary<string, object> GlobalVariables { get; set; }

        /// <summary>
        /// 服务提供者（用于依赖注入）
        /// 符合依赖倒置原则：通过服务容器获取依赖
        /// </summary>
        public IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 父工作流引用（用于访问工作流级别的配置和变量）
        /// </summary>
        public WorkFlowNode ParentWorkFlow { get; set; }

        /// <summary>
        /// 执行ID（唯一标识一次执行）
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 元数据（用于扩展，存储自定义信息）
        /// 符合开闭原则：支持扩展而不修改
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        // ===== 类型安全的辅助方法（提高易用性） =====

        /// <summary>
        /// 获取输入数据（类型安全）
        /// </summary>
        public T GetInput<T>(string key, T defaultValue = default)
        {
            if (InputData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置输入数据
        /// </summary>
        public void SetInput<T>(string key, T value)
        {
            InputData[key] = value;
        }

        /// <summary>
        /// 获取全局变量（类型安全）
        /// </summary>
        public T GetGlobalVariable<T>(string key, T defaultValue = default)
        {
            if (GlobalVariables.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置全局变量
        /// </summary>
        public void SetGlobalVariable<T>(string key, T value)
        {
            GlobalVariables[key] = value;
        }

        /// <summary>
        /// 获取元数据（类型安全）
        /// </summary>
        public T GetMetadata<T>(string key, T defaultValue = default)
        {
            if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置元数据
        /// </summary>
        public void SetMetadata<T>(string key, T value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// 克隆上下文（用于子工作流或并行执行）
        /// </summary>
        public NodeContext Clone()
        {
            return new NodeContext
            {
                InputData = new Dictionary<string, object>(InputData),
                GlobalVariables = GlobalVariables, // 共享全局变量
                ServiceProvider = ServiceProvider,
                ParentWorkFlow = ParentWorkFlow,
                ExecutionId = Guid.NewGuid().ToString(),
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        /// <summary>
        /// 创建子上下文（继承全局变量和服务提供者）
        /// </summary>
        public NodeContext CreateChildContext()
        {
            return new NodeContext
            {
                InputData = new Dictionary<string, object>(),
                GlobalVariables = GlobalVariables, // 共享全局变量
                ServiceProvider = ServiceProvider,
                ParentWorkFlow = ParentWorkFlow,
                ExecutionId = Guid.NewGuid().ToString(),
                Metadata = new Dictionary<string, object>()
            };
        }
    }
}

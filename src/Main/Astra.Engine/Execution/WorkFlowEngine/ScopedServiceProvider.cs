using System;
using System.Collections.Generic;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 轻量级作用域 ServiceProvider
    /// 用于在工作流范围内注入 Logger 等服务，支持作用域级别的服务覆盖
    /// </summary>
    internal class ScopedServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _fallback;
        private readonly Dictionary<Type, object> _scoped = new Dictionary<Type, object>();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="fallback">后备服务提供者，当作用域内找不到服务时使用</param>
        public ScopedServiceProvider(IServiceProvider fallback)
        {
            _fallback = fallback;
        }

        /// <summary>
        /// 添加作用域服务
        /// </summary>
        /// <param name="type">服务类型</param>
        /// <param name="instance">服务实例</param>
        public void AddService(Type type, object instance)
        {
            _scoped[type] = instance;
        }

        /// <summary>
        /// 获取服务
        /// 优先从作用域内查找，如果找不到则从后备服务提供者查找
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务实例，如果未找到则返回null</returns>
        public object GetService(Type serviceType)
        {
            if (_scoped.TryGetValue(serviceType, out var obj))
                return obj;
            return _fallback?.GetService(serviceType);
        }
    }
}


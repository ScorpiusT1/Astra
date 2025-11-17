using System;
using System.Collections.Generic;

namespace Astra.Core.Plugins.Abstractions
{
    /// <summary>
    /// 依赖解析器实现（桥接到 ServiceRegistry）
    /// </summary>
    public class DependencyResolver : IDependencyResolver
    {
        private readonly IServiceRegistry _serviceRegistry;

        public DependencyResolver(IServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public object Resolve(Type serviceType)
        {
            return _serviceRegistry.Resolve(serviceType);
        }

        public T Resolve<T>() where T : class
        {
            return _serviceRegistry.Resolve<T>();
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            try
            {
                service = _serviceRegistry.Resolve<T>();
                return service != null;
            }
            catch
            {
                service = null;
                return false;
            }
        }

        public IEnumerable<object> ResolveAll(Type serviceType)
        {
            var method = typeof(IServiceRegistry).GetMethod("ResolveAll");
            var genericMethod = method.MakeGenericMethod(serviceType);
            return (IEnumerable<object>)genericMethod.Invoke(_serviceRegistry, null);
        }

        public bool IsRegistered(Type serviceType)
        {
            try
            {
                _serviceRegistry.Resolve(serviceType);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

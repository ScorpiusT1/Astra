using Astra.Core.Plugins.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Services
{
    /// <summary>
    /// 增强的服务注册表 - 支持高级特性
    /// </summary>
    public partial class ServiceRegistry : IServiceRegistry
    {
        private readonly ConcurrentDictionary<Type, List<ServiceDescriptor>> _services = new();
        private readonly ConcurrentDictionary<Type, object> _singletons = new();
        private readonly ConcurrentDictionary<string, object> _namedServices = new();
        private readonly ConcurrentDictionary<Type, List<Type>> _decorators = new();
        private readonly ConcurrentDictionary<Type, Func<object, object>> _postProcessors = new();
        private readonly ConcurrentDictionary<Type, (Type Service, Type Implementation)> _openGenerics = new();
        private static readonly System.Threading.AsyncLocal<ServiceScopeContext> _currentScope = new();

        #region 基础实现（保持原有代码）

        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            var descriptor = new ServiceDescriptor
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = ServiceLifetime.Singleton
            };
            AddDescriptor(descriptor);
        }

        public void RegisterSingleton<TService>(TService instance)
        {
            _singletons[typeof(TService)] = instance;
            var descriptor = new ServiceDescriptor
            {
                ServiceType = typeof(TService),
                Instance = instance,
                Lifetime = ServiceLifetime.Singleton
            };
            AddDescriptor(descriptor);
        }

        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            var descriptor = new ServiceDescriptor
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = ServiceLifetime.Transient
            };
            AddDescriptor(descriptor);
        }

        public void RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            var descriptor = new ServiceDescriptor
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = ServiceLifetime.Scoped
            };
            AddDescriptor(descriptor);
        }

        public TService Resolve<TService>()
        {
            return (TService)Resolve(typeof(TService));
        }

		/// <summary>
		/// 安全解析，不存在时返回默认值。
		/// </summary>
		public TService ResolveOrDefault<TService>()
		{
			try { return (TService)Resolve(typeof(TService)); } catch { return default; }
		}

        public object Resolve(Type serviceType)
        {
            // 1. 检查开放泛型
            if (serviceType.IsGenericType && !serviceType.IsGenericTypeDefinition)
            {
                var genericDef = serviceType.GetGenericTypeDefinition();
                if (_openGenerics.TryGetValue(genericDef, out var openGeneric))
                {
                    var closedImpl = openGeneric.Implementation.MakeGenericType(serviceType.GetGenericArguments());
                    return CreateInstance(closedImpl);
                }
            }

            // 2. 正常解析
            if (!_services.TryGetValue(serviceType, out var descriptors) || descriptors.Count == 0)
                throw new InvalidOperationException($"Service not registered: {serviceType}");

            var descriptor = descriptors[^1];
            object instance;

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                instance = _singletons.GetOrAdd(serviceType, _ => CreateInstance(descriptor));
            }
            else
            {
                instance = ResolveNonSingleton(serviceType, descriptor);
            }

            // 3. 应用装饰器
            instance = ApplyDecorators(serviceType, instance);

            // 4. 应用后置处理器
            if (_postProcessors.TryGetValue(serviceType, out var processor))
            {
                instance = processor(instance);
            }

            return instance;
        }

        public IEnumerable<TService> ResolveAll<TService>()
        {
            var serviceType = typeof(TService);
            if (!_services.TryGetValue(serviceType, out var descriptors))
                yield break;

            foreach (var descriptor in descriptors)
            {
                yield return (TService)CreateInstance(descriptor);
            }
        }

        public void Unregister<TService>()
        {
            var serviceType = typeof(TService);
            _services.TryRemove(serviceType, out _);
            _singletons.TryRemove(serviceType, out _);
        }

        public void UnregisterByType(Type serviceType)
        {
            _services.TryRemove(serviceType, out _);
            _singletons.TryRemove(serviceType, out _);
        }

        #endregion

        #region 高级特性实现

        /// <summary>
        /// 注册装饰器
        /// </summary>
        public void AddDecorator<TService, TDecorator>() where TDecorator : TService
        {
            var serviceType = typeof(TService);
            var decorators = _decorators.GetOrAdd(serviceType, _ => new List<Type>());
            lock (decorators)
            {
                decorators.Add(typeof(TDecorator));
            }
        }

        /// <summary>
        /// 添加后置处理器
        /// </summary>
        public void AddPostProcessor<TService>(Func<TService, TService> processor)
        {
            _postProcessors[typeof(TService)] = obj => processor((TService)obj);
        }

        /// <summary>
        /// 注册命名服务
        /// </summary>
        public void RegisterNamed<TService>(string name, TService instance)
        {
            _namedServices[$"{typeof(TService).FullName}:{name}"] = instance;
        }

        /// <summary>
        /// 解析命名服务
        /// </summary>
        public TService ResolveNamed<TService>(string name)
        {
            var key = $"{typeof(TService).FullName}:{name}";
            return _namedServices.TryGetValue(key, out var instance)
                ? (TService)instance
                : default;
        }

        /// <summary>
        /// 注册开放泛型
        /// </summary>
        public void RegisterOpenGeneric(
            Type openGenericServiceType,
            Type openGenericImplementationType,
            ServiceLifetime lifetime)
        {
            _openGenerics[openGenericServiceType] = (openGenericServiceType, openGenericImplementationType);
        }

        /// <summary>
        /// 注册单例服务（工厂方法）
        /// </summary>
        public void RegisterSingleton<TService>(Func<TService> factory)
        {
            var instance = factory();
            RegisterSingleton<TService>(instance);
        }

        /// <summary>
        /// 注册单例服务（工厂方法）
        /// </summary>
        public void RegisterSingleton<TService>(Func<IServiceRegistry, TService> factory)
        {
            var instance = factory(this);
            RegisterSingleton<TService>(instance);
        }

        /// <summary>
        /// 带元数据注册
        /// </summary>
        public void RegisterWithMetadata<TService, TImplementation>(
            ServiceLifetime lifetime,
            Dictionary<string, object> metadata)
            where TImplementation : TService
        {
            var descriptor = new ServiceDescriptor
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = lifetime,
                Metadata = metadata
            };
            AddDescriptor(descriptor);
        }

        /// <summary>
        /// 根据元数据查询服务
        /// </summary>
        public IEnumerable<Type> GetServicesByMetadata(string key, object value)
        {
            return _services
                .Where(kvp => kvp.Value.Any(d =>
                    d.Metadata != null &&
                    d.Metadata.TryGetValue(key, out var v) &&
                    Equals(v, value)))
                .Select(kvp => kvp.Key);
        }

        #endregion

        #region 私有辅助方法

        private void AddDescriptor(ServiceDescriptor descriptor)
        {
            var descriptors = _services.GetOrAdd(descriptor.ServiceType, _ => new List<ServiceDescriptor>());
            lock (descriptors)
            {
                descriptors.Add(descriptor);
            }
        }

        private object CreateInstance(ServiceDescriptor descriptor)
        {
            if (descriptor.Instance != null)
                return descriptor.Instance;

            return CreateInstance(descriptor.ImplementationType);
        }

        private object CreateInstance(Type implementationType)
        {
            var constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
                return TrackAndReturn(Activator.CreateInstance(implementationType));

            var constructor = constructors
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                try
                {
                    args[i] = Resolve(parameters[i].ParameterType);
                }
                catch
                {
                    if (parameters[i].HasDefaultValue)
                        args[i] = parameters[i].DefaultValue;
                    else
                        throw;
                }
            }

            return TrackAndReturn(Activator.CreateInstance(implementationType, args));
        }

        private object ApplyDecorators(Type serviceType, object instance)
        {
            if (!_decorators.TryGetValue(serviceType, out var decoratorTypes))
                return instance;

            foreach (var decoratorType in decoratorTypes)
            {
                var constructor = decoratorType.GetConstructors()[0];
                var parameters = constructor.GetParameters();
                var args = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == serviceType)
                    {
                        args[i] = instance;
                    }
                    else
                    {
                        args[i] = Resolve(parameters[i].ParameterType);
                    }
                }

                instance = TrackAndReturn(Activator.CreateInstance(decoratorType, args));
            }

            return instance;
        }

        private object ResolveNonSingleton(Type serviceType, ServiceDescriptor descriptor)
        {
            if (descriptor.Lifetime == ServiceLifetime.Scoped && _currentScope.Value != null)
            {
                var scope = _currentScope.Value;
                if (!scope.ScopedInstances.TryGetValue(serviceType, out var inst))
                {
                    inst = CreateInstance(descriptor);
                    scope.ScopedInstances[serviceType] = inst;
                }
                return inst;
            }

            // Transient 或无作用域时，直接创建
            return CreateInstance(descriptor);
        }

        private object TrackAndReturn(object instance)
        {
            if (instance is IDisposable d && _currentScope.Value != null)
            {
                _currentScope.Value.Disposables.Add(d);
            }
            return instance;
        }

        /// <summary>
        /// 开启一个服务作用域。返回的 IDisposable 用于结束作用域并释放作用域内创建的 IDisposable 实例。
        /// 注意：该方法为具体实现扩展，未加入接口以避免破坏兼容。
        /// </summary>
        public IDisposable BeginScope()
        {
            var previous = _currentScope.Value;
            var ctx = new ServiceScopeContext(previous);
            _currentScope.Value = ctx;
            return new ScopeCloser(() =>
            {
                try
                {
                    for (int i = ctx.Disposables.Count - 1; i >= 0; i--)
                    {
                        try { ctx.Disposables[i].Dispose(); } catch { }
                    }
                    ctx.ScopedInstances.Clear();
                }
                finally
                {
                    _currentScope.Value = previous;
                }
            });
        }

        private sealed class ScopeCloser : IDisposable
        {
            private readonly Action _close;
            private bool _disposed;
            public ScopeCloser(Action close) { _close = close; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _close();
            }
        }

        private sealed class ServiceScopeContext
        {
            public readonly Dictionary<Type, object> ScopedInstances = new();
            public readonly List<IDisposable> Disposables = new();
            public readonly ServiceScopeContext Parent;
            public ServiceScopeContext(ServiceScopeContext parent) { Parent = parent; }
        }
        #endregion
    }
}

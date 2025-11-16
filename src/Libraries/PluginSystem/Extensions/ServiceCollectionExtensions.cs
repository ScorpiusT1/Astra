using Addins.Core.Abstractions;
using Addins.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Extensions
{
    /// <summary>
    /// 服务注册扩展方法 - 提供流畅的 API
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        #region 基础注册扩展

        /// <summary>
        /// 注册单例服务（自动推断实现类型）
        /// </summary>
        public static IServiceRegistry AddSingleton<TService>(this IServiceRegistry services)
            where TService : class
        {
            services.RegisterSingleton<TService, TService>();
            return services;
        }

        /// <summary>
        /// 注册单例服务（使用工厂方法）
        /// </summary>
        public static IServiceRegistry AddSingleton<TService>(
            this IServiceRegistry services,
            Func<IServiceRegistry, TService> factory) where TService : class
        {
            var instance = factory(services);
            services.RegisterSingleton(instance);
            return services;
        }

        /// <summary>
        /// 注册瞬时服务（自动推断实现类型）
        /// </summary>
        public static IServiceRegistry AddTransient<TService>(this IServiceRegistry services)
            where TService : class
        {
            services.RegisterTransient<TService, TService>();
            return services;
        }

        /// <summary>
        /// 注册瞬时服务
        /// </summary>
        public static IServiceRegistry AddTransient<TService, TImplementation>(
            this IServiceRegistry services)
            where TImplementation : TService
        {
            services.RegisterTransient<TService, TImplementation>();
            return services;
        }

        /// <summary>
        /// 注册作用域服务（自动推断实现类型）
        /// </summary>
        public static IServiceRegistry AddScoped<TService>(this IServiceRegistry services)
            where TService : class
        {
            services.RegisterScoped<TService, TService>();
            return services;
        }

        /// <summary>
        /// 注册作用域服务
        /// </summary>
        public static IServiceRegistry AddScoped<TService, TImplementation>(
            this IServiceRegistry services)
            where TImplementation : TService
        {
            services.RegisterScoped<TService, TImplementation>();
            return services;
        }

        #endregion

        #region 条件注册

        /// <summary>
        /// 仅当服务未注册时才注册
        /// </summary>
        public static IServiceRegistry TryAddSingleton<TService, TImplementation>(
            this IServiceRegistry services)
            where TImplementation : TService
        {
            if (!services.IsRegistered<TService>())
            {
                services.RegisterSingleton<TService, TImplementation>();
            }
            return services;
        }

        /// <summary>
        /// 仅当服务未注册时才注册（瞬时）
        /// </summary>
        public static IServiceRegistry TryAddTransient<TService, TImplementation>(
            this IServiceRegistry services)
            where TImplementation : TService
        {
            if (!services.IsRegistered<TService>())
            {
                services.RegisterTransient<TService, TImplementation>();
            }
            return services;
        }

        /// <summary>
        /// 仅当服务未注册时才注册（作用域）
        /// </summary>
        public static IServiceRegistry TryAddScoped<TService, TImplementation>(
            this IServiceRegistry services)
            where TImplementation : TService
        {
            if (!services.IsRegistered<TService>())
            {
                services.RegisterScoped<TService, TImplementation>();
            }
            return services;
        }

        #endregion

        #region 批量注册

        /// <summary>
        /// 注册多个实现到同一接口
        /// </summary>
        public static IServiceRegistry AddMany<TService>(
            this IServiceRegistry services,
            ServiceLifetime lifetime,
            params Type[] implementations)
        {
            foreach (var impl in implementations)
            {
                if (!typeof(TService).IsAssignableFrom(impl))
                    throw new ArgumentException($"{impl} does not implement {typeof(TService)}");

                services.Register(typeof(TService), impl, lifetime);
            }
            return services;
        }

        /// <summary>
        /// 注册多个单例实现
        /// </summary>
        public static IServiceRegistry AddManySingleton<TService>(
            this IServiceRegistry services,
            params Type[] implementations)
        {
            return services.AddMany<TService>(ServiceLifetime.Singleton, implementations);
        }

        /// <summary>
        /// 注册一个实现到多个接口
        /// </summary>
        public static IServiceRegistry AddToMany(
            this IServiceRegistry services,
            Type implementation,
            ServiceLifetime lifetime,
            params Type[] serviceTypes)
        {
            foreach (var serviceType in serviceTypes)
            {
                if (!serviceType.IsAssignableFrom(implementation))
                    throw new ArgumentException($"{implementation} does not implement {serviceType}");

                services.Register(serviceType, implementation, lifetime);
            }
            return services;
        }

        #endregion

        #region 程序集扫描注册

        /// <summary>
        /// 扫描程序集自动注册服务（通过特性标记）
        /// </summary>
        public static IServiceRegistry AddServicesFromAssembly(
            this IServiceRegistry services,
            Assembly assembly,
            Func<Type, bool> filter = null)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetCustomAttribute<ServiceAttribute>() != null);

            if (filter != null)
            {
                types = types.Where(filter);
            }

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<ServiceAttribute>();
                var serviceType = attr.ServiceType ?? type.GetInterfaces().FirstOrDefault() ?? type;

                services.Register(serviceType, type, attr.Lifetime);
            }

            return services;
        }

        /// <summary>
        /// 扫描当前程序集注册所有标记的服务
        /// </summary>
        public static IServiceRegistry AddServicesFromCallingAssembly(
            this IServiceRegistry services)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            return services.AddServicesFromAssembly(callingAssembly);
        }

        /// <summary>
        /// 按接口约定自动注册（I{Name} -> {Name}）
        /// </summary>
        public static IServiceRegistry AddServicesByConvention(
            this IServiceRegistry services,
            Assembly assembly,
            ServiceLifetime defaultLifetime = ServiceLifetime.Transient)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract);

            foreach (var type in types)
            {
                var interfaceType = type.GetInterface($"I{type.Name}");
                if (interfaceType != null)
                {
                    services.Register(interfaceType, type, defaultLifetime);
                }
            }

            return services;
        }

        /// <summary>
        /// 注册所有实现了指定接口的类型
        /// </summary>
        public static IServiceRegistry AddAllImplementations<TService>(
            this IServiceRegistry services,
            Assembly assembly,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            var implementations = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => typeof(TService).IsAssignableFrom(t));

            foreach (var impl in implementations)
            {
                services.Register(typeof(TService), impl, lifetime);
            }

            return services;
        }

        #endregion

        #region 装饰器模式

        /// <summary>
        /// 注册装饰器
        /// </summary>
        public static IServiceRegistry Decorate<TService, TDecorator>(
            this IServiceRegistry services)
            where TDecorator : TService
        {
            var registry = services as ServiceRegistry;
            if (registry == null)
                throw new NotSupportedException("Decorator pattern requires ServiceRegistry");

            registry.AddDecorator<TService, TDecorator>();
            return services;
        }

        #endregion

        #region 链式配置

        /// <summary>
        /// 配置已注册的服务
        /// </summary>
        public static IServiceRegistry Configure<TService>(
            this IServiceRegistry services,
            Action<TService> configure) where TService : class
        {
            var instance = services.Resolve<TService>();
            configure(instance);
            return services;
        }

        /// <summary>
        /// 后置处理器
        /// </summary>
        public static IServiceRegistry PostProcess<TService>(
            this IServiceRegistry services,
            Func<TService, TService> processor) where TService : class
        {
            var registry = services as ServiceRegistry;
            registry?.AddPostProcessor(processor);
            return services;
        }

        #endregion

        #region 命名服务

        /// <summary>
        /// 注册命名服务
        /// </summary>
        public static IServiceRegistry AddSingleton<TService>(
            this IServiceRegistry services,
            string name,
            TService instance) where TService : class
        {
            var registry = services as ServiceRegistry;
            registry?.RegisterNamed(name, instance);
            return services;
        }

        /// <summary>
        /// 解析命名服务
        /// </summary>
        public static TService ResolveNamed<TService>(
            this IServiceRegistry services,
            string name) where TService : class
        {
            var registry = services as ServiceRegistry;
            return registry?.ResolveNamed<TService>(name);
        }

        #endregion

        #region 泛型注册

        /// <summary>
        /// 注册开放泛型类型
        /// </summary>
        public static IServiceRegistry AddOpenGeneric(
            this IServiceRegistry services,
            Type openGenericServiceType,
            Type openGenericImplementationType,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            if (!openGenericServiceType.IsGenericTypeDefinition)
                throw new ArgumentException("Service type must be an open generic type");

            if (!openGenericImplementationType.IsGenericTypeDefinition)
                throw new ArgumentException("Implementation type must be an open generic type");

            var registry = services as ServiceRegistry;
            registry?.RegisterOpenGeneric(openGenericServiceType, openGenericImplementationType, lifetime);

            return services;
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public static bool IsRegistered<TService>(this IServiceRegistry services)
        {
            try
            {
                services.Resolve<TService>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 移除服务
        /// </summary>
        public static IServiceRegistry Remove<TService>(this IServiceRegistry services)
        {
            services.Unregister<TService>();
            return services;
        }

        /// <summary>
        /// 替换服务
        /// </summary>
        public static IServiceRegistry Replace<TService, TImplementation>(
            this IServiceRegistry services,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TImplementation : TService
        {
            services.Unregister<TService>();
            services.Register(typeof(TService), typeof(TImplementation), lifetime);
            return services;
        }

        #endregion

        #region 插件专用扩展

        /// <summary>
        /// 注册插件服务（自动追踪插件来源）
        /// </summary>
        public static IServiceRegistry AddPluginService<TService, TImplementation>(
            this IServiceRegistry services,
            string pluginId,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TImplementation : TService
        {
            var registry = services as ServiceRegistry;
            registry?.RegisterWithMetadata<TService, TImplementation>(lifetime, new Dictionary<string, object>
            {
                ["PluginId"] = pluginId,
                ["RegisteredAt"] = DateTime.UtcNow
            });

            return services;
        }

        /// <summary>
        /// 获取插件注册的所有服务
        /// </summary>
        public static IEnumerable<Type> GetPluginServices(
            this IServiceRegistry services,
            string pluginId)
        {
            var registry = services as ServiceRegistry;
            return registry?.GetServicesByMetadata("PluginId", pluginId) ?? Enumerable.Empty<Type>();
        }

        /// <summary>
        /// 注销插件的所有服务
        /// </summary>
        public static IServiceRegistry UnregisterPluginServices(
            this IServiceRegistry services,
            string pluginId)
        {
            var registry = services as ServiceRegistry;
            var serviceTypes = registry?.GetServicesByMetadata("PluginId", pluginId);

            if (serviceTypes != null)
            {
                foreach (var serviceType in serviceTypes.ToList())
                {
                    registry.UnregisterByType(serviceType);
                }
            }

            return services;
        }

        #endregion

        /// <summary>
        /// 内部辅助方法：注册服务
        /// </summary>
        private static void Register(
            this IServiceRegistry services,
            Type serviceType,
            Type implementationType,
            ServiceLifetime lifetime)
        {
            var method = typeof(IServiceRegistry).GetMethods()
                .FirstOrDefault(m => m.Name == $"Register{lifetime}" && m.IsGenericMethodDefinition);

            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(serviceType, implementationType);
                genericMethod.Invoke(services, null);
            }
        }
    }
}

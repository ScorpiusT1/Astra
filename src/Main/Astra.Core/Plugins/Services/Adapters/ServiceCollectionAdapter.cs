using Astra.Core.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Plugins.Services.Adapters
{
	/// <summary>
	/// 将 Microsoft.Extensions.DependencyInjection 的 IServiceCollection/IServiceProvider
	/// 适配为本系统的 IServiceRegistry，以便无缝接入标准 .NET 生态（Logging/Options/HostedService 等）。
	/// 
	/// ⭐ 改进：支持接受已构建的 IServiceProvider，确保单例服务在应用级别共享
	/// 
	/// 说明：
	/// - 基础的 Singleton/Scoped/Transient 注册完全映射；
	/// - Resolve/ResolveAll 使用 ServiceProvider 获取；
	/// - 如果提供了 externalServiceProvider，将优先使用它（确保单例共享）；
	/// - Decorator/Metadata/Named 等高级特性在 Microsoft DI 中无原生等价，提供受限实现或抛出不支持；
	/// </summary>
	public class ServiceCollectionAdapter : IServiceRegistry, IDisposable
	{
		private readonly IServiceCollection _services;
		private readonly IServiceProvider _externalServiceProvider;
		private ServiceProvider _internalServiceProvider;
		private readonly Dictionary<string, object> _named = new();
		private bool _ownsServiceProvider = false;

		/// <summary>
		/// 使用 IServiceCollection 创建适配器（会构建自己的 ServiceProvider）
		/// </summary>
		public ServiceCollectionAdapter(IServiceCollection services)
		{
			_services = services ?? throw new ArgumentNullException(nameof(services));
			_externalServiceProvider = null;
			_ownsServiceProvider = true;
			RebuildProvider();
		}

		/// <summary>
		/// 使用已构建的 IServiceProvider 创建适配器（共享单例服务）
		/// ⭐ 推荐使用此构造函数，确保插件系统和主应用共享同一个 ServiceProvider 实例
		/// </summary>
		public ServiceCollectionAdapter(IServiceCollection services, IServiceProvider externalServiceProvider)
		{
			_services = services ?? throw new ArgumentNullException(nameof(services));
			_externalServiceProvider = externalServiceProvider ?? throw new ArgumentNullException(nameof(externalServiceProvider));
			_ownsServiceProvider = false;
		}

		/// <summary>
		/// 获取当前使用的 ServiceProvider（优先使用外部提供的）
		/// </summary>
		private IServiceProvider CurrentServiceProvider => _externalServiceProvider ?? _internalServiceProvider;

		private void RebuildProvider()
		{
			// 如果使用外部 ServiceProvider，不需要重建
			if (_externalServiceProvider != null)
			{
				return;
			}

			_internalServiceProvider?.Dispose();
			_internalServiceProvider = _services.BuildServiceProvider();
		}

		public void RegisterSingleton<TService, TImplementation>() where TImplementation : TService
		{
			_services.AddSingleton(typeof(TService), typeof(TImplementation));
			// ⚠️ 如果使用外部 ServiceProvider，注册新服务后需要重新构建
			// 但为了保持兼容性，这里仍然调用 RebuildProvider（对于外部 ServiceProvider 是 no-op）
			RebuildProvider();
		}

		public void RegisterSingleton<TService>(TService instance)
		{
			_services.AddSingleton(typeof(TService), instance);
			RebuildProvider();
		}

		public void RegisterTransient<TService, TImplementation>() where TImplementation : TService
		{
			_services.AddTransient(typeof(TService), typeof(TImplementation));
			RebuildProvider();
		}

		public void RegisterScoped<TService, TImplementation>() where TImplementation : TService
		{
			_services.AddScoped(typeof(TService), typeof(TImplementation));
			RebuildProvider();
		}

		public object Resolve(Type serviceType)
		{
			return CurrentServiceProvider.GetRequiredService(serviceType);
		}

		public TService Resolve<TService>()
		{
			return CurrentServiceProvider.GetRequiredService<TService>();
		}

		public IEnumerable<TService> ResolveAll<TService>()
		{
			return CurrentServiceProvider.GetServices<TService>();
		}

		public void Unregister<TService>()
		{
			// Microsoft DI 不支持移除已注册服务；适配器提供 no-op 以保持接口一致性。
			// 如需移除，请在外部重建 IServiceCollection。
		}

		public void UnregisterByType(Type serviceType)
		{
			// 同上，不支持运行时移除；no-op。
		}

		public void AddDecorator<TService, TDecorator>() where TDecorator : TService
		{
			// Microsoft DI 无原生 Decorator。若强需支持，可通过 Scrutor 等第三方库。
			// 这里先抛出不支持，防止误用导致行为不一致。
			throw new NotSupportedException("Decorator is not supported in ServiceCollectionAdapter. Consider using Scrutor.");
		}

		public void AddPostProcessor<TService>(Func<TService, TService> processor)
		{
			// 无直接等价。可通过工厂/代理模式在调用侧完成。此处不支持。
			throw new NotSupportedException("PostProcessor is not supported in ServiceCollectionAdapter.");
		}

		public IEnumerable<Type> GetServicesByMetadata(string key, object value)
		{
			// Microsoft DI 无元数据模型；返回空集合以保持兼容。
			return Enumerable.Empty<Type>();
		}

		public void RegisterNamed<TService>(string name, TService instance)
		{
			_named[$"{typeof(TService).FullName}:{name}"] = instance;
		}

		public TService ResolveNamed<TService>(string name)
		{
			var key = $"{typeof(TService).FullName}:{name}";
			return _named.TryGetValue(key, out var obj) ? (TService)obj : default;
		}

		public void RegisterOpenGeneric(Type openGenericServiceType, Type openGenericImplementationType, ServiceLifetime lifetime)
		{
			switch (lifetime)
			{
				case ServiceLifetime.Singleton:
					_services.AddSingleton(openGenericServiceType, openGenericImplementationType);
					break;
				case ServiceLifetime.Scoped:
					_services.AddScoped(openGenericServiceType, openGenericImplementationType);
					break;
				case ServiceLifetime.Transient:
					_services.AddTransient(openGenericServiceType, openGenericImplementationType);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
			}
			RebuildProvider();
		}

		public void RegisterWithMetadata<TService, TImplementation>(ServiceLifetime lifetime, Dictionary<string, object> metadata) where TImplementation : TService
		{
			// 无元数据模型，降级为普通注册
			switch (lifetime)
			{
				case ServiceLifetime.Singleton:
					_services.AddSingleton(typeof(TService), typeof(TImplementation));
					break;
				case ServiceLifetime.Scoped:
					_services.AddScoped(typeof(TService), typeof(TImplementation));
					break;
				case ServiceLifetime.Transient:
					_services.AddTransient(typeof(TService), typeof(TImplementation));
					break;
			}
			RebuildProvider();
		}

		public void Dispose()
		{
			// 只释放自己创建的 ServiceProvider，不释放外部提供的
			if (_ownsServiceProvider)
			{
				_internalServiceProvider?.Dispose();
			}
		}
	}
}


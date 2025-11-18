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
		private IServiceProvider _externalServiceProvider; // ⭐ 改为非 readonly，支持后续更新
		private ServiceProvider _internalServiceProvider;
		private readonly Dictionary<string, object> _named = new();
		private bool _ownsServiceProvider = false;
		private bool _needsRebuild = false; // 标记是否需要重建 ServiceProvider

		/// <summary>
		/// 使用 IServiceCollection 创建适配器（延迟构建 ServiceProvider，只在需要时构建）
		/// ⭐ 优化：不在构造函数中立即构建，避免不必要的重复构建
		/// </summary>
		public ServiceCollectionAdapter(IServiceCollection services)
		{
			_services = services ?? throw new ArgumentNullException(nameof(services));
			_externalServiceProvider = null;
			_ownsServiceProvider = true;
			_needsRebuild = true; // 标记需要构建，但不立即构建
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
		/// ⭐ 优化：延迟构建，只在第一次需要解析服务时才构建
		/// 
		/// 注意：此属性每次访问都会重新计算，优先返回 _externalServiceProvider。
		/// 当调用 SetExternalServiceProvider 后，下次访问此属性时会自动使用新的 ServiceProvider。
		/// </summary>
		private IServiceProvider CurrentServiceProvider
		{
			get
			{
				// ⭐ 优先使用外部 ServiceProvider（如果已设置）
				if (_externalServiceProvider != null)
				{
					return _externalServiceProvider;
				}

				// 延迟构建：只在第一次需要时才构建内部 ServiceProvider
				if (_internalServiceProvider == null || _needsRebuild)
				{
					RebuildProvider();
				}

				return _internalServiceProvider;
			}
		}

		/// <summary>
		/// 获取当前使用的 ServiceProvider（公共方法，供外部访问）
		/// ⭐ 用于确保插件系统和主应用共享同一个 ServiceProvider 实例
		/// </summary>
		public IServiceProvider GetServiceProvider()
		{
			return CurrentServiceProvider;
		}

		/// <summary>
		/// 设置外部 ServiceProvider（用于在主程序构建 ServiceProvider 后更新）
		/// ⭐ 这样可以让插件系统使用主程序构建好的全局 ServiceProvider，确保单例服务共享
		/// 
		/// 注意：设置后，CurrentServiceProvider 会自动使用新的 _externalServiceProvider，
		/// 因为 CurrentServiceProvider 的 getter 会优先检查 _externalServiceProvider
		/// </summary>
		/// <param name="externalServiceProvider">主程序构建的 ServiceProvider</param>
		public void SetExternalServiceProvider(IServiceProvider externalServiceProvider)
		{
			if (externalServiceProvider == null)
			{
				throw new ArgumentNullException(nameof(externalServiceProvider));
			}

			// ⭐ 延迟释放：不立即释放 _internalServiceProvider，避免阻塞 UI 线程
			// 如果之前有内部 ServiceProvider，标记为待释放（在后台线程中异步释放）
			if (_ownsServiceProvider && _internalServiceProvider != null)
			{
				// 将当前的 _internalServiceProvider 标记为待释放
				var providerToDispose = _internalServiceProvider;
				_internalServiceProvider = null;
				
				// ⭐ 在后台线程中异步释放，避免阻塞 UI 线程
				// 使用 Task.Run 确保释放操作不会阻塞当前线程
				System.Threading.Tasks.Task.Run(() =>
				{
					try
					{
						providerToDispose.Dispose();
					}
					catch
					{
						// 忽略释放过程中的异常，避免影响主流程
					}
				});
			}

			// ⭐ 设置外部 ServiceProvider 后，CurrentServiceProvider 会自动使用它
			// 因为 CurrentServiceProvider 的 getter 会优先检查 _externalServiceProvider
			_externalServiceProvider = externalServiceProvider;
			
            _ownsServiceProvider = false;
			_needsRebuild = false; // 使用外部 ServiceProvider，不需要重建
		}

		/// <summary>
		/// 重建 ServiceProvider（只在需要时调用）
		/// </summary>
		private void RebuildProvider()
		{
			// 如果使用外部 ServiceProvider，不需要重建
			if (_externalServiceProvider != null)
			{
				return;
			}

			_internalServiceProvider?.Dispose();
			_internalServiceProvider = _services.BuildServiceProvider();
			_needsRebuild = false;
		}

		public void RegisterSingleton<TService, TImplementation>() where TImplementation : TService
		{
			_services.AddSingleton(typeof(TService), typeof(TImplementation));
			// ⭐ 优化：只标记需要重建，不立即重建（延迟到第一次解析时）
			// 如果使用外部 ServiceProvider，标记无效（因为不会使用内部 ServiceProvider）
			if (_externalServiceProvider == null)
			{
				_needsRebuild = true;
			}
		}

		public void RegisterSingleton<TService>(TService instance)
		{
			_services.AddSingleton(typeof(TService), instance);
			if (_externalServiceProvider == null)
			{
				_needsRebuild = true;
			}
		}

		public void RegisterTransient<TService, TImplementation>() where TImplementation : TService
		{
			_services.AddTransient(typeof(TService), typeof(TImplementation));
			if (_externalServiceProvider == null)
			{
				_needsRebuild = true;
			}
		}

		public void RegisterScoped<TService, TImplementation>() where TImplementation : TService
		{
			_services.AddScoped(typeof(TService), typeof(TImplementation));
			if (_externalServiceProvider == null)
			{
				_needsRebuild = true;
			}
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
			if (_externalServiceProvider == null)
			{
				_needsRebuild = true;
			}
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
			if (_externalServiceProvider == null)
			{
				_needsRebuild = true;
			}
		}

		public void Dispose()
		{
			// 只释放自己创建的 ServiceProvider，不释放外部提供的
			if (_ownsServiceProvider && _internalServiceProvider != null)
			{
				var providerToDispose = _internalServiceProvider;
				_internalServiceProvider = null;
				
				// ⭐ 在后台线程中异步释放，避免阻塞 UI 线程
				System.Threading.Tasks.Task.Run(() =>
				{
					try
					{
						providerToDispose.Dispose();
					}
					catch
					{
						// 忽略释放过程中的异常
					}
				});
			}
		}
	}
}


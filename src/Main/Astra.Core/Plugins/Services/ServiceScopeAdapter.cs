using Microsoft.Extensions.DependencyInjection;
using System;

namespace Astra.Core.Plugins.Services
{
	/// <summary>
	/// 基于 ServiceRegistry 的简单 IServiceScopeFactory 适配实现。
	/// 通过 ServiceRegistry.BeginScope() 管理作用域内的释放。
	/// </summary>
	public sealed class RegistryServiceScopeFactory : IServiceScopeFactory
	{
		private readonly ServiceRegistry _registry;

		public RegistryServiceScopeFactory(ServiceRegistry registry)
		{
			_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		}

		public IServiceScope CreateScope()
		{
			var scopeHandle = _registry.BeginScope();
			return new RegistryScope(_registry, scopeHandle);
		}

		private sealed class RegistryScope : IServiceScope
		{
			private readonly IDisposable _scopeHandle;
			public IServiceProvider ServiceProvider { get; }

			public RegistryScope(ServiceRegistry registry, IDisposable scopeHandle)
			{
				_scopeHandle = scopeHandle;
				ServiceProvider = new RegistryServiceProvider(registry);
			}

			public void Dispose()
			{
				_scopeHandle.Dispose();
			}
		}

		private sealed class RegistryServiceProvider : IServiceProvider
		{
			private readonly ServiceRegistry _registry;

			public RegistryServiceProvider(ServiceRegistry registry)
			{
				_registry = registry;
			}

			public object GetService(Type serviceType)
			{
				try
				{
					return _registry.Resolve(serviceType);
				}
				catch
				{
					return null;
				}
			}
		}
	}
}


using System;
using Astra.Core.Plugins.Abstractions;

namespace Astra.Core.Plugins.Services
{
	/// <summary>
	/// 为 IServiceRegistry 提供安全解析扩展。
	/// </summary>
	/// <summary>
	/// <see cref="IServiceRegistry"/> 的安全解析扩展方法。
	/// </summary>
	public static class IServiceRegistryExtensions
	{
		/// <summary>
		/// 尝试解析服务实例，失败时返回默认值（null）。
		/// </summary>
		public static T TryResolve<T>(this IServiceRegistry services)
		{
			try { return services.Resolve<T>(); } catch { return default; }
		}

		/// <summary>
		/// 语义同 <see cref="TryResolve{T}(IServiceRegistry)"/>。
		/// </summary>
		public static T ResolveOrDefault<T>(this IServiceRegistry services)
		{
			try { return services.Resolve<T>(); } catch { return default; }
		}
	}
}


using System;
using Astra.Core.Plugins.Abstractions;

namespace Astra.Core.Plugins.Services
{
	/// <summary>
	/// 极简服务定位器：用于在非 DI 上下文（静态方法/文件系统回调等）中，
	/// 尝试获取已注册的服务实例（可选）。
	/// 仅用于边缘场景，不建议在业务代码中滥用。
	/// </summary>
	public static class ServiceLocator
	{
		private static IServiceRegistry _registry;

		public static void Initialize(IServiceRegistry registry) => _registry = registry;

		/// <summary>
		/// 尝试解析服务，不存在时返回默认值（null）。
		/// </summary>
		public static T ResolveOrDefault<T>()
		{
			try { return _registry is ServiceRegistry r ? r.ResolveOrDefault<T>() : default; }
			catch { return default; }
		}
	}
}


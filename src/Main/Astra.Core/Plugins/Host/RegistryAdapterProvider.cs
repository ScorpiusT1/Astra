using Astra.Core.Plugins.Abstractions;

namespace Astra.Core.Plugins.Host
{
public partial class PluginHost
    {
        private sealed class RegistryAdapterProvider : IServiceProvider
		{
			private readonly IServiceRegistry _registry;
			/// <summary>
			/// 使用 <see cref="IServiceRegistry"/> 作为后端实现的 <see cref="IServiceProvider"/> 适配器，
			/// 便于插件按标准方式请求依赖。
			/// </summary>
			public RegistryAdapterProvider(IServiceRegistry registry) { _registry = registry; }
			/// <summary>
			/// 解析服务，若未注册则返回 null。
			/// </summary>
			public object GetService(Type serviceType)
			{
				try { return _registry.Resolve(serviceType); } catch { return null; }
			}
		}
    }
}

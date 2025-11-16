using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Astra.Core.Plugins.Loading
{
	/// <summary>
	/// 可回收的插件专用 <see cref="AssemblyLoadContext"/>。
	/// 使用 <see cref="AssemblyDependencyResolver"/> 将插件目录下的依赖解析为绝对路径，
	/// 并开启 <c>isCollectible</c> 以支持完全卸载（配合 <see cref="AssemblyLoadContext.Unload"/> 和 GC）。
	/// </summary>
	public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
	{
		private readonly AssemblyDependencyResolver _resolver;

		/// <summary>
		/// 该加载上下文对应的插件物理目录，主要用于日志与调试。
		/// </summary>
		public string PluginDirectory { get; }

		public PluginAssemblyLoadContext(string pluginAssemblyPath, bool isCollectible = true)
			: base(isCollectible: isCollectible)
		{
			if (string.IsNullOrEmpty(pluginAssemblyPath)) throw new ArgumentNullException(nameof(pluginAssemblyPath));
			PluginDirectory = Path.GetDirectoryName(Path.GetFullPath(pluginAssemblyPath));
			_resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
		}

		protected override Assembly Load(AssemblyName assemblyName)
		{
			// 将需要解析的程序集名称映射为插件目录中的具体路径
			var path = _resolver.ResolveAssemblyToPath(assemblyName);
			if (path != null)
			{
				return LoadFromAssemblyPath(path);
			}
			return null;
		}

		protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
		{
			// 对非托管依赖也通过 resolver 定位到插件包内对应的二进制并加载
			var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
			if (path != null)
			{
				return LoadUnmanagedDllFromPath(path);
			}
			return IntPtr.Zero;
		}
	}
}


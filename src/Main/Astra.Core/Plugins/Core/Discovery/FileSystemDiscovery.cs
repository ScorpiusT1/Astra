using Astra.Core.Plugins.Models;
using Astra.Core.Plugins.Manifest.Serializers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Astra.Core.Plugins.Discovery
{
    /// <summary>
    /// 基于文件系统的插件发现器。
    /// - 支持从指定目录递归查找 <c>*.addin</c> 清单文件并解析为 <see cref="PluginDescriptor"/>；
    /// - 利用 <c>ICacheManager</c> 对目录级别的发现结果做短期缓存；
    /// - 使用 <see cref="FileSystemWatcher"/> 监听目录变化，自动失效缓存，保证热更新生效。
    /// </summary>
    public class FileSystemDiscovery : IPluginDiscovery
    {
        private readonly List<IManifestSerializer> _serializers = new();
		private static readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();

        public FileSystemDiscovery(IEnumerable<IManifestSerializer> serializers)
        {
            _serializers.AddRange(serializers);
        }

		/// <summary>
		/// 发现指定目录下的插件清单并解析为 <see cref="PluginDescriptor"/> 集合。
		/// 优先返回缓存结果，若未命中则扫描磁盘并写入缓存。
		/// </summary>
		/// <param name="searchPath">要扫描的根目录</param>
		/// <returns>插件描述符集合</returns>
		public async Task<IEnumerable<PluginDescriptor>> DiscoverAsync(string searchPath)
        {
			var descriptors = new List<PluginDescriptor>();

            if (!Directory.Exists(searchPath))
                return descriptors;

			// 优先从缓存读取（按目录为键），减少频繁 IO/反序列化
			try
			{
				var cache = Astra.Core.Plugins.Services.ServiceLocator.ResolveOrDefault<Astra.Core.Plugins.Caching.ICacheManager>();
				if (cache != null)
				{
					var cached = await cache.GetAsync<IEnumerable<PluginDescriptor>>($"descriptors:{searchPath}");
					if (cached != null) return cached;
				}
			}
			catch { }

			// 查找所有 .addin 文件
            var addinFiles = Directory.GetFiles(searchPath, "*.addin", SearchOption.AllDirectories);

			// 建立目录变化监控，清单文件变化时主动失效缓存
			SetupWatcher(searchPath);

            foreach (var addinFile in addinFiles)
            {
                try
                {
                    var descriptor = await LoadAddinAsync(addinFile);
                    if (descriptor != null)
                    {
                        descriptors.Add(descriptor);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load addin {addinFile}: {ex.Message}");
                }
            }

			// 将本次结果写入缓存，设置适度过期时间以兼顾新鲜度与性能
			try
			{
				var cache = Astra.Core.Plugins.Services.ServiceLocator.ResolveOrDefault<Astra.Core.Plugins.Caching.ICacheManager>();
				if (cache != null)
				{
					await cache.SetAsync($"descriptors:{searchPath}", descriptors, new Astra.Core.Plugins.Caching.CacheOptions
					{
						Expiration = TimeSpan.FromMinutes(5)
					});
				}
			}
			catch { }

			return descriptors;
        }

		/// <summary>
		/// 为目标目录设置清单文件变化监听，变化时主动清理缓存。
		/// </summary>
		/// <param name="searchPath">监听目录</param>
		private void SetupWatcher(string searchPath)
		{
			if (_watchers.ContainsKey(searchPath)) return;

			try
			{
				var watcher = new FileSystemWatcher(searchPath, "*.addin")
				{
					IncludeSubdirectories = true,
					NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
				};

				FileSystemEventHandler handler = async (s, e) =>
				{
					try
					{
						var cache = Astra.Core.Plugins.Services.ServiceLocator.ResolveOrDefault<Astra.Core.Plugins.Caching.ICacheManager>();
						if (cache != null)
						{
							await cache.RemoveAsync($"descriptors:{searchPath}");
						}
					}
					catch { }
				};

				watcher.Created += handler;
				watcher.Changed += handler;
				watcher.Deleted += handler;
				watcher.Renamed += (s, e) => handler(s, e);
				watcher.EnableRaisingEvents = true;

				_watchers[searchPath] = watcher;
			}
			catch
			{
				// 忽略 watcher 失败
			}
		}

		/// <summary>
		/// 解析单个 .addin 清单为 <see cref="PluginDescriptor"/>。
		/// </summary>
		/// <param name="addinFile">清单文件完整路径</param>
		/// <returns>插件描述符</returns>
        private async Task<PluginDescriptor> LoadAddinAsync(string addinFile)
        {
            var serializer = _serializers.FirstOrDefault(s => s.CanHandle(addinFile));
            if (serializer == null)
                return null;

            using var stream = File.OpenRead(addinFile);
            var manifest = serializer.Deserialize(stream);

            var descriptor = new PluginDescriptor
            {
                Id = manifest.Addin.Id,
                Name = manifest.Addin.Name,
                Version = Version.Parse(manifest.Addin.Version),
                Description = manifest.Addin.Description,
                Author = manifest.Addin.Author,
                AssemblyPath = Path.Combine(Path.GetDirectoryName(addinFile),
                                           manifest.Addin.Runtime.Assembly),
                TypeName = manifest.Addin.Runtime.TypeName,
                IconPath = manifest.Addin.IconPath,
                State = PluginState.Discovered
            };

            // 解析依赖
            foreach (var dep in manifest.Addin.Dependencies)
            {
                descriptor.Dependencies.Add(new DependencyInfo
                {
                    PluginId = dep.AddinId,
                    VersionRange = ParseVersionRange(dep.Version),
                    IsOptional = dep.Optional
                });
            }

            // 解析权限
            descriptor.Permissions = ParsePermissions(manifest.Addin.Permissions.Required);

            return descriptor;
        }

		/// <summary>
		/// 解析版本区间字符串为 <see cref="VersionRange"/>。
		/// 支持格式: "1.0", "[1.0,2.0)", "(1.0,2.0]", "1.0+"。
		/// </summary>
		/// <param name="versionString">版本区间字符串</param>
		/// <returns>版本区间</returns>
        private VersionRange ParseVersionRange(string versionString)
        {
            // 支持格式: "1.0", "[1.0,2.0)", "(1.0,2.0]", "1.0+"
            var range = new VersionRange();

            if (versionString.EndsWith("+"))
            {
                range.MinVersion = Version.Parse(versionString.TrimEnd('+'));
                range.IncludeMin = true;
                return range;
            }

            if (versionString.StartsWith("[") || versionString.StartsWith("("))
            {
                range.IncludeMin = versionString[0] == '[';
                range.IncludeMax = versionString[^1] == ']';

                var parts = versionString.Trim('[', ']', '(', ')').Split(',');
                range.MinVersion = Version.Parse(parts[0]);
                if (parts.Length > 1)
                {
                    range.MaxVersion = Version.Parse(parts[1]);
                }
            }
            else
            {
                range.MinVersion = range.MaxVersion = Version.Parse(versionString);
            }

            return range;
        }

        /// <summary>
        /// 将权限字符串列表解析为权限位枚举。
        /// </summary>
        /// <param name="permissions">权限字符串列表</param>
        /// <returns>权限位</returns>
        private PluginPermissions ParsePermissions(List<string> permissions)
        {
            var result = PluginPermissions.None;
            foreach (var perm in permissions)
            {
                if (Enum.TryParse<PluginPermissions>(perm, true, out var parsed))
                {
                    result |= parsed;
                }
            }
            return result;
        }
    }
}

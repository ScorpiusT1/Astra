using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Astra.Core.Plugins.Configuration
{
	/// <summary>
	/// 提供默认的 IConfiguration 构建（支持 JSON 文件热重载）。
	/// </summary>
	public static class ConfigurationProviderRegistration
	{
		public static IConfiguration BuildDefaultConfiguration()
		{
			// 不依赖 ConfigurationBuilder/Json 包的轻量实现：解析两个 JSON 文件到内存字典，并用 FileSystemWatcher 触发热重载
			var files = new[] { "appsettings.json", "plugin-config.json" };
			var root = new SimpleJsonConfiguration(files);
			return root;
		}
	}

	internal sealed class SimpleJsonConfiguration : IConfigurationRoot, IConfiguration
	{
		private readonly string[] _files;
		private readonly ConcurrentDictionary<string, string> _data = new();
		private readonly List<FileSystemWatcher> _watchers = new();
		private volatile ReloadToken _reloadToken = new ReloadToken();

		public SimpleJsonConfiguration(string[] files)
		{
			_files = files ?? Array.Empty<string>();
			LoadAll();
			SetupWatchers();
		}

		public string this[string key]
		{
			get => _data.TryGetValue(Normalize(key), out var v) ? v : null;
			set
			{
				if (value == null) _data.TryRemove(Normalize(key), out _);
				else _data[Normalize(key)] = value;
			}
		}

		public IEnumerable<IConfigurationProvider> Providers => Enumerable.Empty<IConfigurationProvider>();

		public IChangeToken GetReloadToken() => _reloadToken;

		public IEnumerable<IConfigurationSection> GetChildren()
		{
			var roots = _data.Keys
				.Select(k => k.Split(':')[0])
				.Distinct(StringComparer.OrdinalIgnoreCase);
			foreach (var r in roots)
				yield return GetSection(r);
		}

		public IConfigurationSection GetSection(string key) => new Section(this, key);

		public void Reload()
		{
			LoadAll();
			var prev = _reloadToken;
			_reloadToken = new ReloadToken();
			prev.OnReload();
		}

		private void LoadAll()
		{
			var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var file in _files)
			{
				try
				{
					if (!File.Exists(file)) continue;
					var json = File.ReadAllText(file);
					using var doc = JsonDocument.Parse(json);
					FlattenJson(doc.RootElement, snapshot, parentPath: null);
				}
				catch
				{
					// 忽略单个文件解析错误，避免影响整体
				}
			}
			_data.Clear();
			foreach (var kv in snapshot) _data[kv.Key] = kv.Value;
		}

		private void SetupWatchers()
		{
			foreach (var file in _files.Distinct())
			{
				var dir = Path.GetDirectoryName(Path.GetFullPath(file));
				var name = Path.GetFileName(file);
				if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) continue;
				try
				{
					var w = new FileSystemWatcher(dir, name)
					{
						NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
						EnableRaisingEvents = true
					};
					FileSystemEventHandler handler = (s, e) => Reload();
					RenamedEventHandler renameHandler = (s, e) => Reload();
					w.Changed += handler;
					w.Created += handler;
					w.Deleted += handler;
					w.Renamed += renameHandler;
					_watchers.Add(w);
				}
				catch
				{
					// watcher 初始化失败则忽略热重载
				}
			}
		}

		private static string Normalize(string key) => key?.Replace("__", ":", StringComparison.Ordinal) ?? string.Empty;

		private static void FlattenJson(JsonElement element, Dictionary<string, string> dict, string parentPath)
		{
			switch (element.ValueKind)
			{
				case JsonValueKind.Object:
					foreach (var prop in element.EnumerateObject())
					{
						var path = string.IsNullOrEmpty(parentPath) ? prop.Name : $"{parentPath}:{prop.Name}";
						FlattenJson(prop.Value, dict, path);
					}
					break;
				case JsonValueKind.Array:
					int i = 0;
					foreach (var item in element.EnumerateArray())
					{
						var path = $"{parentPath}:{i}";
						FlattenJson(item, dict, path);
						i++;
					}
					break;
				case JsonValueKind.String:
					dict[parentPath] = element.GetString();
					break;
				case JsonValueKind.Number:
					dict[parentPath] = element.ToString();
					break;
				case JsonValueKind.True:
				case JsonValueKind.False:
					dict[parentPath] = element.GetBoolean().ToString();
					break;
				case JsonValueKind.Null:
				case JsonValueKind.Undefined:
					// 忽略
					break;
			}
		}

		private sealed class Section : IConfigurationSection
		{
			private readonly SimpleJsonConfiguration _root;
			public Section(SimpleJsonConfiguration root, string key)
			{
				_root = root;
				Key = key ?? string.Empty;
			}

			public string this[string key] { get => _root[$"{Path}:{key}"]; set => _root[$"{Path}:{key}"] = value; }
			public string Key { get; }
			public string Path => string.IsNullOrEmpty(_parentPath) ? Key : $"{_parentPath}:{Key}";
			public string Value { get => _root[Path]; set => _root[Path] = value; }

			private string _parentPath => null;

			public IEnumerable<IConfigurationSection> GetChildren()
			{
				// 查找所有以 Path: 开头的键的第一级子键
				var prefix = Path + ":";
				var children = _root._data.Keys
					.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					.Select(k => k.Substring(prefix.Length))
					.Select(k => k.Split(':')[0])
					.Distinct(StringComparer.OrdinalIgnoreCase);
				foreach (var c in children)
				{
					yield return new NestedSection(_root, c, Path);
				}
			}

			public IChangeToken GetReloadToken() => _root.GetReloadToken();

			public IConfigurationSection GetSection(string key) => new NestedSection(_root, key, Path);
		}

		private sealed class NestedSection : IConfigurationSection
		{
			private readonly SimpleJsonConfiguration _root;
			private readonly string _parentPath;
			public NestedSection(SimpleJsonConfiguration root, string key, string parentPath)
			{
				_root = root; Key = key ?? string.Empty; _parentPath = parentPath ?? string.Empty;
			}
			public string this[string key] { get => _root[$"{Path}:{key}"]; set => _root[$"{Path}:{key}"] = value; }
			public string Key { get; }
			public string Path => string.IsNullOrEmpty(_parentPath) ? Key : $"{_parentPath}:{Key}";
			public string Value { get => _root[Path]; set => _root[Path] = value; }
			public IEnumerable<IConfigurationSection> GetChildren()
			{
				var prefix = Path + ":";
				var children = _root._data.Keys
					.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					.Select(k => k.Substring(prefix.Length))
					.Select(k => k.Split(':')[0])
					.Distinct(StringComparer.OrdinalIgnoreCase);
				foreach (var c in children)
				{
					yield return new NestedSection(_root, c, Path);
				}
			}
			public IChangeToken GetReloadToken() => _root.GetReloadToken();
			public IConfigurationSection GetSection(string key) => new NestedSection(_root, key, Path);
		}

		private sealed class ReloadToken : IChangeToken
		{
			private event Action _listeners;
			public bool ActiveChangeCallbacks => true;
			public bool HasChanged { get; private set; }
			public IDisposable RegisterChangeCallback(Action<object> callback, object state)
			{
				void wrapper() { callback(state); }
				_listeners += wrapper;
				return new CallbackUnregister(() => _listeners -= wrapper);
			}
			public void OnReload()
			{
				HasChanged = true;
				_listeners?.Invoke();
				HasChanged = false;
			}
			private sealed class CallbackUnregister : IDisposable
			{
				private readonly Action _unregister;
				private bool _disposed;
				public CallbackUnregister(Action unregister) { _unregister = unregister; }
				public void Dispose() { if (_disposed) return; _disposed = true; _unregister(); }
			}
		}
	}
}


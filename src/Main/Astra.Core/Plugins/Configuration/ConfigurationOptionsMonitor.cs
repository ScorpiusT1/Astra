using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;

namespace Astra.Core.Plugins.Configuration
{
	/// <summary>
	/// 基于 IConfiguration 的轻量 IOptionsMonitor 实现，按类型名为节命名进行绑定，支持热重载。
	/// 例如：T = MyOptions -> 使用配置节 "MyOptions" 进行绑定。
	/// </summary>
	/// <typeparam name="T">强类型配置</typeparam>
	public sealed class ConfigurationOptionsMonitor<T> : IOptionsMonitor<T>, IDisposable where T : new()
	{
		private readonly IConfiguration _configuration;
		private readonly string _sectionName;
		private readonly List<Action<T, string>> _listeners = new();
		private IDisposable _reloadRegistration;
		private T _current;

		public ConfigurationOptionsMonitor(IConfiguration configuration)
		{
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_sectionName = typeof(T).Name;
			_current = Bind();
			_reloadRegistration = ChangeToken.OnChange(_configuration.GetReloadToken, OnReload);
		}

		public T CurrentValue => _current;

		public T Get(string name)
		{
			// 简化实现：忽略命名配置，统一返回当前值
			return _current;
		}

		public IDisposable OnChange(Action<T, string> listener)
		{
			if (listener == null) throw new ArgumentNullException(nameof(listener));
			_listeners.Add(listener);
			return new ListenerHandle(_listeners, listener);
		}

		private void OnReload()
		{
			var newValue = Bind();
			_current = newValue;
			for (int i = 0; i < _listeners.Count; i++)
			{
				try { _listeners[i]?.Invoke(newValue, _sectionName); } catch { }
			}
		}

		private T Bind()
		{
			var section = _configuration.GetSection(_sectionName);
			var instance = new T();
			SimpleBinder.BindSectionToObject(section, instance);
			return instance;
		}

		public void Dispose()
		{
			_reloadRegistration?.Dispose();
		}

		/// <summary>
		/// 轻量绑定器：不依赖 Binder 包，支持基础类型、枚举、可空、嵌套对象与简单数组（0,1,2,...）
		/// </summary>
		private static class SimpleBinder
		{
			public static void BindSectionToObject(IConfiguration section, object target)
			{
				if (section == null || target == null) return;

				var props = target.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
					.Where(p => p.CanWrite);

				foreach (var prop in props)
				{
					var child = section.GetSection(prop.Name);
					if (!child.Exists())
					{
						// 尝试直接取标量
						var raw = section[prop.Name];
						if (raw != null)
						{
							if (TryConvert(raw, prop.PropertyType, out var converted))
							{
								prop.SetValue(target, converted);
							}
						}
						continue;
					}

					// 复杂类型或数组
					if (IsSimpleType(prop.PropertyType))
					{
						var raw = child.Value ?? section[prop.Name];
						if (raw != null && TryConvert(raw, prop.PropertyType, out var converted))
						{
							prop.SetValue(target, converted);
						}
						continue;
					}

					if (IsEnumerableOfT(prop.PropertyType, out var elemType))
					{
						// 简单数组约定：0,1,2,...
						var items = new List<object>();
						int index = 0;
						while (true)
						{
							var itemSection = child.GetSection(index.ToString());
							if (!itemSection.Exists()) break;

							if (IsSimpleType(elemType))
							{
								var raw = itemSection.Value ?? child[index.ToString()];
								if (raw != null && TryConvert(raw, elemType, out var simpleItem))
								{
									items.Add(simpleItem);
								}
							}
							else
							{
								var item = Activator.CreateInstance(elemType);
								BindSectionToObject(itemSection, item);
								items.Add(item);
							}
							index++;
						}

						if (prop.PropertyType.IsArray)
						{
							var array = Array.CreateInstance(elemType, items.Count);
							for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
							prop.SetValue(target, array);
						}
						else if (typeof(System.Collections.IList).IsAssignableFrom(prop.PropertyType))
						{
							var list = (System.Collections.IList)(prop.GetValue(target) ?? Activator.CreateInstance(prop.PropertyType));
							foreach (var it in items) list.Add(it);
							prop.SetValue(target, list);
						}
						continue;
					}

					// 复杂对象递归绑定
					var childObj = prop.GetValue(target) ?? Activator.CreateInstance(prop.PropertyType);
					BindSectionToObject(child, childObj);
					prop.SetValue(target, childObj);
				}
			}

			private static bool IsEnumerableOfT(Type t, out Type elementType)
			{
				elementType = null;
				if (t.IsArray)
				{
					elementType = t.GetElementType();
					return true;
				}
				if (t.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(t.GetGenericTypeDefinition()))
				{
					elementType = t.GetGenericArguments()[0];
					return true;
				}
				// IList<T> 或 List<T> 等
				var ifaces = t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToList();
				if (ifaces.Count > 0)
				{
					elementType = ifaces[0].GetGenericArguments()[0];
					return true;
				}
				return false;
			}

			private static bool IsSimpleType(Type t)
			{
				var type = Nullable.GetUnderlyingType(t) ?? t;
				return type.IsPrimitive
					|| type.IsEnum
					|| type == typeof(string)
					|| type == typeof(decimal)
					|| type == typeof(DateTime)
					|| type == typeof(TimeSpan)
					|| type == typeof(Guid);
			}

			private static bool TryConvert(string raw, Type targetType, out object value)
			{
				try
				{
					var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
					if (type.IsEnum)
					{
						value = Enum.Parse(type, raw, ignoreCase: true);
						return true;
					}
					if (type == typeof(Guid))
					{
						value = Guid.Parse(raw);
						return true;
					}
					if (type == typeof(TimeSpan))
					{
						value = TimeSpan.Parse(raw);
						return true;
					}
					if (type == typeof(DateTime))
					{
						value = DateTime.Parse(raw);
						return true;
					}
					value = Convert.ChangeType(raw, type);
					return true;
				}
				catch
				{
					value = null;
					return false;
				}
			}
		}

		private sealed class ListenerHandle : IDisposable
		{
			private readonly List<Action<T, string>> _list;
			private readonly Action<T, string> _listener;
			private bool _disposed;
			public ListenerHandle(List<Action<T, string>> list, Action<T, string> listener)
			{
				_list = list; _listener = listener;
			}
			public void Dispose()
			{
				if (_disposed) return;
				_disposed = true;
				_list.Remove(_listener);
			}
		}
	}
}


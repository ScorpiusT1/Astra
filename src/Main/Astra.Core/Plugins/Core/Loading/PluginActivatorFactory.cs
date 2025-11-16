using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Astra.Core.Plugins.Abstractions;

namespace Astra.Core.Plugins.Loading
{
	/// <summary>
	/// 高性能插件激活器工厂。
	/// 使用表达式树缓存插件类型的无参构造函数委托，避免频繁的 <see cref="Activator.CreateInstance(Type)"/> 反射开销。
	/// 仅支持具有公共无参构造函数且实现 <see cref="IPlugin"/> 的类型。
	/// </summary>
	public static class PluginActivatorFactory
	{
		private static readonly ConcurrentDictionary<Type, Func<IPlugin>> _factories = new();

		/// <summary>
		/// 创建插件实例。第一次调用时编译表达式树并缓存，后续复用委托。
		/// </summary>
		public static IPlugin Create(Type pluginType)
		{
			if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
			return _factories.GetOrAdd(pluginType, BuildFactory)();
		}

		/// <summary>
		/// 为目标类型构建实例化委托。
		/// </summary>
		private static Func<IPlugin> BuildFactory(Type pluginType)
		{
			var ctor = pluginType.GetConstructor(Type.EmptyTypes);
			if (ctor == null)
				throw new InvalidOperationException($"Type {pluginType.FullName} must have a public parameterless constructor.");

			var newExpr = Expression.New(ctor);
			var castExpr = Expression.Convert(newExpr, typeof(IPlugin));
			var lambda = Expression.Lambda<Func<IPlugin>>(castExpr);
			return lambda.Compile();
		}
	}
}


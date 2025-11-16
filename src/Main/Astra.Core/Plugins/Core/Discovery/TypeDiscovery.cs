using Astra.Core.Plugins.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Discovery
{
    /// <summary>
    /// 类型发现服务 - 从程序集中发现特定类型
    /// </summary>
    public class TypeDiscovery
    {
        /// <summary>
        /// 查找实现了指定接口的所有类型
        /// </summary>
        public IEnumerable<Type> FindImplementations<TInterface>(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => typeof(TInterface).IsAssignableFrom(t));
        }

        /// <summary>
        /// 查找标记了指定特性的类型
        /// </summary>
        public IEnumerable<Type> FindTypesWithAttribute<TAttribute>(Assembly assembly)
            where TAttribute : Attribute
        {
            return assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TAttribute>() != null);
        }

        /// <summary>
        /// 查找插件类型
        /// </summary>
        public Type FindPluginType(Assembly assembly)
        {
            // 优先查找标记了 [Plugin] 特性的类型
            var markedType = assembly.GetTypes()
                .FirstOrDefault(t => t.GetCustomAttribute<PluginAttribute>() != null);

            if (markedType != null)
                return markedType;

            // 查找实现 IPlugin 的类型
            return assembly.GetTypes()
                .FirstOrDefault(t => t.IsClass && !t.IsAbstract && typeof(IPlugin).IsAssignableFrom(t));
        }

        /// <summary>
        /// 扫描类型的所有扩展点
        /// </summary>
        public IEnumerable<ExtensionPointInfo> ScanExtensionPoints(Type type)
        {
            var extensionPoints = new List<ExtensionPointInfo>();

            // 扫描方法
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<ExtensionPointAttribute>();
                if (attr != null)
                {
                    extensionPoints.Add(new ExtensionPointInfo
                    {
                        Path = attr.Path,
                        Name = attr.Name ?? method.Name,
                        Type = ExtensionPointType.Method,
                        MemberInfo = method
                    });
                }
            }

            // 扫描属性
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = property.GetCustomAttribute<ExtensionPointAttribute>();
                if (attr != null)
                {
                    extensionPoints.Add(new ExtensionPointInfo
                    {
                        Path = attr.Path,
                        Name = attr.Name ?? property.Name,
                        Type = ExtensionPointType.Property,
                        MemberInfo = property
                    });
                }
            }

            return extensionPoints;
        }
    }

    /// <summary>
    /// 插件标记特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public PluginAttribute() { }

        public PluginAttribute(string id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// 扩展点标记特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class ExtensionPointAttribute : Attribute
    {
        public string Path { get; set; }
        public string Name { get; set; }

        public ExtensionPointAttribute(string path)
        {
            Path = path;
        }
    }

    /// <summary>
    /// 扩展点信息
    /// </summary>
    public class ExtensionPointInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public ExtensionPointType Type { get; set; }
        public MemberInfo MemberInfo { get; set; }
    }

    public enum ExtensionPointType
    {
        Method,
        Property,
        Event
    }
}

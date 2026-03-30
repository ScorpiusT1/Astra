using System.Collections.Concurrent;
using System.IO;
using Astra.Core.Constants;
using Astra.Core.Devices.Configuration;

namespace Astra.Core.Configuration.Helpers
{
    /// <summary>
    /// 配置路径统一管理类。
    ///
    /// 扩展方式（开闭原则）：新增配置类型不需要修改本类，
    /// 在插件/模块启动时调用 <see cref="RegisterConfigDirectory{T}(string)"/> 或
    /// <see cref="RegisterInheritanceMapping{T}"/> 注册即可。
    ///
    /// 解析优先级：精确注册 &gt; 继承注册 &gt; 约定规则（去掉 Config 后缀作为目录名）。
    /// </summary>
    public static class ConfigPathString
    {
        public const string DefaultConfigTypeSuffix = AstraSharedConstants.ConfigDefaults.DefaultConfigTypeSuffix;

        public static string BaseConfigDirectory     => Path.Combine(AppContext.BaseDirectory, "Configs");
        public static string DeviceConfigDirectory   => Path.Combine(BaseConfigDirectory, "Devices");
        public static string SensorConfigDirectory   => Path.Combine(BaseConfigDirectory, "Sensors");
        public static string DatabaseConfigDirectory => Path.Combine(BaseConfigDirectory, "Database");

        // 使用线程安全集合，支持在多线程环境中并发注册
        private static readonly ConcurrentDictionary<Type, Func<string>> _exactMappings = new();
        private static readonly object _inheritanceLock = new();
        private static readonly List<(Type BaseType, Func<string> DirectoryFactory)> _inheritanceMappings = new();

        static ConfigPathString()
        {
            RegisterInheritanceMapping<DeviceConfig>(() => DeviceConfigDirectory);
        }

        /// <summary>注册精确类型的配置目录（静态路径）。</summary>
        public static void RegisterConfigDirectory<T>(string directory) where T : class
            => _exactMappings[typeof(T)] = () => directory;

        /// <summary>注册精确类型的配置目录（动态路径工厂，延迟求值）。</summary>
        public static void RegisterConfigDirectory<T>(Func<string> directoryFactory) where T : class
            => _exactMappings[typeof(T)] = directoryFactory
                ?? throw new ArgumentNullException(nameof(directoryFactory));

        /// <summary>
        /// 注册基类/接口的配置目录（所有子类/实现类共用同一目录）。
        /// 多个继承映射按注册顺序匹配，先注册者优先。
        /// </summary>
        public static void RegisterInheritanceMapping<T>(Func<string> directoryFactory) where T : class
        {
            if (directoryFactory == null) throw new ArgumentNullException(nameof(directoryFactory));
            lock (_inheritanceLock) _inheritanceMappings.Add((typeof(T), directoryFactory));
        }

        /// <summary>
        /// 根据配置类型获取配置目录。
        /// 解析优先级：1. 精确注册  2. 继承注册  3. 约定规则（去掉 Config 后缀）
        /// </summary>
        public static string GetConfigDirectory(Type configType)
        {
            if (configType == null) throw new ArgumentNullException(nameof(configType));

            if (_exactMappings.TryGetValue(configType, out var exactFactory))
                return exactFactory();

            List<(Type, Func<string>)> snapshot;
            lock (_inheritanceLock) snapshot = _inheritanceMappings.ToList();

            foreach (var (baseType, factory) in snapshot)
            {
                if (baseType.IsAssignableFrom(configType))
                    return factory();
            }

            var name = configType.Name;
            var dirName = name.EndsWith(DefaultConfigTypeSuffix, StringComparison.Ordinal)
                ? name[..^DefaultConfigTypeSuffix.Length]
                : name;
            return Path.Combine(BaseConfigDirectory, dirName);
        }

        /// <summary>确保配置目录存在（不存在则创建）。</summary>
        public static void EnsureConfigDirectoryExists(Type configType)
        {
            var directory = GetConfigDirectory(configType);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}

using Astra.Core.Devices.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// ConfigProvider 自动发现服务
    /// 扫描所有程序集，自动发现并注册 ConfigProvider
    /// 符合约定优于配置原则，提高易用性
    /// </summary>
    public class ConfigProviderDiscovery
    {
        private readonly IConfigurationManager _configManager;
        private readonly ILogger<ConfigProviderDiscovery> _logger;

        public ConfigProviderDiscovery(
            IConfigurationManager configManager,
            ILogger<ConfigProviderDiscovery> logger = null)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger;
        }

        /// <summary>
        /// 从所有已加载的程序集中自动发现并注册 ConfigProvider
        /// </summary>
        public void AutoRegisterAllProviders()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            _logger?.LogInformation("开始自动发现 ConfigProvider，程序集数量: {Count}", assemblies.Length);

            var registeredCount = 0;
            foreach (var assembly in assemblies)
            {
                try
                {
                    var count = DiscoverAndRegisterProviders(assembly);
                    registeredCount += count;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "扫描程序集 {AssemblyName} 时出错", assembly.FullName);
                }
            }

            _logger?.LogInformation("自动发现完成，共注册 {Count} 个 ConfigProvider", registeredCount);
        }

        /// <summary>
        /// 从指定程序集中发现并注册 ConfigProvider
        /// </summary>
        /// <returns>注册的 Provider 数量</returns>
        public int DiscoverAndRegisterProviders(Assembly assembly)
        {
            var registeredCount = 0;

            // 查找所有继承自 JsonConfigProvider<T> 的类型
            // ⚠️ 注意：assembly.GetTypes() 可能抛出 ReflectionTypeLoadException
            // 需要处理这种情况，确保即使部分类型加载失败也能继续扫描
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 部分类型加载失败，但可以使用已成功加载的类型继续扫描
                types = ex.Types.Where(t => t != null).ToArray();
                _logger?.LogWarning(ex, "程序集 {AssemblyName} 部分类型加载失败，将使用已成功加载的类型继续扫描", assembly.FullName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "无法从程序集 {AssemblyName} 获取类型，跳过该程序集", assembly.FullName);
                return 0;
            }

            var providerTypes = types
                .Where(t => t != null && t.IsClass && !t.IsAbstract)
                .Where(t => IsConfigProviderType(t))
                .ToList();

            foreach (var providerType in providerTypes)
            {
                try
                {
                    // 提取配置类型（从泛型参数）
                    var configType = ExtractConfigType(providerType);
                    if (configType == null)
                    {
                        _logger?.LogWarning("无法从 Provider {ProviderType} 提取配置类型", providerType.Name);
                        continue;
                    }

                    // 检查是否已注册
                    var existsMethod = typeof(IConfigurationManager).GetMethod(
                        nameof(IConfigurationManager.ExistsAsync),
                        new[] { typeof(string) });
                    // 这里简化：直接尝试注册，如果已存在则跳过

                    // 创建 Provider 实例
                    var configDirectory = GetConfigDirectory(configType);
                    _logger?.LogInformation("为配置类型 {ConfigType} 创建 Provider，配置目录: {ConfigDirectory}", 
                        configType.Name, configDirectory);
                    
                    var provider = CreateProviderInstance(providerType, configType);
                    if (provider == null)
                    {
                        _logger?.LogWarning("无法创建 Provider 实例: {ProviderType}", providerType.Name);
                        continue;
                    }

                    // 注册 Provider（使用反射调用泛型方法）
                    RegisterProviderGeneric(provider, configType);

                    registeredCount++;
                    _logger?.LogInformation("自动注册 ConfigProvider: {ProviderType} -> {ConfigType}, 配置目录: {ConfigDirectory}",
                        providerType.Name, configType.Name, configDirectory);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "注册 Provider {ProviderType} 失败", providerType.Name);
                }
            }

            return registeredCount;
        }

        /// <summary>
        /// 判断是否为 ConfigProvider 类型
        /// </summary>
        private bool IsConfigProviderType(Type type)
        {
            // 检查是否继承自 JsonConfigProvider<>
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && 
                    baseType.GetGenericTypeDefinition() == typeof(JsonConfigProvider<>))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            return false;
        }

        /// <summary>
        /// 从 Provider 类型中提取配置类型
        /// </summary>
        private Type ExtractConfigType(Type providerType)
        {
            // 从基类的泛型参数中提取配置类型
            var baseType = providerType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && 
                    baseType.GetGenericTypeDefinition() == typeof(JsonConfigProvider<>))
                {
                    var genericArgs = baseType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var configType = genericArgs[0];
                        // 验证配置类型是否实现 IConfig
                        if (typeof(IConfig).IsAssignableFrom(configType))
                        {
                            return configType;
                        }
                    }
                }
                baseType = baseType.BaseType;
            }
            return null;
        }

        /// <summary>
        /// 创建 Provider 实例（需要 Provider 有无参构造函数或可推断的构造函数）
        /// </summary>
        private object CreateProviderInstance(Type providerType, Type configType)
        {
            // 尝试使用配置文件路径约定创建 Provider
            // 约定：配置文件路径 = ConfigPathString.BaseConfigDirectory + 配置类型名称
            var configDirectory = GetConfigDirectory(configType);
            var options = CreateProviderOptions(configType);

            // 查找接受 (string, ConfigProviderOptions<T>) 的构造函数
            var optionsType = typeof(ConfigProviderOptions<>).MakeGenericType(configType);
            var constructor = providerType.GetConstructor(new[] { typeof(string), optionsType });
            
            if (constructor != null)
            {
                return Activator.CreateInstance(providerType, configDirectory, options);
            }

            // 如果没有找到，尝试无参构造函数
            var parameterlessConstructor = providerType.GetConstructor(Type.EmptyTypes);
            if (parameterlessConstructor != null)
            {
                return Activator.CreateInstance(providerType);
            }

            _logger?.LogWarning("Provider {ProviderType} 没有找到合适的构造函数", providerType.Name);
            return null;
        }

        /// <summary>
        /// 获取配置目录（基于约定）
        /// 现在统一使用 ConfigPathString.GetConfigDirectory() 方法
        /// </summary>
        private string GetConfigDirectory(Type configType)
        {
            // 优先使用 ConfigPathString 的统一方法
            return ConfigPathString.GetConfigDirectory(configType);
        }

        /// <summary>
        /// 创建 Provider 选项（基于约定）
        /// </summary>
        private object CreateProviderOptions(Type configType)
        {
            var optionsType = typeof(ConfigProviderOptions<>).MakeGenericType(configType);
            var options = Activator.CreateInstance(optionsType);

            // 设置默认集合文件名（约定：{配置类型名}.json）
            var defaultFileName = $"{configType.Name}.json";
            
            // 类型安全的方式获取属性名：使用辅助方法从表达式树中提取属性名
            // 这样可以在编译时检查属性是否存在，避免硬编码字符串
            var propertyName = GetPropertyName<ConfigProviderOptions<ConfigBase>>(
                x => x.DefaultCollectionFileName);
            
            // 从构造后的具体类型获取属性
            var propertyInfo = optionsType.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            
            propertyInfo?.SetValue(options, defaultFileName);

            // ⚠️ 关键：对于设备配置（DeviceConfig 的子类），启用自动搜索所有文件
            // 这样可以支持插件配置文件（如 Astra.Plugins.DataAcquisition.config.json）
            if (typeof(DeviceConfig).IsAssignableFrom(configType))
            {
                var autoSearchPropertyName = GetPropertyName<ConfigProviderOptions<ConfigBase>>(
                    x => x.AutoSearchAllFiles);
                var autoSearchProperty = optionsType.GetProperty(
                    autoSearchPropertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                autoSearchProperty?.SetValue(options, true);
                
                _logger?.LogInformation("为设备配置类型 {ConfigType} 启用自动搜索所有文件", configType.Name);
            }

            // 确保配置目录存在
            ConfigPathString.EnsureConfigDirectoryExists(configType);

            return options;
        }

        /// <summary>
        /// 从表达式树中提取属性名（类型安全的方式）
        /// </summary>
        private static string GetPropertyName<T>(Expression<Func<T, object>> expression)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            
            if (expression.Body is UnaryExpression unaryExpression 
                && unaryExpression.Operand is MemberExpression operand)
            {
                return operand.Member.Name;
            }
            
            throw new ArgumentException("表达式必须是一个属性访问表达式", nameof(expression));
        }

        /// <summary>
        /// 使用反射注册 Provider（调用泛型方法 RegisterProvider<T>）
        /// </summary>
        private void RegisterProviderGeneric(object provider, Type configType)
        {
            var method = typeof(IConfigurationManager).GetMethod(
                nameof(IConfigurationManager.RegisterProvider),
                BindingFlags.Public | BindingFlags.Instance);

            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(configType);
                genericMethod.Invoke(_configManager, new[] { provider });
            }
        }
    }
}


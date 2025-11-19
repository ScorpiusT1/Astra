using Astra.Core.Foundation.Common;
using Astra.Core.Devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置管理器
    /// 统一管理所有类型的配置（设备配置、基础配置等）
    /// </summary>
    public class ConfigurationManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, IConfig> _configs = new ConcurrentDictionary<string, IConfig>();
        private readonly ConcurrentDictionary<string, List<ConfigChangeRecord>> _changeHistory = new ConcurrentDictionary<string, List<ConfigChangeRecord>>();
        // 存储配置文件路径（ConfigId -> 配置文件路径）
        private readonly ConcurrentDictionary<string, string> _configFilePaths = new ConcurrentDictionary<string, string>();
        private bool _disposed = false;

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        /// <summary>
        /// 获取配置数量
        /// </summary>
        public int Count => _configs.Count;

        /// <summary>
        /// 获取所有配置ID
        /// </summary>
        public IEnumerable<string> GetAllConfigIds() => _configs.Keys;

        /// <summary>
        /// 获取所有配置
        /// </summary>
        public IEnumerable<IConfig> GetAllConfigs() => _configs.Values;

        /// <summary>
        /// 按类型获取配置
        /// </summary>
        public IEnumerable<IConfig> GetConfigsByType(string configType)
        {
            return _configs.Values.Where(c => c.ConfigType == configType);
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        public OperationResult<IConfig> GetConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return OperationResult<IConfig>.Fail("配置ID不能为空", ErrorCodes.InvalidData);

            if (_configs.TryGetValue(configId, out var config))
            {
                return OperationResult<IConfig>.Succeed(config);
            }

            return OperationResult<IConfig>.Fail($"配置 {configId} 不存在", ErrorCodes.ConfigNotFound);
        }

        /// <summary>
        /// 获取配置（泛型版本）
        /// </summary>
        public OperationResult<T> GetConfig<T>(string configId) where T : class, IConfig
        {
            var result = GetConfig(configId);
            if (!result.Success)
                return OperationResult<T>.Fail(result.ErrorMessage, result.ErrorCode);

            if (result.Data is T typedConfig)
            {
                return OperationResult<T>.Succeed(typedConfig);
            }

            return OperationResult<T>.Fail($"配置 {configId} 类型不匹配", ErrorCodes.InvalidData);
        }

        /// <summary>
        /// 注册配置
        /// </summary>
        /// <param name="config">配置对象</param>
        /// <param name="configFilePath">配置文件路径（可选，如果提供会在注册时保存路径信息）</param>
        public OperationResult RegisterConfig(IConfig config, string configFilePath = null)
        {
            if (config == null)
                return OperationResult.Fail("配置对象不能为空", ErrorCodes.InvalidData);

            if (string.IsNullOrWhiteSpace(config.ConfigId))
                return OperationResult.Fail("配置ID不能为空", ErrorCodes.InvalidData);

            // 验证配置
            var validateResult = config.Validate();
            if (!validateResult.Success)
                return OperationResult.Fail($"配置验证失败: {validateResult.ErrorMessage}", ErrorCodes.InvalidConfig);

            if (_configs.TryAdd(config.ConfigId, config))
            {
                // 处理配置文件路径
                if (!string.IsNullOrWhiteSpace(configFilePath))
                {
                    // 如果提供了路径，使用提供的路径
                    _configFilePaths[config.ConfigId] = configFilePath;
                }
                else
                {
                    // 如果没有提供路径，尝试查找同类型配置的路径（同一类型配置使用相同路径）
                    var existingPath = GetConfigFilePathByType(config.ConfigType);
                    if (!string.IsNullOrWhiteSpace(existingPath))
                    {
                        _configFilePaths[config.ConfigId] = existingPath;
                    }
                }

                // 订阅配置变更事件
                config.ConfigChanged += OnConfigChanged;

                // 记录变更历史
                RecordConfigChange(config.ConfigId, new ConfigChangedEventArgs
                {
                    ConfigId = config.ConfigId,
                    ConfigType = config.ConfigType,
                    ChangedProperties = new List<string> { "Registered" },
                    NewConfig = config,
                    Timestamp = DateTime.Now,
                    ChangedBy = "System"
                });

                return OperationResult.Succeed($"配置 {config.ConfigId} 注册成功");
            }

            return OperationResult.Fail($"配置 {config.ConfigId} 已存在", ErrorCodes.InvalidData);
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public OperationResult UpdateConfig(string configId, IConfig newConfig, string changedBy = null)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return OperationResult.Fail("配置ID不能为空", ErrorCodes.InvalidData);

            if (newConfig == null)
                return OperationResult.Fail("新配置对象不能为空", ErrorCodes.InvalidData);

            if (!_configs.TryGetValue(configId, out IConfig? existingConfig))
                return OperationResult.Fail($"配置 {configId} 不存在", ErrorCodes.ConfigNotFound);

            if (existingConfig.GetType() != newConfig.GetType())
                return OperationResult.Fail($"配置类型不匹配", ErrorCodes.InvalidData);

            // 验证新配置
            var validateResult = newConfig.Validate();
            if (!validateResult.Success)
                return OperationResult.Fail($"配置验证失败: {validateResult.ErrorMessage}", ErrorCodes.InvalidConfig);

            // 获取变更的属性
            List<string> changedProperties;
            if (existingConfig is BaseConfig baseConfig)
            {
                changedProperties = baseConfig.GetChangedProperties(newConfig);
            }
            else
            {
                // 对于其他类型的配置，使用反射比较
                changedProperties = CompareConfigProperties(existingConfig, newConfig);
            }
            
            if (changedProperties.Count == 0)
                return OperationResult.Succeed("配置无变更");

            // 检查是否需要重启
            var restartRequired = existingConfig.GetRestartRequiredProperties()
                .Intersect(changedProperties)
                .ToList();

            if (restartRequired.Any())
            {
                var restartProps = string.Join(", ", restartRequired);
                return OperationResult.Fail(
                    $"以下配置项需要重启: {restartProps}",
                    ErrorCodes.ConfigRequireRestart)
                    .WithData("RestartRequired", true)
                    .WithData("RestartProperties", restartRequired);
            }

            // 更新配置
            newConfig.ConfigId = configId; // 确保ID一致
            _configs[configId] = newConfig;

            // 取消旧配置的事件订阅，订阅新配置的事件
            existingConfig.ConfigChanged -= OnConfigChanged;
            newConfig.ConfigChanged += OnConfigChanged;

            // 记录变更历史
            RecordConfigChange(configId, new ConfigChangedEventArgs
            {
                ConfigId = configId,
                ConfigType = newConfig.ConfigType,
                ChangedProperties = changedProperties,
                OldConfig = existingConfig,
                NewConfig = newConfig,
                Timestamp = DateTime.Now,
                ChangedBy = changedBy ?? "System"
            });

            // 触发配置变更事件
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ConfigId = configId,
                ConfigType = newConfig.ConfigType,
                ChangedProperties = changedProperties,
                OldConfig = existingConfig,
                NewConfig = newConfig,
                Timestamp = DateTime.Now,
                ChangedBy = changedBy ?? "System"
            });

            return OperationResult.Succeed($"配置更新成功，变更了 {changedProperties.Count} 个属性")
                .WithData("ChangedProperties", changedProperties);
        }

        /// <summary>
        /// 注销配置
        /// </summary>
        public OperationResult UnregisterConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return OperationResult.Fail("配置ID不能为空", ErrorCodes.InvalidData);

            if (_configs.TryRemove(configId, out var config))
            {
                // 取消事件订阅
                config.ConfigChanged -= OnConfigChanged;

                // 记录变更历史
                RecordConfigChange(configId, new ConfigChangedEventArgs
                {
                    ConfigId = configId,
                    ConfigType = config.ConfigType,
                    ChangedProperties = new List<string> { "Unregistered" },
                    OldConfig = config,
                    Timestamp = DateTime.Now,
                    ChangedBy = "System"
                });

                return OperationResult.Succeed($"配置 {configId} 注销成功");
            }

            return OperationResult.Fail($"配置 {configId} 不存在", ErrorCodes.ConfigNotFound);
        }

        /// <summary>
        /// 检查配置是否存在
        /// </summary>
        public bool ConfigExists(string configId)
        {
            return !string.IsNullOrWhiteSpace(configId) && _configs.ContainsKey(configId);
        }

        /// <summary>
        /// 设置配置文件路径
        /// </summary>
        public void SetConfigFilePath(string configId, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(configId) && !string.IsNullOrWhiteSpace(filePath))
            {
                _configFilePaths[configId] = filePath;
            }
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string GetConfigFilePath(string configId)
        {
            if (!string.IsNullOrWhiteSpace(configId) && _configFilePaths.TryGetValue(configId, out var filePath))
            {
                return filePath;
            }
            return null;
        }

        /// <summary>
        /// 根据配置类型获取配置文件路径（返回同一类型配置的公共路径）
        /// </summary>
        public string GetConfigFilePathByType(string configType)
        {
            // 查找同一类型配置的第一个配置路径，作为该类型配置的公共路径
            var config = _configs.Values.FirstOrDefault(c => c.ConfigType == configType);
            if (config != null && _configFilePaths.TryGetValue(config.ConfigId, out var filePath))
            {
                return filePath;
            }
            return null;
        }

        /// <summary>
        /// 获取配置变更历史
        /// </summary>
        public OperationResult<List<ConfigChangeRecord>> GetChangeHistory(string configId, int maxCount = 10)
        {
            if (_changeHistory.TryGetValue(configId, out var history))
            {
                var records = history.TakeLast(maxCount).ToList();
                return OperationResult<List<ConfigChangeRecord>>.Succeed(records);
            }

            return OperationResult<List<ConfigChangeRecord>>.Succeed(new List<ConfigChangeRecord>());
        }

        /// <summary>
        /// 处理配置变更事件
        /// </summary>
        private void OnConfigChanged(object sender, ConfigChangedEventArgs e)
        {
            // 记录变更历史
            if (sender is IConfig config)
            {
                RecordConfigChange(config.ConfigId, e);
            }

            // 触发管理器级别的配置变更事件
            ConfigChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 记录配置变更历史
        /// </summary>
        private void RecordConfigChange(string configId, ConfigChangedEventArgs e)
        {
            var record = new ConfigChangeRecord
            {
                ConfigId = configId,
                ConfigType = e.ConfigType,
                Timestamp = e.Timestamp,
                ChangedBy = e.ChangedBy,
                ChangedProperties = e.ChangedProperties,
                OldValues = e.OldConfig?.ToDictionary(),
                NewValues = e.NewConfig?.ToDictionary()
            };

            _changeHistory.AddOrUpdate(
                configId,
                new List<ConfigChangeRecord> { record },
                (key, list) =>
                {
                    list.Add(record);
                    // 限制历史记录数量
                    if (list.Count > 100)
                        list.RemoveAt(0);
                    return list;
                });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // 取消所有配置的事件订阅
                foreach (var config in _configs.Values)
                {
                    config.ConfigChanged -= OnConfigChanged;
                }

                _configs.Clear();
                _changeHistory.Clear();
                _configFilePaths.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// 比较两个配置的属性差异（反射方式）
        /// </summary>
        private List<string> CompareConfigProperties(IConfig config1, IConfig config2)
        {
            var changedProperties = new List<string>();
            
            if (config1 == null || config2 == null || config1.GetType() != config2.GetType())
                return changedProperties;

            var properties = config1.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value1 = prop.GetValue(config1);
                        var value2 = prop.GetValue(config2);

                        if (!Equals(value1, value2))
                        {
                            changedProperties.Add(prop.Name);
                        }
                    }
                    catch
                    {
                        // 忽略无法读取的属性
                    }
                }
            }

            return changedProperties;
        }
    }

    /// <summary>
    /// 配置变更记录
    /// </summary>
    public class ConfigChangeRecord
    {
        public string ConfigId { get; set; }
        public string ConfigType { get; set; }
        public DateTime Timestamp { get; set; }
        public string ChangedBy { get; set; }
        public List<string> ChangedProperties { get; set; }
        public Dictionary<string, object> OldValues { get; set; }
        public Dictionary<string, object> NewValues { get; set; }
    }
}


using Astra.Core.Foundation.Common;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 智能JSON文件配置提供者 - 严格版本，强制实现 IConfig
    /// </summary>
    public abstract class JsonConfigProvider<T> : IConfigProvider<T>
        where T : class, IConfig  // ← 严格约束：必须实现 IConfig
    {
        protected readonly string ConfigDirectory;
        protected readonly JsonSerializerSettings JsonSettings;
        protected readonly ConfigProviderOptions<T> Options;

        // 配置索引：ConfigId → 元数据
        private readonly ConcurrentDictionary<string, ConfigMetadata> _configIndex
            = new ConcurrentDictionary<string, ConfigMetadata>();

        // 文件格式缓存：FileName → StorageFormat
        private readonly ConcurrentDictionary<string, ConfigStorageFormat> _fileFormatCache
            = new ConcurrentDictionary<string, ConfigStorageFormat>();

        private bool _indexBuilt = false;
        private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);

        protected JsonConfigProvider(
            string configDirectory,
            ConfigProviderOptions<T>? options)
        {
            ConfigDirectory = configDirectory;
            Options = options ?? new ConfigProviderOptions<T>();

            JsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include, // ✅ 不忽略null值，确保数据完整性
                DefaultValueHandling = DefaultValueHandling.Include,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                // Newtonsoft.Json 可以访问 protected setter（因为反序列化发生在同一类型或派生类型中）
            };

            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }
        }

        protected virtual string GetFilePath(string fileName)
        {
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(ConfigDirectory, fileName);
            }

            return Path.Combine(ConfigDirectory, $"{fileName}.json");
        }

        /// <summary>
        /// 获取配置的唯一标识符（直接使用 ConfigId）
        /// </summary>
        protected string GetConfigIdentifier(T config)
        {
            return config.ConfigId;  // ← 简化：直接使用 ConfigId，编译时保证存在
        }

        /// <summary>
        /// 检测文件格式
        /// </summary>
        private async Task<ConfigStorageFormat> DetectFileFormatAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var trimmed = json.TrimStart();

                if (trimmed.StartsWith("["))
                {
                    return ConfigStorageFormat.Array;
                }
                else if (trimmed.StartsWith("{"))
                {
                    // 需要进一步判断是 SingleObject 还是 Container
                    var root = JObject.Parse(json);

                    // 检查是否有 Configs 属性（或自定义的容器属性名）
                    if (root.TryGetValue(Options.ContainerPropertyName, out var configsProp)
                        && configsProp.Type == JTokenType.Array)
                    {
                        return ConfigStorageFormat.Container;
                    }

                    return ConfigStorageFormat.SingleObject;
                }

                return ConfigStorageFormat.Auto;
            }
            catch (JsonReaderException jsonEx)
            {
                // JSON 格式错误，记录详细错误信息
                System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] JSON 格式检测失败: {filePath}, 错误: {jsonEx.Message}");
                return ConfigStorageFormat.Auto;
            }
            catch (JsonException jsonEx)
            {
                // JSON 序列化错误
                System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] JSON 格式检测失败: {filePath}, 错误: {jsonEx.Message}");
                return ConfigStorageFormat.Auto;
            }
            catch (Exception ex)
            {
                // 其他错误（如文件读取失败）
                System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] 文件格式检测失败: {filePath}, 错误: {ex.Message}");
                return ConfigStorageFormat.Auto;
            }
        }

        /// <summary>
        /// 从文件加载配置列表
        /// </summary>
        private async Task<List<T>?> LoadConfigsFromFileAsync(string filePath, ConfigStorageFormat format)
        {
            // 如果格式为 Auto，先自动检测实际格式
            if (format == ConfigStorageFormat.Auto)
            {
                format = await DetectFileFormatAsync(filePath);
                
                // 如果检测失败，记录错误并返回 null
                if (format == ConfigStorageFormat.Auto)
                {
                    System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] 无法自动检测配置文件格式: {filePath}，请检查 JSON 语法是否正确");
                    return null;
                }
            }

            var json = await File.ReadAllTextAsync(filePath);
            List<T>? configs = null;

            switch (format)
            {
                case ConfigStorageFormat.SingleObject:
                    configs = new List<T> { JsonConvert.DeserializeObject<T>(json, JsonSettings)! };
                    break;

                case ConfigStorageFormat.Array:
                    configs = JsonConvert.DeserializeObject<List<T>>(json, JsonSettings);
                    break;

                case ConfigStorageFormat.Container:
                    configs = LoadFromContainer(json);
                    break;

                case ConfigStorageFormat.Auto:
                    // 理论上不应该到达这里（已在上面处理过）
                    System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] 配置格式仍为 Auto，无法加载: {filePath}");
                    return null;

                default:
                    System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] 不支持的配置格式: {format}，文件: {filePath}");
                    return null;
            }

            // Newtonsoft.Json 应该能够通过 internal setter 设置 ConfigId
            // 但如果 ConfigId 仍然为空（JSON 中没有或反序列化失败），则生成新的
            // 注意：这不应该覆盖 JSON 中的 ConfigId，因为 SetConfigId 只在 ConfigId 为空时才设置
            if (configs != null)
            {
                foreach (var config in configs)
                {
                    if (config is ConfigBase configBase && string.IsNullOrWhiteSpace(configBase.ConfigId))
                    {
                        // 仅在 ConfigId 为空时生成新的（用于向后兼容或创建新配置）
                        configBase.SetConfigId(System.Guid.NewGuid().ToString());
                    }
                }
            }

            return configs;
        }

        /// <summary>
        /// 确保配置有 ConfigId
        /// 如果 ConfigId 为空，则生成新的（用于创建新配置的场景）
        /// 注意：JSON 反序列化时，如果 JSON 中包含 configId，Newtonsoft.Json 会通过 internal setter 设置
        /// </summary>
        private void EnsureConfigId(T config)
        {
            if (config is ConfigBase configBase && string.IsNullOrWhiteSpace(configBase.ConfigId))
            {
                // 仅在 ConfigId 为空时生成新的（说明是新创建的配置，不是从 JSON 反序列化的）
                configBase.SetConfigId(System.Guid.NewGuid().ToString());
            }
        }

        /// <summary>
        /// 从容器格式加载配置
        /// </summary>
        private List<T>? LoadFromContainer(string json)
        {
            try
            {
                // 使用动态方式解析容器
                var root = JObject.Parse(json);

                if (root.TryGetValue(Options.ContainerPropertyName, out var configsProp))
                {
                    var configsJson = configsProp.ToString();
                    return JsonConvert.DeserializeObject<List<T>>(configsJson, JsonSettings);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 保存配置列表到文件
        /// </summary>
        private async Task SaveConfigsToFileAsync(
            string filePath,
            List<T> configs,
            ConfigStorageFormat format)
        {
            string json;

            switch (format)
            {
                case ConfigStorageFormat.SingleObject:
                    if (configs.Count != 1)
                    {
                        throw new InvalidOperationException("SingleObject 格式只能保存一个配置");
                    }
                    json = JsonConvert.SerializeObject(configs[0], JsonSettings);
                    break;

                case ConfigStorageFormat.Array:
                    json = JsonConvert.SerializeObject(configs, JsonSettings);
                    break;

                case ConfigStorageFormat.Container:
                    json = SaveToContainer(configs);
                    break;

                default:
                    throw new NotSupportedException($"不支持的存储格式: {format}");
            }

            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// 保存到容器格式
        /// </summary>
        private string SaveToContainer(List<T> configs)
        {
            // 如果指定了容器类型，使用该类型
            if (Options.ContainerType != null)
            {
                var container = Activator.CreateInstance(Options.ContainerType);
                var configsProp = Options.ContainerType.GetProperty(Options.ContainerPropertyName);
                configsProp?.SetValue(container, configs);

                // 尝试设置 LastModified
                var lastModifiedProp = Options.ContainerType.GetProperty("LastModified");
                if (lastModifiedProp != null && lastModifiedProp.PropertyType == typeof(DateTime))
                {
                    lastModifiedProp.SetValue(container, DateTime.Now);
                }

                return JsonConvert.SerializeObject(container, JsonSettings);
            }

            // 否则使用默认容器
            var defaultContainer = new ConfigContainer<T>
            {
                Configs = configs,
                LastModified = DateTime.Now
            };
            return JsonConvert.SerializeObject(defaultContainer, JsonSettings);
        }

        /// <summary>
        /// 构建配置索引
        /// </summary>
        private async Task BuildIndexAsync()
        {
            _configIndex.Clear();
            _fileFormatCache.Clear();

            // 如果启用自动扫描，则遍历目录下所有 json 文件
            if (Options.AutoSearchAllFiles)
            {
                var files = Directory.GetFiles(ConfigDirectory, "*.json");

                foreach (var filePath in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var format = await DetectFileFormatAsync(filePath);

                    _fileFormatCache[fileName] = format;

                    var configs = await LoadConfigsFromFileAsync(filePath, format);
                    if (configs == null) continue;

                    foreach (var config in configs)
                    {
                        var configId = config.ConfigId;  // ← 直接使用 ConfigId

                        _configIndex[configId] = new ConfigMetadata
                        {
                            ConfigId = configId,
                            FileName = fileName,
                            StorageMode = format switch
                            {
                                ConfigStorageFormat.SingleObject => ConfigStorageMode.SingleFile,
                                _ => ConfigStorageMode.Collection
                            }
                        };
                    }
                }
            }
            else
            {
                // 否则只使用 Options.DefaultCollectionFileName 指定的单个文件
                var fileName = Options.DefaultCollectionFileName;
                var filePath = GetFilePath(fileName);

                if (!File.Exists(filePath))
                {
                    // 文件不存在时，仅注册默认格式，索引保持为空，后续保存时会创建
                    var defaultFormat = Options.DefaultFormat == ConfigStorageFormat.Auto
                        ? Options.NewConfigFormat
                        : Options.DefaultFormat;

                    _fileFormatCache[fileName] = defaultFormat;
                    return;
                }

                var formatToUse = Options.DefaultFormat == ConfigStorageFormat.Auto
                    ? await DetectFileFormatAsync(filePath)
                    : Options.DefaultFormat;

                _fileFormatCache[fileName] = formatToUse;

                var configs = await LoadConfigsFromFileAsync(filePath, formatToUse);
                if (configs == null) return;

                foreach (var config in configs)
                {
                    var configId = config.ConfigId;

                    _configIndex[configId] = new ConfigMetadata
                    {
                        ConfigId = configId,
                        FileName = fileName,
                        StorageMode = formatToUse switch
                        {
                            ConfigStorageFormat.SingleObject => ConfigStorageMode.SingleFile,
                            _ => ConfigStorageMode.Collection
                        }
                    };
                }
            }
        }

        private async Task EnsureIndexBuiltAsync()
        {
            if (_indexBuilt) return;

            await _indexLock.WaitAsync();
            try
            {
                if (_indexBuilt) return;
                await BuildIndexAsync();
                _indexBuilt = true;
            }
            finally
            {
                _indexLock.Release();
            }
        }

        // ==================== 公共接口实现 ====================

        public virtual async Task<OperationResult<T>> LoadAsync(string configId)
        {
            try
            {
                await EnsureIndexBuiltAsync();

                if (!_configIndex.TryGetValue(configId, out var metadata))
                {
                    return OperationResult<T>.Failure($"未找到配置: {configId}");
                }

                var filePath = GetFilePath(metadata.FileName);
                var format = _fileFormatCache[metadata.FileName];
                var configs = await LoadConfigsFromFileAsync(filePath, format);

                var config = configs?.FirstOrDefault(c => c.ConfigId == configId);  // ← 直接使用 ConfigId
                if (config == null)
                {
                    return OperationResult<T>.Failure($"未找到配置: {configId}");
                }

                return OperationResult<T>.Succeed(config);
            }
            catch (Exception ex)
            {
                return OperationResult<T>.Failure($"加载配置失败: {ex.Message}");
            }
        }

        public virtual async Task<OperationResult> SaveAsync(T config)
        {
            try
            {
                await EnsureIndexBuiltAsync();

                var configId = config.ConfigId;  // ← 直接使用 ConfigId

                if (string.IsNullOrEmpty(configId))
                {
                    return OperationResult.Failure($"配置ID不能为空");
                }

                if (_configIndex.TryGetValue(configId, out var metadata))
                {
                    // 更新现有配置
                    return await UpdateConfigInFileAsync(metadata.FileName, config);
                }
                else
                {
                    // 新建配置
                    var fileName = Options.DefaultCollectionFileName;
                    var format = Options.NewConfigFormat;

                    var result = await AddConfigToFileAsync(fileName, config, format);
                    if (result.Success)
                    {
                        // 更新索引
                        _configIndex[configId] = new ConfigMetadata
                        {
                            ConfigId = configId,
                            FileName = fileName,
                            StorageMode = format == ConfigStorageFormat.SingleObject
                                ? ConfigStorageMode.SingleFile
                                : ConfigStorageMode.Collection
                        };
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"保存配置失败: {ex.Message}");
            }
        }

        private async Task<OperationResult> UpdateConfigInFileAsync(string fileName, T config)
        {
            var filePath = GetFilePath(fileName);
            var format = _fileFormatCache[fileName];
            var configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();

            var configId = config.ConfigId;  // ← 直接使用 ConfigId
            var index = configs.FindIndex(c => c.ConfigId == configId);  // ← 直接使用 ConfigId

            if (index >= 0)
            {
                configs[index] = config;
            }
            else
            {
                configs.Add(config);
            }

            await SaveConfigsToFileAsync(filePath, configs, format);
            return OperationResult.Succeed();
        }

        private async Task<OperationResult> AddConfigToFileAsync(
            string fileName,
            T config,
            ConfigStorageFormat format)
        {
            var filePath = GetFilePath(fileName);
            List<T> configs;

            if (File.Exists(filePath))
            {
                // 读取现有文件内容
                var existingFormat = _fileFormatCache.GetValueOrDefault(fileName, format);
                configs = await LoadConfigsFromFileAsync(filePath, existingFormat) ?? new List<T>();
                format = existingFormat; // 使用现有格式

                // 只在“不存在该配置”时追加；如果已存在同一 ConfigId，则视为更新
                var configId = config.ConfigId;
                var index = configs.FindIndex(c => c.ConfigId == configId);
                if (index >= 0)
                {
                    configs[index] = config; // 更新已有配置
                }
                else
                {
                    configs.Add(config);     // 追加新配置
                }
            }
            else
            {
                // 文件不存在时，新建列表并记录格式
                configs = new List<T> { config };
                _fileFormatCache[fileName] = format;
            }

            await SaveConfigsToFileAsync(filePath, configs, format);
            return OperationResult.Succeed();
        }

        public virtual async Task<OperationResult> DeleteAsync(string configId)
        {
            try
            {
                await EnsureIndexBuiltAsync();

                if (!_configIndex.TryGetValue(configId, out var metadata))
                {
                    return OperationResult.Failure($"未找到配置: {configId}");
                }

                var filePath = GetFilePath(metadata.FileName);
                var format = _fileFormatCache[metadata.FileName];
                var configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();

                var removed = configs.RemoveAll(c => c.ConfigId == configId);  // ← 直接使用 ConfigId
                if (removed == 0)
                {
                    return OperationResult.Failure($"未找到配置: {configId}");
                }

                if (configs.Count == 0 && format == ConfigStorageFormat.SingleObject)
                {
                    // 如果是单文件模式且已无配置，删除文件
                    File.Delete(filePath);
                    _fileFormatCache.TryRemove(metadata.FileName, out _);
                }
                else
                {
                    await SaveConfigsToFileAsync(filePath, configs, format);
                }

                _configIndex.TryRemove(configId, out _);
                return OperationResult.Succeed();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"删除配置失败: {ex.Message}");
            }
        }

        public virtual async Task<bool> ExistsAsync(string configId)
        {
            await EnsureIndexBuiltAsync();
            return _configIndex.ContainsKey(configId);
        }

        public virtual async Task<OperationResult<IEnumerable<T>>> GetAllAsync()
        {
            try
            {
                await EnsureIndexBuiltAsync();

                var allConfigs = new List<T>();
                var processedFiles = new HashSet<string>();

                foreach (var metadata in _configIndex.Values)
                {
                    if (!processedFiles.Add(metadata.FileName))
                        continue;

                    var filePath = GetFilePath(metadata.FileName);
                    var format = _fileFormatCache[metadata.FileName];
                    var configs = await LoadConfigsFromFileAsync(filePath, format);

                    if (configs != null)
                    {
                        allConfigs.AddRange(configs);
                    }
                }

                return OperationResult<IEnumerable<T>>.Succeed(allConfigs);
            }
            catch (Exception ex)
            {
                return OperationResult<IEnumerable<T>>.Failure($"获取所有配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 实现基接口方法，将泛型结果转换为 IConfig 集合
        /// </summary>
        public virtual async Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync()
        {
            var result = await GetAllAsync();
            if (!result.Success)
            {
                return OperationResult<IEnumerable<IConfig>>.Failure(result.Message);
            }

            // 将 IEnumerable<T> 转换为 IEnumerable<IConfig>
            var configs = result.Data.Cast<IConfig>().ToList();
            return OperationResult<IEnumerable<IConfig>>.Succeed(configs);
        }

        /// <summary>
        /// 保存配置（非泛型版本）
        /// </summary>
        public virtual async Task<OperationResult> SaveConfigAsync(IConfig config)
        {
            if (config is T typedConfig)
            {
                return await SaveAsync(typedConfig);
            }

            return OperationResult.Failure($"配置类型不匹配：需要 {typeof(T).Name}，但提供的是 {config.GetType().Name}");
        }

        public virtual async Task RebuildIndexAsync()
        {
            await _indexLock.WaitAsync();
            try
            {
                _indexBuilt = false;
                await EnsureIndexBuiltAsync();
            }
            finally
            {
                _indexLock.Release();
            }
        }
    }
}

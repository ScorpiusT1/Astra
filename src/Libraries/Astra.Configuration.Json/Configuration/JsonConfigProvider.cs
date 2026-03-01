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
        where T : class, IConfig
    {
        protected readonly string ConfigDirectory;
        protected readonly JsonSerializerSettings JsonSettings;
        protected readonly ConfigProviderOptions<T> Options;

        private readonly ConcurrentDictionary<string, ConfigMetadata> _configIndex
            = new ConcurrentDictionary<string, ConfigMetadata>();

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
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
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

        protected string GetConfigIdentifier(T config)
        {
            return config.ConfigId;
        }

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
                    var root = JObject.Parse(json);

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
                System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] JSON 格式检测失败: {filePath}, 错误: {jsonEx.Message}");
                return ConfigStorageFormat.Auto;
            }
            catch (JsonException jsonEx)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] JSON 格式检测失败: {filePath}, 错误: {jsonEx.Message}");
                return ConfigStorageFormat.Auto;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] 文件格式检测失败: {filePath}, 错误: {ex.Message}");
                return ConfigStorageFormat.Auto;
            }
        }

        private async Task<List<T>?> LoadConfigsFromFileAsync(string filePath, ConfigStorageFormat format)
        {
            if (format == ConfigStorageFormat.Auto)
            {
                format = await DetectFileFormatAsync(filePath);
                if (format == ConfigStorageFormat.Auto)
                {
                    System.Diagnostics.Debug.WriteLine($"[JsonConfigProvider] 无法自动检测配置文件格式: {filePath}");
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
                    return null;
                default:
                    return null;
            }

            if (configs != null)
            {
                foreach (var config in configs)
                {
                    if (config is ConfigBase configBase && string.IsNullOrWhiteSpace(configBase.ConfigId))
                    {
                        configBase.SetConfigId(System.Guid.NewGuid().ToString());
                    }
                }
            }

            return configs;
        }

        private void EnsureConfigId(T config)
        {
            if (config is ConfigBase configBase && string.IsNullOrWhiteSpace(configBase.ConfigId))
            {
                configBase.SetConfigId(System.Guid.NewGuid().ToString());
            }
        }

        private List<T>? LoadFromContainer(string json)
        {
            try
            {
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

        private string SaveToContainer(List<T> configs)
        {
            if (Options.ContainerType != null)
            {
                var container = Activator.CreateInstance(Options.ContainerType);
                var configsProp = Options.ContainerType.GetProperty(Options.ContainerPropertyName);
                configsProp?.SetValue(container, configs);

                var lastModifiedProp = Options.ContainerType.GetProperty("LastModified");
                if (lastModifiedProp != null && lastModifiedProp.PropertyType == typeof(DateTime))
                {
                    lastModifiedProp.SetValue(container, DateTime.Now);
                }

                return JsonConvert.SerializeObject(container, JsonSettings);
            }

            var defaultContainer = new ConfigContainer<T>
            {
                Configs = configs,
                LastModified = DateTime.Now
            };
            return JsonConvert.SerializeObject(defaultContainer, JsonSettings);
        }

        private async Task BuildIndexAsync()
        {
            _configIndex.Clear();
            _fileFormatCache.Clear();

            if (Options.AutoSearchAllFiles)
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    return;
                }

                var files = Directory.GetFiles(ConfigDirectory, "*.json");

                foreach (var filePath in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var format = await DetectFileFormatAsync(filePath);

                    _fileFormatCache[fileName] = format;

                    var configs = await LoadConfigsFromFileAsync(filePath, format);
                    if (configs == null)
                    {
                        continue;
                    }

                    foreach (var config in configs)
                    {
                        var configId = config.ConfigId;

                        if (string.IsNullOrEmpty(configId))
                        {
                            continue;
                        }

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
                var fileName = Options.DefaultCollectionFileName;
                if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }
                var filePath = GetFilePath(fileName);

                if (!File.Exists(filePath))
                {
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

        public virtual async Task<OperationResult<T>> LoadAsync(string configId)
        {
            try
            {
                await EnsureIndexBuiltAsync();

                if (!_configIndex.TryGetValue(configId, out var metadata))
                {
                    return OperationResult<T>.Failure($"未找到配置: {configId}");
                }

                var fileName = metadata.FileName;
                if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }

                var filePath = GetFilePath(fileName);
                var format = _fileFormatCache.GetValueOrDefault(fileName, Options.DefaultFormat);

                if (!_fileFormatCache.ContainsKey(fileName) && File.Exists(filePath))
                {
                    format = await DetectFileFormatAsync(filePath);
                    _fileFormatCache[fileName] = format;
                }

                var configs = await LoadConfigsFromFileAsync(filePath, format);

                var config = configs?.FirstOrDefault(c => c.ConfigId == configId);
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

                var configId = config.ConfigId;

                if (string.IsNullOrEmpty(configId))
                {
                    return OperationResult.Failure($"配置ID不能为空");
                }

                if (_configIndex.TryGetValue(configId, out var metadata))
                {
                    return await UpdateConfigInFileAsync(metadata.FileName, config);
                }
                else
                {
                    var fileName = Options.DefaultCollectionFileName;
                    if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = Path.GetFileNameWithoutExtension(fileName);
                    }
                    var format = Options.NewConfigFormat;

                    var result = await AddConfigToFileAsync(fileName, config, format);
                    if (result.Success)
                    {
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
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            var filePath = GetFilePath(fileName);
            var format = _fileFormatCache.GetValueOrDefault(fileName, Options.NewConfigFormat);

            if (!_fileFormatCache.ContainsKey(fileName) && File.Exists(filePath))
            {
                format = await DetectFileFormatAsync(filePath);
                _fileFormatCache[fileName] = format;
            }

            var configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();

            var configId = config.ConfigId;
            var index = configs.FindIndex(c => c.ConfigId == configId);

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
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            var filePath = GetFilePath(fileName);
            List<T> configs;

            if (File.Exists(filePath))
            {
                if (!_fileFormatCache.ContainsKey(fileName))
                {
                    var detectedFormat = await DetectFileFormatAsync(filePath);
                    _fileFormatCache[fileName] = detectedFormat;
                    format = detectedFormat;
                }
                else
                {
                    format = _fileFormatCache[fileName];
                }

                configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();

                var configId = config.ConfigId;
                var index = configs.FindIndex(c => c.ConfigId == configId);
                if (index >= 0)
                {
                    configs[index] = config;
                }
                else
                {
                    configs.Add(config);
                }
            }
            else
            {
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

                var fileName = metadata.FileName;
                if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }

                var filePath = GetFilePath(fileName);
                var format = _fileFormatCache.GetValueOrDefault(fileName, Options.DefaultFormat);

                if (!_fileFormatCache.ContainsKey(fileName) && File.Exists(filePath))
                {
                    format = await DetectFileFormatAsync(filePath);
                    _fileFormatCache[fileName] = format;
                }

                var configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();

                var removed = configs.RemoveAll(c => c.ConfigId == configId);
                if (removed == 0)
                {
                    return OperationResult.Failure($"未找到配置: {configId}");
                }

                if (configs.Count == 0 && format == ConfigStorageFormat.SingleObject)
                {
                    File.Delete(filePath);
                    _fileFormatCache.TryRemove(fileName, out _);
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
                    var fileName = metadata.FileName;
                    if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = Path.GetFileNameWithoutExtension(fileName);
                    }

                    if (!processedFiles.Add(fileName))
                        continue;

                    var filePath = GetFilePath(fileName);

                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    var format = _fileFormatCache.GetValueOrDefault(fileName, Options.DefaultFormat);

                    if (!_fileFormatCache.ContainsKey(fileName))
                    {
                        format = await DetectFileFormatAsync(filePath);
                        _fileFormatCache[fileName] = format;
                    }

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

        public virtual async Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync()
        {
            var result = await GetAllAsync();
            if (!result.Success)
            {
                return OperationResult<IEnumerable<IConfig>>.Failure(result.Message);
            }

            var configs = result.Data.Cast<IConfig>().ToList();
            return OperationResult<IEnumerable<IConfig>>.Succeed(configs);
        }

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


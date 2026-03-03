using Astra.Core.Foundation.Common;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Astra.Core.Configuration.Providers
{
    /// <summary>
    /// 智能JSON文件配置提供者 — 支持 SingleObject / Array / Container 三种存储格式，自动检测。
    /// </summary>
    public abstract class JsonConfigProvider<T> : IConfigProvider<T>
        where T : class, IConfig
    {
        protected readonly string ConfigDirectory;
        protected readonly JsonSerializerSettings JsonSettings;
        protected readonly ConfigProviderOptions<T> Options;

        // 配置索引：ConfigId → 元数据
        private readonly ConcurrentDictionary<string, ConfigMetadata> _configIndex = new();

        // 文件格式缓存：FileName → StorageFormat
        private readonly ConcurrentDictionary<string, ConfigStorageFormat> _fileFormatCache = new();

        private volatile bool _indexBuilt;
        private readonly SemaphoreSlim _indexLock = new(1, 1);

        protected JsonConfigProvider(string configDirectory, ConfigProviderOptions<T>? options)
        {
            ConfigDirectory = configDirectory;
            Options = options ?? new ConfigProviderOptions<T>();
            JsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                // 对于配置类，反序列化时始终用 JSON 覆盖集合属性，避免构造函数里初始化的默认集合残留或被“部分合并”
                // 这样像 DataAcquisitionConfig.Channels 这类集合会完全按文件里的内容重建，不会夹杂默认“通道 1/2/3/4”。
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);
        }

        protected virtual string GetFilePath(string fileName)
        {
            return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(ConfigDirectory, fileName)
                : Path.Combine(ConfigDirectory, $"{fileName}.json");
        }

        /// <summary>
        /// 检测文件存储格式，返回文件内容和格式（避免调用方再次读取文件）。
        /// </summary>
        private async Task<(ConfigStorageFormat Format, string Json)> DetectFileFormatAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var trimmed = json.TrimStart();
                if (trimmed.StartsWith("[")) return (ConfigStorageFormat.Array, json);
                if (trimmed.StartsWith("{"))
                {
                    var root = JObject.Parse(json);
                    if (root.TryGetValue(Options.ContainerPropertyName, out var prop) && prop.Type == JTokenType.Array)
                        return (ConfigStorageFormat.Container, json);
                    return (ConfigStorageFormat.SingleObject, json);
                }
                return (ConfigStorageFormat.Auto, json);
            }
            catch
            {
                return (ConfigStorageFormat.Auto, null);
            }
        }

        private async Task<List<T>?> LoadConfigsFromFileAsync(string filePath, ConfigStorageFormat format)
        {
            string json;

            if (format == ConfigStorageFormat.Auto)
            {
                var (detectedFormat, detectedJson) = await DetectFileFormatAsync(filePath);
                if (detectedFormat == ConfigStorageFormat.Auto) return null;
                format = detectedFormat;
                json = detectedJson;
            }
            else
            {
                json = await File.ReadAllTextAsync(filePath);
            }

            List<T>? configs = format switch
            {
                ConfigStorageFormat.SingleObject => new List<T> { JsonConvert.DeserializeObject<T>(json, JsonSettings)! },
                ConfigStorageFormat.Array        => JsonConvert.DeserializeObject<List<T>>(json, JsonSettings),
                ConfigStorageFormat.Container    => LoadFromContainer(json),
                _                                => null
            };

            if (configs != null)
            {
                foreach (var config in configs)
                {
                    if (config is ConfigBase cb && string.IsNullOrWhiteSpace(cb.ConfigId))
                        cb.SetConfigId(Guid.NewGuid().ToString());
                }
            }
            return configs;
        }

        private List<T>? LoadFromContainer(string json)
        {
            var root = JObject.Parse(json);
            if (root.TryGetValue(Options.ContainerPropertyName, out var configsProp))
                return JsonConvert.DeserializeObject<List<T>>(configsProp.ToString(), JsonSettings);
            return null;
        }

        private async Task SaveConfigsToFileAsync(string filePath, List<T> configs, ConfigStorageFormat format)
        {
            string json = format switch
            {
                ConfigStorageFormat.SingleObject => configs.Count == 1
                    ? JsonConvert.SerializeObject(configs[0], JsonSettings)
                    : throw new InvalidOperationException("SingleObject 格式只能保存一个配置"),
                ConfigStorageFormat.Array     => JsonConvert.SerializeObject(configs, JsonSettings),
                ConfigStorageFormat.Container => SaveToContainer(configs),
                _                             => throw new NotSupportedException($"不支持的存储格式: {format}")
            };

            // 原子写入：先写临时文件，再重命名，避免写入过程中崩溃导致配置文件损坏
            var tempPath = filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, filePath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }
        }

        private string SaveToContainer(List<T> configs)
        {
            if (Options.ContainerType != null)
            {
                var container = Activator.CreateInstance(Options.ContainerType);
                Options.ContainerType.GetProperty(Options.ContainerPropertyName)?.SetValue(container, configs);
                var lastModProp = Options.ContainerType.GetProperty("LastModified");
                if (lastModProp?.PropertyType == typeof(DateTime))
                    lastModProp.SetValue(container, DateTime.Now);
                return JsonConvert.SerializeObject(container, JsonSettings);
            }
            return JsonConvert.SerializeObject(new ConfigContainer<T> { Configs = configs, LastModified = DateTime.Now }, JsonSettings);
        }

        private async Task BuildIndexAsync()
        {
            _configIndex.Clear();
            _fileFormatCache.Clear();

            if (Options.AutoSearchAllFiles)
            {
                if (!Directory.Exists(ConfigDirectory)) return;
                foreach (var filePath in Directory.GetFiles(ConfigDirectory, "*.json"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var (format, _) = await DetectFileFormatAsync(filePath);
                    _fileFormatCache[fileName] = format;
                    var configs = await LoadConfigsFromFileAsync(filePath, format);
                    if (configs == null) continue;
                    foreach (var config in configs)
                    {
                        if (string.IsNullOrEmpty(config.ConfigId)) continue;
                        _configIndex[config.ConfigId] = new ConfigMetadata
                        {
                            ConfigId = config.ConfigId,
                            FileName = fileName,
                            StorageMode = format == ConfigStorageFormat.SingleObject
                                ? ConfigStorageMode.SingleFile : ConfigStorageMode.Collection
                        };
                    }
                }
            }
            else
            {
                var fileName = Options.DefaultCollectionFileName;
                var filePath = GetFilePath(fileName);
                if (!File.Exists(filePath))
                {
                    _fileFormatCache[fileName] = Options.DefaultFormat == ConfigStorageFormat.Auto
                        ? Options.NewConfigFormat : Options.DefaultFormat;
                    return;
                }
                var format = Options.DefaultFormat == ConfigStorageFormat.Auto
                    ? (await DetectFileFormatAsync(filePath)).Format : Options.DefaultFormat;
                _fileFormatCache[fileName] = format;
                var configs = await LoadConfigsFromFileAsync(filePath, format);
                if (configs == null) return;
                foreach (var config in configs)
                {
                    _configIndex[config.ConfigId] = new ConfigMetadata
                    {
                        ConfigId = config.ConfigId,
                        FileName = fileName,
                        StorageMode = format == ConfigStorageFormat.SingleObject
                            ? ConfigStorageMode.SingleFile : ConfigStorageMode.Collection
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
            finally { _indexLock.Release(); }
        }

        /// <remarks>
        /// 内部实现：调用方已持有 <c>_indexLock</c> 时使用，避免重入死锁。
        /// </remarks>
        private async Task RebuildIndexInternalAsync()
        {
            _indexBuilt = false;
            await BuildIndexAsync();
            _indexBuilt = true;
        }

        // ── 公共接口实现 ──────────────────────────────────────────────────────

        public virtual async Task<OperationResult<T>> LoadAsync(string configId)
        {
            try
            {
                await EnsureIndexBuiltAsync();
                if (!_configIndex.TryGetValue(configId, out var metadata))
                    return OperationResult<T>.Failure($"未找到配置: {configId}");

                var configs = await LoadConfigsFromFileAsync(GetFilePath(metadata.FileName), _fileFormatCache[metadata.FileName]);
                var config = configs?.FirstOrDefault(c => c.ConfigId == configId);
                return config != null
                    ? OperationResult<T>.Succeed(config)
                    : OperationResult<T>.Failure($"未找到配置: {configId}");
            }
            catch (Exception ex) { return OperationResult<T>.Failure($"加载配置失败: {ex.Message}"); }
        }

        public virtual async Task<OperationResult> SaveAsync(T config)
        {
            try
            {
                await EnsureIndexBuiltAsync();
                if (string.IsNullOrEmpty(config.ConfigId))
                    return OperationResult.Failure("配置ID不能为空");

                if (_configIndex.TryGetValue(config.ConfigId, out var metadata))
                    return await UpdateConfigInFileAsync(metadata.FileName, config);

                var fileName = Options.DefaultCollectionFileName;
                var result = await AddConfigToFileAsync(fileName, config, Options.NewConfigFormat);
                if (result.Success)
                {
                    _configIndex[config.ConfigId] = new ConfigMetadata
                    {
                        ConfigId = config.ConfigId,
                        FileName = fileName,
                        StorageMode = Options.NewConfigFormat == ConfigStorageFormat.SingleObject
                            ? ConfigStorageMode.SingleFile : ConfigStorageMode.Collection
                    };
                }
                return result;
            }
            catch (Exception ex) { return OperationResult.Failure($"保存配置失败: {ex.Message}"); }
        }

        private async Task<OperationResult> UpdateConfigInFileAsync(string fileName, T config)
        {
            var filePath = GetFilePath(fileName);
            var format = _fileFormatCache[fileName];
            var configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();
            var index = configs.FindIndex(c => c.ConfigId == config.ConfigId);
            if (index >= 0) configs[index] = config;
            else configs.Add(config);
            await SaveConfigsToFileAsync(filePath, configs, format);
            return OperationResult.Succeed();
        }

        private async Task<OperationResult> AddConfigToFileAsync(string fileName, T config, ConfigStorageFormat format)
        {
            var filePath = GetFilePath(fileName);
            List<T> configs;
            if (File.Exists(filePath))
            {
                var existingFormat = _fileFormatCache.GetValueOrDefault(fileName, format);
                configs = await LoadConfigsFromFileAsync(filePath, existingFormat) ?? new List<T>();
                format = existingFormat;
                var index = configs.FindIndex(c => c.ConfigId == config.ConfigId);
                if (index >= 0) configs[index] = config;
                else configs.Add(config);
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
                    return OperationResult.Failure($"未找到配置: {configId}");

                var filePath = GetFilePath(metadata.FileName);
                var format = _fileFormatCache[metadata.FileName];
                var configs = await LoadConfigsFromFileAsync(filePath, format) ?? new List<T>();
                if (configs.RemoveAll(c => c.ConfigId == configId) == 0)
                    return OperationResult.Failure($"未找到配置: {configId}");

                if (configs.Count == 0 && format == ConfigStorageFormat.SingleObject)
                {
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
            catch (Exception ex) { return OperationResult.Failure($"删除配置失败: {ex.Message}"); }
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
                    if (!processedFiles.Add(metadata.FileName)) continue;
                    var filePath = GetFilePath(metadata.FileName);
                    if (!File.Exists(filePath)) continue;
                    var configs = await LoadConfigsFromFileAsync(filePath, _fileFormatCache[metadata.FileName]);
                    if (configs != null) allConfigs.AddRange(configs);
                }
                return OperationResult<IEnumerable<T>>.Succeed(allConfigs);
            }
            catch (Exception ex) { return OperationResult<IEnumerable<T>>.Failure($"获取所有配置失败: {ex.Message}"); }
        }

        public virtual async Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync()
        {
            var result = await GetAllAsync();
            return result.Success
                ? OperationResult<IEnumerable<IConfig>>.Succeed(result.Data.Cast<IConfig>().ToList())
                : OperationResult<IEnumerable<IConfig>>.Failure(result.Message);
        }

        public virtual async Task<OperationResult> SaveConfigAsync(IConfig config)
        {
            return config is T typed
                ? await SaveAsync(typed)
                : OperationResult.Failure($"配置类型不匹配：需要 {typeof(T).Name}，但提供的是 {config.GetType().Name}");
        }

        public virtual async Task RebuildIndexAsync()
        {
            await _indexLock.WaitAsync();
            try
            {
                // 直接调用内部实现，避免重入 _indexLock 导致死锁
                await RebuildIndexInternalAsync();
            }
            finally { _indexLock.Release(); }
        }
    }

    /// <summary>
    /// 开箱即用的 JSON 配置提供者 — 使用默认设置，无需派生子类。
    /// 插件或主程序注册配置时直接使用此类，无需单独创建 Provider 文件。
    /// </summary>
    public sealed class DefaultJsonConfigProvider<T> : JsonConfigProvider<T>
        where T : class, IConfig
    {
        public DefaultJsonConfigProvider(string configDirectory, ConfigProviderOptions<T> options = null)
            : base(configDirectory, options) { }
    }
}

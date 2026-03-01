using Astra.Core.Foundation.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// 配置导入/导出服务实现。
    ///
    /// 文件格式（UTF-8 JSON）：
    /// <code>
    /// {
    ///   "exportVersion": "1.0",
    ///   "exportedAt": "2024-01-01T00:00:00Z",
    ///   "entryCount": 3,
    ///   "entries": [
    ///     {
    ///       "typeName": "Astra.Plugins.DataAcquisition.Configs.SensorConfig",
    ///       "configId": "abc123",
    ///       "configName": "传感器1",
    ///       "data": { ... }
    ///     }
    ///   ]
    /// }
    /// </code>
    /// 导入时按 <c>typeName</c> 在 <see cref="IConfigurationManager.GetRegisteredTypes()"/>
    /// 中匹配已注册类型，未注册的类型将被记录为失败并跳过。
    /// </summary>
    public class ConfigurationImportExportService : IConfigurationImportExportService
    {
        private readonly IConfigurationManager _configManager;
        private readonly ILogger<ConfigurationImportExportService> _logger;

        public ConfigurationImportExportService(
            IConfigurationManager configManager,
            ILogger<ConfigurationImportExportService> logger = null)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger;
        }

        // ── 导出 ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<OperationResult> ExportAsync(string filePath, ExportOptions options = null)
        {
            try
            {
                var json = await BuildBundleJsonAsync(typeFilter: null, options);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                _logger?.LogInformation("所有配置已导出至: {Path}", filePath);
                return OperationResult.Succeed($"已导出至 {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置失败: {Path}", filePath);
                return OperationResult.Failure($"导出失败: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult> ExportAsync<T>(string filePath, ExportOptions options = null)
            where T : class, IConfig
        {
            try
            {
                var result = await _configManager.GetAllAsync<T>();
                var configs = result.Success
                    ? result.Data.Cast<IConfig>()
                    : Enumerable.Empty<IConfig>();

                var json = BuildBundleJson(configs, options);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                _logger?.LogInformation("配置 {Type} 已导出至: {Path}", typeof(T).Name, filePath);
                return OperationResult.Succeed($"已导出至 {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置 {Type} 失败: {Path}", typeof(T).Name, filePath);
                return OperationResult.Failure($"导出失败: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult<string>> ExportToStringAsync(ExportOptions options = null)
        {
            try
            {
                var json = await BuildBundleJsonAsync(typeFilter: null, options);
                return OperationResult<string>.Succeed(json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置到字符串失败");
                return OperationResult<string>.Failure($"导出失败: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult> ExportConfigsAsync(IEnumerable<IConfig> configs, string filePath, ExportOptions options = null)
        {
            if (configs == null)
                return OperationResult.Failure("配置列表不能为空");
            try
            {
                var list = configs as IList<IConfig> ?? configs.ToList();
                if (list.Count == 0)
                    return OperationResult.Failure("没有可导出的配置");
                var json = BuildBundleJson(list, options);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                _logger?.LogInformation("已导出 {Count} 个配置至: {Path}", list.Count, filePath);
                return OperationResult.Succeed($"已导出 {list.Count} 个配置至 {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置列表失败: {Path}", filePath);
                return OperationResult.Failure($"导出失败: {ex.Message}");
            }
        }

        // ── 导入 ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(string filePath, ImportOptions options = null)
        {
            if (!File.Exists(filePath))
                return new ImportResult().WithFailure("file", $"文件不存在: {filePath}");

            using var stream = File.OpenRead(filePath);
            return await ImportAsync(stream, options);
        }

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(Stream stream, ImportOptions options = null)
        {
            options ??= new ImportOptions();
            var result = new ImportResult();

            // 解析文件
            ConfigExportBundle bundle;
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var json = await reader.ReadToEndAsync();
                bundle = JsonConvert.DeserializeObject<ConfigExportBundle>(json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "解析导入文件失败");
                return result.WithFailure("parse", $"文件格式错误: {ex.Message}");
            }

            if (bundle?.Entries == null || bundle.Entries.Count == 0)
                return result;

            // 构建已注册类型的快速查找表（FullName → Type）
            var typeMap = _configManager.GetRegisteredTypes()
                .ToDictionary(t => t.FullName!, t => t);

            foreach (var entry in bundle.Entries)
            {
                try
                {
                    await ImportEntryAsync(entry, typeMap, options, result);
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Failures[entry.ConfigId ?? "unknown"] = ex.Message;
                    _logger?.LogWarning(ex, "导入条目异常: {Id}", entry.ConfigId);
                }
            }

            _logger?.LogInformation(
                "导入完成: 成功={Imported}, 跳过={Skipped}, 失败={Failed}",
                result.ImportedCount, result.SkippedCount, result.FailureCount);

            return result;
        }

        // ── 私有辅助 ─────────────────────────────────────────────────────────

        private async Task ImportEntryAsync(
            ConfigExportEntry entry,
            Dictionary<string, Type> typeMap,
            ImportOptions options,
            ImportResult result)
        {
            // 类型过滤
            if (options.TypeFilter != null
                && !options.TypeFilter.Any(t => t.FullName == entry.TypeName))
            {
                result.SkippedCount++;
                result.SkippedIds.Add(entry.ConfigId ?? entry.TypeName);
                return;
            }

            // 类型解析
            if (!typeMap.TryGetValue(entry.TypeName, out var resolvedType))
            {
                result.FailureCount++;
                result.Failures[entry.ConfigId ?? entry.TypeName] =
                    $"未找到已注册的类型: {entry.TypeName}";
                _logger?.LogWarning("导入时未找到类型: {TypeName}", entry.TypeName);
                return;
            }

            // 反序列化
            IConfig config;
            try
            {
                config = (IConfig)entry.Data.ToObject(resolvedType)!;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Failures[entry.ConfigId ?? "unknown"] = $"反序列化失败: {ex.Message}";
                return;
            }

            // 验证（可选）
            if (options.ValidateBeforeImport && config is IValidatableConfig validatable)
            {
                var vr = validatable.Validate();
                if (!vr.IsSuccess)
                {
                    result.FailureCount++;
                    result.Failures[config.ConfigId] =
                        $"验证失败: {vr.GetErrorSummary()}";
                    return;
                }
            }

            // 冲突检测
            bool exists = !string.IsNullOrEmpty(config.ConfigId)
                && await _configManager.ExistsAsync(resolvedType, config.ConfigId);

            if (exists)
            {
                switch (options.ConflictResolution)
                {
                    case ConflictResolution.Skip:
                        result.SkippedCount++;
                        result.SkippedIds.Add(config.ConfigId);
                        return;

                    case ConflictResolution.KeepBoth:
                        // 生成新 ID 以两者共存
                        if (config is ConfigBase cb)
                            cb.ConfigId = Guid.NewGuid().ToString("N")[..8];
                        break;

                    // ConflictResolution.Overwrite：直接落入下方 SaveAsync
                }
            }

            // 保存
            var saveResult = await _configManager.SaveAsync(config);
            if (saveResult.Success)
            {
                result.ImportedCount++;
                result.ImportedConfigs.Add(config);
            }
            else
            {
                result.FailureCount++;
                result.Failures[config.ConfigId ?? "unknown"] = saveResult.Message;
            }
        }

        /// <summary>
        /// 获取全部或过滤后的配置，序列化为 bundle JSON 字符串。
        /// </summary>
        private async Task<string> BuildBundleJsonAsync(Type typeFilter, ExportOptions options)
        {
            IEnumerable<IConfig> configs;

            if (typeFilter == null)
            {
                var r = await _configManager.GetAllAsync();
                configs = r.Success ? r.Data : Enumerable.Empty<IConfig>();

                // 应用 ExportOptions.TypeFilter（多类型白名单）
                if (options?.TypeFilter != null)
                {
                    var filterSet = new HashSet<Type>(options.TypeFilter);
                    configs = configs.Where(c => filterSet.Contains(c.GetType()));
                }
            }
            else
            {
                // 由调用方（ExportAsync<T>）保证仅传入已注册类型
                var r = await _configManager.GetAllAsync();
                configs = r.Success
                    ? r.Data.Where(c => c.GetType() == typeFilter)
                    : Enumerable.Empty<IConfig>();
            }

            return BuildBundleJson(configs, options);
        }

        /// <summary>
        /// 将配置序列组装为 bundle JSON 字符串。
        /// </summary>
        private static string BuildBundleJson(IEnumerable<IConfig> configs, ExportOptions options)
        {
            var entries = configs.Select(c => new ConfigExportEntry
            {
                TypeName = c.GetType().FullName!,
                ConfigId = c.ConfigId,
                ConfigName = c.ConfigName,
                Data = JObject.FromObject(c)
            }).ToList();

            var bundle = new ConfigExportBundle
            {
                ExportedAt = DateTime.UtcNow,
                Entries = entries
            };

            var formatting = (options?.PrettyPrint ?? true)
                ? Formatting.Indented
                : Formatting.None;

            return JsonConvert.SerializeObject(bundle, formatting);
        }

        // ── 内部数据模型（仅用于文件序列化，不对外暴露）─────────────────────

        private sealed class ConfigExportBundle
        {
            [JsonProperty("exportVersion")]
            public string ExportVersion { get; set; } = "1.0";

            [JsonProperty("exportedAt")]
            public DateTime ExportedAt { get; set; }

            [JsonProperty("entryCount")]
            public int EntryCount => Entries?.Count ?? 0;

            [JsonProperty("entries")]
            public List<ConfigExportEntry> Entries { get; set; } = new();
        }

        private sealed class ConfigExportEntry
        {
            [JsonProperty("typeName")]
            public string TypeName { get; set; }

            [JsonProperty("configId")]
            public string ConfigId { get; set; }

            [JsonProperty("configName")]
            public string ConfigName { get; set; }

            [JsonProperty("data")]
            public JObject Data { get; set; }
        }
    }
}

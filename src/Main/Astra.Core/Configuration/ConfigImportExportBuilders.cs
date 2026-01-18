using Astra.Core.Foundation.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    #region 导入构建器实现

    /// <summary>
    /// 从文件导入的构建器
    /// </summary>
    public class ImportFromFileBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ImportOptions _options;
        private readonly string _filePath;
        private readonly ILogger<ConfigImportExportBuilder> _logger;

        public ImportFromFileBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ImportOptions options,
            string filePath,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _options = options;
            _filePath = filePath;
            _logger = logger;
        }

        /// <summary>
        /// 执行导入并保存到配置管理器
        /// </summary>
        public async Task<ImportResult> AndSaveAsync()
        {
            if (_options.TargetType == null)
            {
                return ImportResult.CreateFailure("未指定目标配置类型");
            }

            var method = typeof(ImportFromFileBuilder).GetMethod(
                nameof(ImportAndSaveInternalGeneric),
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                return ImportResult.CreateFailure("无法找到导入方法");
            }

            var genericMethod = method.MakeGenericMethod(_options.TargetType);
            return await (Task<ImportResult>)genericMethod.Invoke(this, null);
        }

        private async Task<ImportResult> ImportAndSaveInternalGeneric<T>() where T : class, IConfig
        {
            try
            {
                // 1. 从文件导入配置
                var importResult = await _importExportService.ImportFromFileAsync<T>(_filePath);
                if (!importResult.Success)
                {
                    return ImportResult.CreateFailure(importResult.Message);
                }

                var config = importResult.Data;

                // 2. 生成新 ID（如果需要）
                if (_options.GenerateNewId && config is ConfigBase configBase)
                {
                    configBase.SetConfigId(Guid.NewGuid().ToString());
                }

                // 3. 检查冲突
                var exists = await _configManager.ExistsAsync<T>(config.ConfigId);
                if (exists)
                {
                    switch (_options.ConflictResolution)
                    {
                        case ConflictResolution.Skip:
                            return ImportResult.CreateSkipped($"配置 {config.ConfigId} 已存在，跳过导入");
                        case ConflictResolution.Overwrite:
                            await _configManager.UpdateConfigAsync(config);
                            return ImportResult.CreateSuccess($"已覆盖配置: {config.ConfigName}", 1, new List<IConfig> { config });
                        case ConflictResolution.Rename:
                            // 检查配置是否实现了 IClonableConfig 接口
                            if (config is IClonableConfig cloneableConfig)
                            {
                                var clonedConfig = cloneableConfig.Clone() as T;
                                if (clonedConfig != null)
                                {
                                    config = clonedConfig;
                                    if (config is ConfigBase renamedConfig)
                                    {
                                        renamedConfig.SetConfigId(Guid.NewGuid().ToString());
                                    }
                                    await _configManager.AddConfigAsync(config);
                                    return ImportResult.CreateSuccess($"已导入配置（重命名）: {config.ConfigName}", 1, new List<IConfig> { config });
                                }
                            }
                            // 如果配置不支持克隆，返回失败
                            return ImportResult.CreateFailure($"配置类型 {config.GetType().Name} 不支持克隆操作");
                    }
                }

                // 4. 保存到配置管理器
                var addResult = await _configManager.AddConfigAsync(config);
                if (!addResult.Success)
                {
                    return ImportResult.CreateFailure(addResult.Message);
                }

                return ImportResult.CreateSuccess($"成功导入配置: {config.ConfigName}", 1, new List<IConfig> { config });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导入配置失败: {FilePath}", _filePath);
                return ImportResult.CreateFailure($"导入失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 从多个文件导入的构建器
    /// </summary>
    public class ImportFromFilesBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ImportOptions _options;
        private readonly IEnumerable<string> _filePaths;
        private readonly ILogger<ConfigImportExportBuilder> _logger;

        public ImportFromFilesBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ImportOptions options,
            IEnumerable<string> filePaths,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _options = options;
            _filePaths = filePaths;
            _logger = logger;
        }

        /// <summary>
        /// 执行导入并保存到配置管理器
        /// </summary>
        public async Task<ImportResult> AndSaveAsync()
        {
            if (_options.TargetType == null)
            {
                return ImportResult.CreateFailure("未指定目标配置类型");
            }

            var result = new ImportResult { Success = true };

            foreach (var filePath in _filePaths)
            {
                var builder = new ImportFromFileBuilder(_configManager, _importExportService, _options, filePath, _logger);
                var importResult = await builder.AndSaveAsync();
                result.Merge(importResult);
            }

            return result;
        }
    }

    /// <summary>
    /// 从 JSON 字符串导入的构建器
    /// </summary>
    public class ImportFromJsonBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ImportOptions _options;
        private readonly string _json;
        private readonly ILogger<ConfigImportExportBuilder> _logger;

        public ImportFromJsonBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ImportOptions options,
            string json,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _options = options;
            _json = json;
            _logger = logger;
        }

        /// <summary>
        /// 执行导入并保存到配置管理器
        /// </summary>
        public async Task<ImportResult> AndSaveAsync()
        {
            if (_options.TargetType == null)
            {
                return ImportResult.CreateFailure("未指定目标配置类型");
            }

            var method = typeof(ImportFromJsonBuilder).GetMethod(
                nameof(ImportAndSaveInternalGeneric),
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                return ImportResult.CreateFailure("无法找到导入方法");
            }

            var genericMethod = method.MakeGenericMethod(_options.TargetType);
            return await (Task<ImportResult>)genericMethod.Invoke(this, null);
        }

        private async Task<ImportResult> ImportAndSaveInternalGeneric<T>() where T : class, IConfig
        {
            try
            {
                // 从 JSON 导入配置
                var importResult = await _importExportService.ImportFromJsonAsync<T>(_json);
                if (!importResult.Success)
                {
                    return ImportResult.CreateFailure(importResult.Message);
                }

                var config = importResult.Data;

                // 生成新 ID（如果需要）
                if (_options.GenerateNewId && config is ConfigBase configBase)
                {
                    configBase.SetConfigId(Guid.NewGuid().ToString());
                }

                // 保存到配置管理器
                var addResult = await _configManager.AddConfigAsync(config);
                if (!addResult.Success)
                {
                    return ImportResult.CreateFailure(addResult.Message);
                }

                return ImportResult.CreateSuccess($"成功导入配置: {config.ConfigName}", 1, new List<IConfig> { config });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从JSON导入配置失败");
                return ImportResult.CreateFailure($"导入失败: {ex.Message}");
            }
        }
    }

    #endregion

    #region 导出构建器实现

    /// <summary>
    /// 导出单个配置的构建器
    /// </summary>
    public class ExportConfigBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ExportOptions _options;
        private readonly IEnumerable<IConfig> _configs;
        private readonly ILogger<ConfigImportExportBuilder> _logger;

        public ExportConfigBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ExportOptions options,
            IEnumerable<IConfig> configs,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _options = options;
            _configs = configs;
            _logger = logger;
        }

        /// <summary>
        /// 导出到文件
        /// </summary>
        public async Task<ExportResult> ToFileAsync(string filePath)
        {
            var configsList = _configs.ToList();
            if (configsList.Count == 0)
            {
                return ExportResult.CreateFailure("没有可导出的配置");
            }

            if (configsList.Count == 1)
            {
                // 单个配置导出
                return await ExportSingleConfigAsync(configsList[0], filePath);
            }

            // 多个配置导出（使用 ExportConfigsBuilder）
            return await new ExportConfigsBuilder(_configManager, _importExportService, _options, configsList, _logger)
                .ToFileAsync(filePath);
        }

        private async Task<ExportResult> ExportSingleConfigAsync(IConfig config, string filePath)
        {
            try
            {
                var configType = config.ConfigType ?? _options.ConfigType;
                if (configType == null)
                {
                    return ExportResult.CreateFailure("无法确定配置类型");
                }

                // 使用反射调用泛型方法
                var method = typeof(IConfigurationImportExportService).GetMethod(
                    nameof(IConfigurationImportExportService.ExportToFileAsync));

                if (method == null)
                {
                    return ExportResult.CreateFailure("无法找到导出方法");
                }

                var genericMethod = method.MakeGenericMethod(configType);
                var result = await (Task<OperationResult>)genericMethod.Invoke(_importExportService, new object[] { config, filePath });

                if (result.Success)
                {
                    return ExportResult.CreateSuccess($"成功导出配置: {config.ConfigName}", filePath, 1);
                }

                return ExportResult.CreateFailure(result.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置失败");
                return ExportResult.CreateFailure($"导出失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 导出多个配置的构建器
    /// </summary>
    public class ExportConfigsBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ExportOptions _options;
        private readonly IEnumerable<IConfig> _configs;
        private readonly ILogger<ConfigImportExportBuilder> _logger;

        public ExportConfigsBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ExportOptions options,
            IEnumerable<IConfig> configs,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _options = options;
            _configs = configs;
            _logger = logger;
        }

        /// <summary>
        /// 导出到文件
        /// </summary>
        public async Task<ExportResult> ToFileAsync(string filePath)
        {
            var configsList = _configs.ToList();
            if (configsList.Count == 0)
            {
                return ExportResult.CreateFailure("没有可导出的配置");
            }

            try
            {
                switch (_options.Format)
                {
                    case ExportFormat.JsonArray:
                        return await ExportAsJsonArrayAsync(configsList, filePath);

                    case ExportFormat.JsonObject:
                        return await ExportAsJsonObjectAsync(configsList, filePath);

                    case ExportFormat.SingleFile:
                        return await ExportAsSingleFilesAsync(configsList, filePath);

                    default:
                        return ExportResult.CreateFailure($"不支持的导出格式: {_options.Format}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "批量导出配置失败");
                return ExportResult.CreateFailure($"导出失败: {ex.Message}");
            }
        }

        private async Task<ExportResult> ExportAsJsonArrayAsync(List<IConfig> configs, string filePath)
        {
            var jsonOptions = _options.JsonOptions ?? new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var configJsons = new List<string>();
            foreach (var config in configs)
            {
                var configType = config.ConfigType ?? _options.ConfigType;
                if (configType != null)
                {
                    var json = JsonSerializer.Serialize(config, configType, jsonOptions);
                    configJsons.Add(json);
                }
            }

            if (configJsons.Count == 0)
            {
                return ExportResult.CreateFailure("没有成功序列化的配置");
            }

            var combinedJson = $"[{string.Join(",\n", configJsons)}]";

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, combinedJson, System.Text.Encoding.UTF8);

            return ExportResult.CreateSuccess($"成功导出 {configs.Count} 个配置", filePath, configs.Count);
        }

        private async Task<ExportResult> ExportAsJsonObjectAsync(List<IConfig> configs, string filePath)
        {
            // TODO: 实现包含元数据的 JSON 对象格式
            // 临时使用 JsonArray 格式
            return await ExportAsJsonArrayAsync(configs, filePath);
        }

        private async Task<ExportResult> ExportAsSingleFilesAsync(List<IConfig> configs, string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.GetDirectoryName(filePath);
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var successCount = 0;
            var errors = new List<string>();

            foreach (var config in configs)
            {
                try
                {
                    var fileName = $"{config.ConfigId}.json";
                    var configFilePath = Path.Combine(directory, fileName);
                    
                    var exportResult = await new ExportConfigBuilder(
                        _configManager, 
                        _importExportService, 
                        _options, 
                        new IConfig[] { config }, 
                        _logger)
                        .ToFileAsync(configFilePath);

                    if (exportResult.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        errors.Add($"{config.ConfigName}: {exportResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{config.ConfigName}: {ex.Message}");
                }
            }

            if (successCount == configs.Count)
            {
                return ExportResult.CreateSuccess($"成功导出 {successCount} 个配置", directory, successCount);
            }

            var message = $"导出完成：成功 {successCount} 个，失败 {configs.Count - successCount} 个";
            if (errors.Count > 0)
            {
                message += $"\n{string.Join("\n", errors.Take(5))}";
            }

            return ExportResult.CreateFailure(message, errors);
        }
    }

    #endregion
}


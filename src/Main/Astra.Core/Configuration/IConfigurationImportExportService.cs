using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置导入导出服务接口 - 符合单一职责原则（SRP）
    /// 仅负责配置的导入和导出操作
    /// </summary>
    public interface IConfigurationImportExportService
    {
        /// <summary>
        /// 从文件导入配置
        /// </summary>
        Task<OperationResult<T>> ImportFromFileAsync<T>(string filePath, string configId = null) where T : class, IConfig;

        /// <summary>
        /// 批量从文件导入配置
        /// </summary>
        Task<OperationResult<BatchOperationResult>> ImportManyFromFilesAsync<T>(IEnumerable<string> filePaths) where T : class, IConfig;

        /// <summary>
        /// 从JSON字符串导入配置
        /// </summary>
        Task<OperationResult<T>> ImportFromJsonAsync<T>(string json, string configId = null) where T : class, IConfig;

        /// <summary>
        /// 导出配置到文件
        /// </summary>
        Task<OperationResult> ExportToFileAsync<T>(T config, string filePath) where T : class, IConfig;

        /// <summary>
        /// 批量导出配置到目录
        /// </summary>
        Task<OperationResult<BatchOperationResult>> ExportManyToDirectoryAsync<T>(IEnumerable<T> configs, string directoryPath) where T : class, IConfig;

        /// <summary>
        /// 导出配置为JSON字符串
        /// </summary>
        string ExportToJson<T>(T config) where T : class, IConfig;

        /// <summary>
        /// 验证导入的配置
        /// </summary>
        ValidationResult ValidateImport<T>(T config) where T : class, IConfig;
    }

    /// <summary>
    /// 配置导入导出服务实现
    /// 符合单一职责原则，仅负责配置的序列化和反序列化
    /// </summary>
    public class ConfigurationImportExportService : IConfigurationImportExportService
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<ConfigurationImportExportService> _logger;

        public ConfigurationImportExportService(ILogger<ConfigurationImportExportService> logger = null)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        public async Task<OperationResult<T>> ImportFromFileAsync<T>(string filePath, string configId = null) where T : class, IConfig
        {
            try
            {
                _logger?.LogInformation("开始从文件导入配置: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    var error = $"文件不存在: {filePath}";
                    _logger?.LogError(error);
                    return OperationResult<T>.Failure(error);
                }

                var json = await File.ReadAllTextAsync(filePath);
                var result = await ImportFromJsonAsync<T>(json, configId);

                if (result.Success)
                {
                    _logger?.LogInformation("成功从文件导入配置: {ConfigId}", result.Data.ConfigId);
                }

                return result;
            }
            catch (IOException ioEx)
            {
                _logger?.LogError(ioEx, "文件读取失败: {FilePath}", filePath);
                return OperationResult<T>.Failure($"文件读取失败: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导入配置失败: {FilePath}", filePath);
                return OperationResult<T>.Failure($"导入配置失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<BatchOperationResult>> ImportManyFromFilesAsync<T>(IEnumerable<string> filePaths) where T : class, IConfig
        {
            var result = new BatchOperationResult();

            foreach (var filePath in filePaths)
            {
                var importResult = await ImportFromFileAsync<T>(filePath);
                if (importResult.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailureCount++;
                    result.Failures[filePath] = importResult.Message;
                }
            }

            _logger?.LogInformation("批量导入完成: 成功 {SuccessCount}, 失败 {FailureCount}", 
                result.SuccessCount, result.FailureCount);

            return OperationResult<BatchOperationResult>.Succeed(result);
        }

        public async Task<OperationResult<T>> ImportFromJsonAsync<T>(string json, string configId = null) where T : class, IConfig
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return OperationResult<T>.Failure("JSON内容不能为空");
                }

                var config = JsonSerializer.Deserialize<T>(json, _jsonOptions);

                if (config == null)
                {
                    _logger?.LogError("JSON反序列化返回null");
                    return OperationResult<T>.Failure("配置反序列化失败");
                }

                // 如果指定了新的configId，则覆盖（通过反射设置私有字段）
                if (!string.IsNullOrWhiteSpace(configId))
                {
                    var configBase = config as ConfigBase;
                    if (configBase != null)
                    {
                        configBase.SetConfigId(configId);
                    }
                }

                // 验证导入的配置
                var validationResult = ValidateImport(config);
                if (!validationResult.IsSuccess)
                {
                    _logger?.LogWarning("导入配置验证失败: {Message}", validationResult.Message);
                    return OperationResult<T>.Failure($"配置验证失败: {validationResult.Message}");
                }

                return await Task.FromResult(OperationResult<T>.Succeed(config));
            }
            catch (JsonException jsonEx)
            {
                _logger?.LogError(jsonEx, "JSON解析失败");
                return OperationResult<T>.Failure($"JSON解析失败: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从JSON导入配置失败");
                return OperationResult<T>.Failure($"从JSON导入配置失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> ExportToFileAsync<T>(T config, string filePath) where T : class, IConfig
        {
            try
            {
                if (config == null)
                {
                    return OperationResult.Failure("配置对象不能为空");
                }

                _logger?.LogInformation("开始导出配置到文件: {FilePath}", filePath);

                var json = ExportToJson(config);

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger?.LogDebug("创建目录: {Directory}", directory);
                }

                await File.WriteAllTextAsync(filePath, json);
                _logger?.LogInformation("成功导出配置: {ConfigId} -> {FilePath}", config.ConfigId, filePath);

                return OperationResult.Succeed();
            }
            catch (IOException ioEx)
            {
                _logger?.LogError(ioEx, "文件写入失败: {FilePath}", filePath);
                return OperationResult.Failure($"文件写入失败: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置失败: {FilePath}", filePath);
                return OperationResult.Failure($"导出配置失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<BatchOperationResult>> ExportManyToDirectoryAsync<T>(IEnumerable<T> configs, string directoryPath) where T : class, IConfig
        {
            var result = new BatchOperationResult();

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger?.LogDebug("创建导出目录: {Directory}", directoryPath);
            }

            foreach (var config in configs)
            {
                var fileName = $"{config.ConfigId}.json";
                var filePath = Path.Combine(directoryPath, fileName);
                
                var exportResult = await ExportToFileAsync(config, filePath);
                if (exportResult.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailureCount++;
                    result.Failures[config.ConfigId] = exportResult.Message;
                }
            }

            _logger?.LogInformation("批量导出完成: 成功 {SuccessCount}, 失败 {FailureCount}",
                result.SuccessCount, result.FailureCount);

            return OperationResult<BatchOperationResult>.Succeed(result);
        }

        public string ExportToJson<T>(T config) where T : class, IConfig
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                return JsonSerializer.Serialize(config, _jsonOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger?.LogError(jsonEx, "JSON序列化失败");
                throw new ConfigurationException(
                    ConfigErrorCode.JsonSerializeError,
                    "JSON序列化失败",
                    config.ConfigId,
                    jsonEx);
            }
        }

        public ValidationResult ValidateImport<T>(T config) where T : class, IConfig
        {
            if (config == null)
            {
                return ValidationResult.Failure("配置对象为空")
                    .WithErrorCode(ConfigErrorCode.ValidationFailed);
            }

            var result = ValidationResult.Success();

            // 基本验证
            if (string.IsNullOrWhiteSpace(config.ConfigId))
            {
                result.WithError("ConfigId", "配置ID不能为空", ConfigErrorCode.ConfigIdEmpty.ToString());
            }

            if (string.IsNullOrWhiteSpace(config.ConfigName))
            {
                result.WithWarning("ConfigName", "建议设置配置名称");
            }

            // 如果实现了IValidatableConfig，执行自定义验证
            if (config is IValidatableConfig validatable)
            {
                var customValidation = validatable.Validate();
                result.Merge(customValidation);
            }

            return result;
        }
    }
}

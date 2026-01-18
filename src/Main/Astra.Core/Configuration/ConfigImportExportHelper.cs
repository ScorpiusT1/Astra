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
    /// <summary>
    /// 配置导入导出助手类 - 封装复杂的导入导出逻辑
    /// 提高可复用性、可读性和可维护性
    /// </summary>
    public class ConfigImportExportHelper
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ILogger<ConfigImportExportHelper> _logger;

        public ConfigImportExportHelper(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ILogger<ConfigImportExportHelper> logger = null)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _importExportService = importExportService ?? throw new ArgumentNullException(nameof(importExportService));
            _logger = logger;
        }

        /// <summary>
        /// 从文件导入配置到内存（不保存到配置管理器）
        /// 支持单个配置对象和配置数组格式
        /// </summary>
        public async Task<OperationResult<List<IConfig>>> ImportConfigsFromFileAsync(
            string filePath,
            Type targetConfigType,
            bool generateNewId = true)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return OperationResult<List<IConfig>>.Failure($"文件不存在: {filePath}");
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                return await ImportConfigsFromJsonAsync(jsonContent, targetConfigType, generateNewId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从文件导入配置失败: {FilePath}", filePath);
                return OperationResult<List<IConfig>>.Failure($"导入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 JSON 字符串导入配置到内存（不保存到配置管理器）
        /// </summary>
        public async Task<OperationResult<List<IConfig>>> ImportConfigsFromJsonAsync(
            string jsonContent,
            Type targetConfigType,
            bool generateNewId = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    return OperationResult<List<IConfig>>.Failure("JSON 内容不能为空");
                }

                if (targetConfigType == null || !typeof(IConfig).IsAssignableFrom(targetConfigType))
                {
                    return OperationResult<List<IConfig>>.Failure("目标配置类型无效");
                }

                var jsonOptions = CreateJsonOptions();
                var configs = new List<IConfig>();

                // 解析 JSON 文档，支持数组和单个对象格式
                List<string> configJsonStrings = ExtractConfigJsonStrings(jsonContent);

                // 反序列化每个配置
                foreach (var configJson in configJsonStrings)
                {
                    var configResult = DeserializeConfig(configJson, targetConfigType, jsonOptions);
                    if (configResult.Success && configResult.Data != null)
                    {
                        var config = configResult.Data;

                        // 验证类型匹配
                        if (config.ConfigType != targetConfigType)
                        {
                            _logger?.LogWarning("配置类型不匹配: 期望 {ExpectedType}, 实际 {ActualType}",
                                targetConfigType.Name, config.ConfigType.Name);
                            continue;
                        }

                        // 生成新 ID（如果需要）
                        if (generateNewId && config is ConfigBase configBase)
                        {
                            configBase.SetConfigId(Guid.NewGuid().ToString());
                        }

                        // 确保 ConfigTypeName 正确
                        EnsureConfigTypeName(config);

                        configs.Add(config);
                    }
                }

                return OperationResult<List<IConfig>>.Succeed(configs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从 JSON 导入配置失败");
                return OperationResult<List<IConfig>>.Failure($"导入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出配置到文件（JSON 数组格式）
        /// </summary>
        public async Task<OperationResult> ExportConfigsToFileAsync(
            IEnumerable<IConfig> configs,
            string filePath,
            ExportFormat format = ExportFormat.JsonArray)
        {
            try
            {
                var configsList = configs?.ToList() ?? new List<IConfig>();
                if (configsList.Count == 0)
                {
                    return OperationResult.Failure("没有可导出的配置");
                }

                var jsonOptions = CreateJsonOptions();
                string json;

                switch (format)
                {
                    case ExportFormat.JsonArray:
                        json = ExportAsJsonArray(configsList, jsonOptions);
                        break;

                    case ExportFormat.JsonObject:
                        json = ExportAsJsonObject(configsList, jsonOptions);
                        break;

                    default:
                        return OperationResult.Failure($"不支持的导出格式: {format}");
                }

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 写入文件
                await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);

                _logger?.LogInformation("成功导出 {Count} 个配置到: {FilePath}", configsList.Count, filePath);
                return OperationResult.Succeed();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置失败: {FilePath}", filePath);
                return OperationResult.Failure($"导出失败: {ex.Message}");
            }
        }

        #region 私有辅助方法

        /// <summary>
        /// 从 JSON 内容中提取配置 JSON 字符串（支持数组和单个对象）
        /// </summary>
        private List<string> ExtractConfigJsonStrings(string jsonContent)
        {
            var configJsonStrings = new List<string>();

            using (var doc = JsonDocument.Parse(jsonContent))
            {
                var rootElement = doc.RootElement;

                if (rootElement.ValueKind == JsonValueKind.Array)
                {
                    // 数组格式：[{...}, {...}]
                    foreach (var element in rootElement.EnumerateArray())
                    {
                        configJsonStrings.Add(element.GetRawText());
                    }
                }
                else if (rootElement.ValueKind == JsonValueKind.Object)
                {
                    // 单个配置对象：{...}
                    configJsonStrings.Add(rootElement.GetRawText());
                }
            }

            return configJsonStrings;
        }

        /// <summary>
        /// 反序列化配置（支持类型验证）
        /// </summary>
        private OperationResult<IConfig> DeserializeConfig(
            string configJson,
            Type targetConfigType,
            JsonSerializerOptions jsonOptions)
        {
            try
            {
                // 尝试反序列化为目标类型
                var config = JsonSerializer.Deserialize(configJson, targetConfigType, jsonOptions) as IConfig;

                if (config == null)
                {
                    return OperationResult<IConfig>.Failure("反序列化失败：返回 null");
                }

                // 验证配置类型
                if (config.ConfigType != targetConfigType)
                {
                    return OperationResult<IConfig>.Failure(
                        $"配置类型不匹配：期望 {targetConfigType.Name}，实际 {config.ConfigType.Name}");
                }

                return OperationResult<IConfig>.Succeed(config);
            }
            catch (JsonException jsonEx)
            {
                return OperationResult<IConfig>.Failure($"JSON 解析失败: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                return OperationResult<IConfig>.Failure($"反序列化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保 ConfigTypeName 正确设置
        /// </summary>
        private void EnsureConfigTypeName(IConfig config)
        {
            if (config == null) return;

            var expectedTypeName = config.ConfigType.AssemblyQualifiedName 
                ?? config.ConfigType.FullName 
                ?? config.ConfigType.Name;

            if (string.IsNullOrWhiteSpace(config.ConfigTypeName) || config.ConfigTypeName != expectedTypeName)
            {
                config.ConfigTypeName = expectedTypeName;
            }
        }

        /// <summary>
        /// 创建 JSON 序列化选项
        /// </summary>
        private JsonSerializerOptions CreateJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        /// <summary>
        /// 导出为 JSON 数组格式
        /// </summary>
        private string ExportAsJsonArray(List<IConfig> configs, JsonSerializerOptions jsonOptions)
        {
            var configJsons = new List<string>();

            foreach (var config in configs)
            {
                var configType = config.ConfigType;
                if (configType != null)
                {
                    var json = JsonSerializer.Serialize(config, configType, jsonOptions);
                    configJsons.Add(json);
                }
            }

            return $"[{string.Join(",\n", configJsons)}]";
        }

        /// <summary>
        /// 导出为 JSON 对象格式（包含元数据）
        /// </summary>
        private string ExportAsJsonObject(List<IConfig> configs, JsonSerializerOptions jsonOptions)
        {
            // 暂时使用 JSON 数组格式，后续可以扩展为包含元数据的对象格式
            return ExportAsJsonArray(configs, jsonOptions);
        }

        #endregion
    }
}


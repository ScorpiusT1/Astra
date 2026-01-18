using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置导入导出构建器 - Fluent API 风格，提高可读性和易用性
    /// 符合建造者模式，支持链式调用
    /// </summary>
    public class ConfigImportExportBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ILogger<ConfigImportExportBuilder> _logger;

        private ConfigImportExportBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ILogger<ConfigImportExportBuilder> logger = null)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _importExportService = importExportService ?? throw new ArgumentNullException(nameof(importExportService));
            _logger = logger;
        }

        /// <summary>
        /// 创建构建器实例
        /// </summary>
        public static ConfigImportExportBuilder For(IConfigurationManager configManager)
        {
            // 从配置管理器获取导入导出服务
            // 这里简化：假设可以通过扩展方法或内部访问获取
            // 实际使用时需要从 DI 容器获取
            return new ConfigImportExportBuilder(configManager, null);
        }

        /// <summary>
        /// 创建构建器实例（显式传入导入导出服务）
        /// </summary>
        public static ConfigImportExportBuilder For(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ILogger<ConfigImportExportBuilder> logger = null)
        {
            return new ConfigImportExportBuilder(configManager, importExportService, logger);
        }

        /// <summary>
        /// 开始导入操作
        /// </summary>
        public ImportBuilder Import()
        {
            return new ImportBuilder(_configManager, _importExportService, _logger);
        }

        /// <summary>
        /// 开始导出操作
        /// </summary>
        public ExportBuilder Export()
        {
            return new ExportBuilder(_configManager, _importExportService, _logger);
        }
    }

    /// <summary>
    /// 导入构建器 - 链式调用，清晰表达导入意图
    /// </summary>
    public class ImportBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ILogger<ConfigImportExportBuilder> _logger;
        private ImportOptions _options = new ImportOptions();

        public ImportBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _logger = logger;
        }

        /// <summary>
        /// 指定目标配置类型
        /// </summary>
        public ImportBuilder As<T>() where T : class, IConfig
        {
            _options.TargetType = typeof(T);
            return this;
        }

        /// <summary>
        /// 指定目标配置类型（非泛型版本）
        /// </summary>
        public ImportBuilder AsType(Type configType)
        {
            _options.TargetType = configType;
            return this;
        }

        /// <summary>
        /// 设置冲突解决策略
        /// </summary>
        public ImportBuilder WithConflictResolution(ConflictResolution resolution)
        {
            _options.ConflictResolution = resolution;
            return this;
        }

        /// <summary>
        /// 是否在导入前验证
        /// </summary>
        public ImportBuilder ValidateBeforeImport(bool validate = true)
        {
            _options.ValidateBeforeImport = validate;
            return this;
        }

        /// <summary>
        /// 是否生成新的 ConfigId
        /// </summary>
        public ImportBuilder GenerateNewId(bool generate = true)
        {
            _options.GenerateNewId = generate;
            return this;
        }

        /// <summary>
        /// 从文件导入
        /// </summary>
        public ImportFromFileBuilder FromFile(string filePath)
        {
            return new ImportFromFileBuilder(_configManager, _importExportService, _options, filePath, _logger);
        }

        /// <summary>
        /// 从多个文件导入
        /// </summary>
        public ImportFromFilesBuilder FromFiles(IEnumerable<string> filePaths)
        {
            return new ImportFromFilesBuilder(_configManager, _importExportService, _options, filePaths, _logger);
        }

        /// <summary>
        /// 从 JSON 字符串导入
        /// </summary>
        public ImportFromJsonBuilder FromJson(string json)
        {
            return new ImportFromJsonBuilder(_configManager, _importExportService, _options, json, _logger);
        }
    }

    /// <summary>
    /// 导出构建器 - 链式调用，清晰表达导出意图
    /// </summary>
    public class ExportBuilder
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly ILogger<ConfigImportExportBuilder> _logger;
        private ExportOptions _options = new ExportOptions();

        public ExportBuilder(
            IConfigurationManager configManager,
            IConfigurationImportExportService importExportService,
            ILogger<ConfigImportExportBuilder> logger)
        {
            _configManager = configManager;
            _importExportService = importExportService;
            _logger = logger;
        }

        /// <summary>
        /// 指定要导出的配置类型
        /// </summary>
        public ExportBuilder As<T>() where T : class, IConfig
        {
            _options.ConfigType = typeof(T);
            return this;
        }

        /// <summary>
        /// 指定要导出的配置类型（非泛型版本）
        /// </summary>
        public ExportBuilder AsType(Type configType)
        {
            _options.ConfigType = configType;
            return this;
        }

        /// <summary>
        /// 设置导出格式
        /// </summary>
        public ExportBuilder Format(ExportFormat format)
        {
            _options.Format = format;
            return this;
        }

        /// <summary>
        /// 导出单个配置
        /// </summary>
        public ExportConfigBuilder Config(IConfig config)
        {
            return new ExportConfigBuilder(_configManager, _importExportService, _options, new[] { config }, _logger);
        }

        /// <summary>
        /// 导出多个配置
        /// </summary>
        public ExportConfigsBuilder Configs(IEnumerable<IConfig> configs)
        {
            return new ExportConfigsBuilder(_configManager, _importExportService, _options, configs, _logger);
        }

        /// <summary>
        /// 导出所有配置（指定类型）
        /// </summary>
        public async Task<ExportResult> AllAsync<T>() where T : class, IConfig
        {
            var result = await _configManager.GetAllConfigsAsync<T>();
            if (!result.Success || result.Data == null)
            {
                return ExportResult.CreateFailure(result.Message ?? "获取配置失败");
            }

            var configs = result.Data.Cast<IConfig>().ToList();
            _options.ConfigType = typeof(T);

            return await new ExportConfigsBuilder(_configManager, _importExportService, _options, configs, _logger)
                .ToFileAsync(string.Empty);
        }
    }
}


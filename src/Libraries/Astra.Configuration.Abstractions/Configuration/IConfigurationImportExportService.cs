using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置导入导出服务接口
    /// </summary>
    public interface IConfigurationImportExportService
    {
        Task<OperationResult> ExportConfigAsync<T>(string configId, string exportPath) where T : class, IConfig;
        Task<OperationResult<BatchOperationResult>> ExportManyAsync<T>(IEnumerable<string> configIds, string exportDirectory) where T : class, IConfig;
        Task<OperationResult<T>> ImportConfigAsync<T>(string filePath, string configId = null) where T : class, IConfig;

        Task<OperationResult<T>> ImportFromFileAsync<T>(string filePath, string configId = null) where T : class, IConfig;
        Task<OperationResult<T>> ImportFromJsonAsync<T>(string json, string configId = null) where T : class, IConfig;
        Task<OperationResult> ExportToFileAsync<T>(T config, string filePath) where T : class, IConfig;
        Task<OperationResult> ExportManyToDirectoryAsync<T>(IEnumerable<T> configs, string directoryPath) where T : class, IConfig;
    }
}


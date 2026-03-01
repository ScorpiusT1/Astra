using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 统一配置管理器接口
    /// </summary>
    public interface IConfigurationManager
    {
        Task<OperationResult<T>> CreateConfigAsync<T>(string configId, Func<T> factory) where T : class, IConfig;
        Task<OperationResult<T>> CreateDefaultConfigAsync<T>(string configId) where T : class, IConfig, new();
        Task<OperationResult<T>> CreateFromTemplateAsync<T>(string configId, string templateId) where T : class, IConfig;
        Task<OperationResult<T>> CloneConfigAsync<T>(string sourceConfigId, string newConfigId) where T : class, IConfig;
        Task<OperationResult<BatchOperationResult>> CreateManyAsync<T>(Dictionary<string, Func<T>> factories) where T : class, IConfig;

        Task<OperationResult> AddConfigAsync<T>(T config, bool overwriteIfExists = false) where T : class, IConfig;
        Task<OperationResult> AddOrUpdateConfigAsync<T>(T config) where T : class, IConfig;
        Task<OperationResult<BatchOperationResult>> AddManyAsync<T>(IEnumerable<T> configs, bool overwriteIfExists = false) where T : class, IConfig;
        Task<OperationResult<T>> ImportConfigAsync<T>(string filePath, string configId = null) where T : class, IConfig;
        Task<OperationResult<BatchOperationResult>> ImportManyAsync<T>(IEnumerable<string> filePaths) where T : class, IConfig;
        Task<OperationResult<T>> ImportFromJsonAsync<T>(string json, string configId = null) where T : class, IConfig;

        Task<OperationResult<T>> GetConfigAsync<T>(string configId) where T : class, IConfig;
        Task<OperationResult<T>> GetOrCreateConfigAsync<T>(string configId, Func<T> factory) where T : class, IConfig;
        Task<OperationResult<T>> GetOrAddConfigAsync<T>(string configId, T config) where T : class, IConfig;

        Task<OperationResult> SaveConfigAsync<T>(T config) where T : class, IConfig;
        Task<OperationResult> UpdateConfigAsync<T>(T config) where T : class, IConfig;
        Task<OperationResult> UpdateConfigAsync(IConfig config);

        Task<OperationResult> DeleteConfigAsync<T>(string configId) where T : class, IConfig;
        Task<OperationResult> DeleteConfigAsync(IConfig config);
        Task<OperationResult<BatchOperationResult>> DeleteManyAsync<T>(IEnumerable<string> configIds) where T : class, IConfig;

        Task<OperationResult<IEnumerable<T>>> GetAllConfigsAsync<T>() where T : class, IConfig;
        Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync();
        Task<OperationResult<IEnumerable<T>>> QueryConfigsAsync<T>(Func<T, bool> predicate) where T : class, IConfig;

        Task<OperationResult<BatchOperationResult>> SaveManyAsync<T>(IEnumerable<T> configs) where T : class, IConfig;
        Task<OperationResult<BatchOperationResult>> UpdateManyAsync<T>(IEnumerable<T> configs) where T : class, IConfig;

        Task<OperationResult> RegisterProviderAsync<T>(IConfigProvider<T> provider) where T : class, IConfig;
        Task<OperationResult> UnregisterProviderAsync<T>() where T : class, IConfig;
        Task<IConfigProvider<T>> GetProviderAsync<T>() where T : class, IConfig;
        Task<IConfigProvider> GetProviderAsync(Type configType);

        Task<OperationResult> SaveToFileAsync<T>(T config, string filePath) where T : class, IConfig;
        Task<OperationResult<T>> LoadFromFileAsync<T>(string filePath) where T : class, IConfig;
        Task<OperationResult<BatchOperationResult>> ExportManyAsync<T>(IEnumerable<string> configIds, string exportDirectory) where T : class, IConfig;
        Task<OperationResult> ExportConfigAsync<T>(string configId, string exportPath) where T : class, IConfig;

        Task<OperationResult> ValidateConfigAsync<T>(T config) where T : class, IConfig;
        Task<OperationResult> ValidateConfigAsync(IConfig config);

        Task<OperationResult> ReloadConfigAsync<T>(string configId) where T : class, IConfig;
        Task<OperationResult> ReloadConfigAsync(IConfig config);

        Task<OperationResult> InitializeAsync();
        Task<OperationResult> CleanupAsync();
        Task<OperationResult> ClearAllAsync();

        void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;
        void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;
    }
}


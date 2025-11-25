using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    // ==================== 配置管理器核心接口 ====================

    /// <summary>
    /// 统一配置管理器接口
    /// </summary>
    public interface IConfigurationManager
    {
        // ========== 创建配置 ==========

        /// <summary>
        /// 创建新配置（使用工厂方法）
        /// </summary>
        Task<OperationResult<T>> CreateConfigAsync<T>(string configId, Func<T> factory) where T : class, IConfig;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        Task<OperationResult<T>> CreateDefaultConfigAsync<T>(string configId) where T : class, IConfig, new();

        /// <summary>
        /// 从模板创建配置
        /// </summary>
        Task<OperationResult<T>> CreateFromTemplateAsync<T>(string configId, string templateId) where T : class, IConfig;

        /// <summary>
        /// 克隆配置
        /// </summary>
        Task<OperationResult<T>> CloneConfigAsync<T>(string sourceConfigId, string newConfigId) where T : class, IConfig;

        /// <summary>
        /// 批量创建配置
        /// </summary>
        Task<OperationResult<BatchOperationResult>> CreateManyAsync<T>(Dictionary<string, Func<T>> factories) where T : class, IConfig;

        // ========== 添加配置 ==========

        /// <summary>
        /// 添加配置（直接添加已有对象）
        /// </summary>
        Task<OperationResult> AddConfigAsync<T>(T config, bool overwriteIfExists = false) where T : class, IConfig;

        /// <summary>
        /// 添加或更新配置
        /// </summary>
        Task<OperationResult> AddOrUpdateConfigAsync<T>(T config) where T : class, IConfig;

        /// <summary>
        /// 批量添加配置
        /// </summary>
        Task<OperationResult<BatchOperationResult>> AddManyAsync<T>(IEnumerable<T> configs, bool overwriteIfExists = false) where T : class, IConfig;

        /// <summary>
        /// 从文件导入配置
        /// </summary>
        Task<OperationResult<T>> ImportConfigAsync<T>(string filePath, string configId = null) where T : class, IConfig;

        /// <summary>
        /// 批量从文件导入配置
        /// </summary>
        Task<OperationResult<BatchOperationResult>> ImportManyAsync<T>(IEnumerable<string> filePaths) where T : class, IConfig;

        /// <summary>
        /// 从JSON字符串导入配置
        /// </summary>
        Task<OperationResult<T>> ImportFromJsonAsync<T>(string json, string configId = null) where T : class, IConfig;

        // ========== 读取配置 ==========

        /// <summary>
        /// 获取配置（泛型方法）
        /// </summary>
        Task<OperationResult<T>> GetConfigAsync<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 获取或创建配置
        /// </summary>
        Task<OperationResult<T>> GetOrCreateConfigAsync<T>(string configId, Func<T> factory) where T : class, IConfig;

        /// <summary>
        /// 获取或添加配置
        /// </summary>
        Task<OperationResult<T>> GetOrAddConfigAsync<T>(string configId, T config) where T : class, IConfig;

        // ========== 更新配置 ==========

        /// <summary>
        /// 保存配置
        /// </summary>
        Task<OperationResult> SaveConfigAsync<T>(T config) where T : class, IConfig;

        /// <summary>
        /// 更新配置（带验证）
        /// </summary>
        Task<OperationResult> UpdateConfigAsync<T>(T config) where T : class, IConfig;

        // ========== 删除配置 ==========

        /// <summary>
        /// 删除配置
        /// </summary>
        Task<OperationResult> DeleteConfigAsync<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 批量删除配置
        /// </summary>
        Task<OperationResult<BatchOperationResult>> DeleteManyAsync<T>(IEnumerable<string> configIds) where T : class, IConfig;

        // ========== 其他操作 ==========

        /// <summary>
        /// 重新加载配置
        /// </summary>
        Task<OperationResult<T>> ReloadConfigAsync<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 检查配置是否存在
        /// </summary>
        Task<bool> ExistsAsync<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 获取所有指定类型的配置
        /// </summary>
        Task<OperationResult<IEnumerable<T>>> GetAllConfigsAsync<T>() where T : class, IConfig;

        /// <summary>
        /// 获取所有继承自IConfig的配置（不限类型）
        /// </summary>
        /// <returns>所有配置的集合，按类型分组</returns>
        Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync();

        /// <summary>
        /// 获取所有配置的类型信息
        /// </summary>
        /// <returns>配置类型列表</returns>
        Task<OperationResult<IEnumerable<Type>>> GetAllConfigTypesAsync();

        /// <summary>
        /// 导出配置到文件
        /// </summary>
        Task<OperationResult> ExportConfigAsync<T>(string configId, string filePath) where T : class, IConfig;

        /// <summary>
        /// 批量导出配置
        /// </summary>
        Task<OperationResult<BatchOperationResult>> ExportManyAsync<T>(IEnumerable<string> configIds, string directoryPath) where T : class, IConfig;

        /// <summary>
        /// 清除缓存
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 清除指定类型的缓存
        /// </summary>
        void ClearCache<T>() where T : class, IConfig;

        /// <summary>
        /// 订阅配置变更
        /// </summary>
        void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;

        /// <summary>
        /// 取消订阅
        /// </summary>
        void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;

        /// <summary>
        /// 注册配置提供者
        /// </summary>
        void RegisterProvider<T>(IConfigProvider<T> provider) where T : class, IConfig;

        /// <summary>
        /// 注册配置工厂
        /// </summary>
        void RegisterFactory<T>(IConfigFactory<T> factory) where T : class, IConfig;
    }
}

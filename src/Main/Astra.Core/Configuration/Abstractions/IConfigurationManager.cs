using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 配置管理器接口 — 提供配置的增删改查、缓存失效和变更订阅能力。
    ///
    /// 使用模式：
    ///   1. 启动时调用 RegisterProvider&lt;T&gt; 为每种配置类型注册持久化提供者
    ///   2. 使用 GetAsync / GetAllAsync 读取配置
    ///   3. 使用 SaveAsync / DeleteAsync 写入配置（自动更新缓存并通知订阅者）
    ///   4. 使用 Subscribe&lt;T&gt; 监听变更事件，UI 刷新无需轮询
    /// </summary>
    public interface IConfigurationManager
    {
        // ── 注册 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 注册指定类型的配置持久化提供者。
        /// 同一类型可多次注册，后注册的覆盖先前注册的。
        /// </summary>
        void RegisterProvider<T>(IConfigProvider<T> provider) where T : class, IConfig;

        /// <summary>
        /// 使用 JSON 文件存储注册指定类型的配置，无需手动创建 Provider 类。
        /// 若不传目录，则自动使用 <see cref="ConfigPathString.GetConfigDirectory"/> 推断路径。
        /// </summary>
        void RegisterProvider<T>(string configDirectory = null, ConfigProviderOptions<T> options = null) where T : class, IConfig;

        // ── 查询 ─────────────────────────────────────────────────────────────

        Task<OperationResult<T>> GetAsync<T>(string configId) where T : class, IConfig;
        Task<OperationResult<IEnumerable<T>>> GetAllAsync<T>() where T : class, IConfig;
        Task<OperationResult<IEnumerable<IConfig>>> GetAllAsync();
        IEnumerable<Type> GetRegisteredTypes();
        Task<bool> ExistsAsync<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 检查指定运行时类型的配置是否存在（供导入/导出等无法使用泛型的场景使用）。
        /// </summary>
        Task<bool> ExistsAsync(Type configType, string configId);

        // ── 写入 ─────────────────────────────────────────────────────────────

        Task<OperationResult> SaveAsync<T>(T config) where T : class, IConfig;
        Task<OperationResult> SaveAsync(IConfig config);
        Task<OperationResult> DeleteAsync<T>(string configId) where T : class, IConfig;
        Task<OperationResult> DeleteAsync(IConfig config);
        Task<BatchOperationResult> SaveManyAsync<T>(IEnumerable<T> configs) where T : class, IConfig;
        Task<BatchOperationResult> DeleteManyAsync<T>(IEnumerable<string> configIds) where T : class, IConfig;

        // ── 缓存 ─────────────────────────────────────────────────────────────

        void InvalidateCache();
        void InvalidateCache<T>() where T : class, IConfig;

        // ── 变更通知 ─────────────────────────────────────────────────────────

        void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;
        void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;
    }
}

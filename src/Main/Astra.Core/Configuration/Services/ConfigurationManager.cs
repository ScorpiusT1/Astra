using Astra.Core.Foundation.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置管理器 — 统一管理配置的加载、保存、缓存和变更通知。
    ///
    /// 职责范围：
    ///   - 维护 Type → IConfigProvider 的注册表
    ///   - 内存缓存（一次加载，后续从缓存直接返回）
    ///   - 保存/删除后自动更新缓存并通知订阅者
    ///   - 支持 IValidatableConfig 自动验证
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        private readonly ILogger<ConfigurationManager> _logger;

        // Provider 注册表：Type → IConfigProvider（泛型擦除后的基接口）
        private readonly ConcurrentDictionary<Type, IConfigProvider> _providers = new();

        // 内存缓存：key = "TypeFullName:configId"
        private readonly ConcurrentDictionary<string, IConfig> _cache = new();

        // 变更订阅：Type → 回调委托列表
        private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();

        public ConfigurationManager(ILogger<ConfigurationManager> logger = null)
        {
            _logger = logger;
        }

        // ── 注册 ─────────────────────────────────────────────────────────────

        public void RegisterProvider<T>(IConfigProvider<T> provider) where T : class, IConfig
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers[typeof(T)] = provider;
            _logger?.LogInformation("注册配置提供者: {Type}", typeof(T).Name);
        }

        public void RegisterProvider<T>(string configDirectory = null, ConfigProviderOptions<T> options = null) where T : class, IConfig
        {
            var dir = configDirectory ?? ConfigPathString.GetConfigDirectory(typeof(T));
            RegisterProvider(new DefaultJsonConfigProvider<T>(dir, options));
        }

        // ── 查询 ─────────────────────────────────────────────────────────────

        public async Task<OperationResult<T>> GetAsync<T>(string configId) where T : class, IConfig
        {
            try
            {
                var cacheKey = CacheKey(typeof(T), configId);
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return OperationResult<T>.Succeed((T)cached);

                var provider = RequireProvider<T>();
                var result = await provider.LoadAsync(configId);
                result.ThrowIfFailed();

                _cache[cacheKey] = result.Data;
                return result;
            }
            catch (ConfigurationException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取配置失败: {Id}", configId);
                throw new ConfigurationException($"获取配置失败: {ex.Message}", configId, ex);
            }
        }

        public async Task<OperationResult<IEnumerable<T>>> GetAllAsync<T>() where T : class, IConfig
        {
            try
            {
                var provider = RequireProvider<T>();
                var result = await provider.GetAllAsync();
                if (result.Success && result.Data != null)
                {
                    foreach (var cfg in result.Data)
                        _cache[CacheKey(typeof(T), cfg.ConfigId)] = cfg;
                }
                return result;
            }
            catch (ConfigurationException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取所有 {Type} 配置失败", typeof(T).Name);
                throw new ConfigurationException($"获取所有 {typeof(T).Name} 配置失败: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult<IEnumerable<IConfig>>> GetAllAsync()
        {
            var all = new List<IConfig>();
            foreach (var (type, provider) in _providers)
            {
                try
                {
                    var result = await provider.GetAllConfigsAsync();
                    if (result.Success && result.Data != null)
                    {
                        foreach (var cfg in result.Data)
                            _cache[CacheKey(type, cfg.ConfigId)] = cfg;
                        all.AddRange(result.Data);
                    }
                    else
                    {
                        _logger?.LogWarning("从 {Type} Provider 获取配置失败: {Msg}", type.Name, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "从 {Type} Provider 获取配置时发生异常", type.Name);
                }
            }
            return OperationResult<IEnumerable<IConfig>>.Succeed(all);
        }

        public IEnumerable<Type> GetRegisteredTypes()
            => _providers.Keys.ToList();

        public async Task<bool> ExistsAsync<T>(string configId) where T : class, IConfig
        {
            if (_cache.ContainsKey(CacheKey(typeof(T), configId))) return true;
            var provider = GetProvider<T>();
            return provider != null && await provider.ExistsAsync(configId);
        }

        public async Task<bool> ExistsAsync(Type configType, string configId)
        {
            if (_cache.ContainsKey(CacheKey(configType, configId))) return true;
            if (_providers.TryGetValue(configType, out var provider))
                return await provider.ExistsAsync(configId);
            return false;
        }

        // ── 写入 ─────────────────────────────────────────────────────────────

        public async Task<OperationResult> SaveAsync<T>(T config) where T : class, IConfig
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            try
            {
                Validate(config);
                if (config is ConfigBase cb) cb.MarkAsUpdated();

                var provider = RequireProvider<T>();
                var result = await provider.SaveAsync(config);
                result.ThrowIfFailed();

                _cache[CacheKey(typeof(T), config.ConfigId)] = config;
                Publish(config, ConfigChangeType.Updated);
                return result;
            }
            catch (ConfigurationException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存配置失败: {Id}", config?.ConfigId);
                throw new ConfigurationException($"保存配置失败: {ex.Message}", config?.ConfigId, ex);
            }
        }

        public async Task<OperationResult> SaveAsync(IConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            try
            {
                Validate(config);
                if (config is ConfigBase cb) cb.MarkAsUpdated();

                var type = config.GetType();
                if (!_providers.TryGetValue(type, out var provider))
                    throw new ProviderNotRegisteredException(type);

                var result = await provider.SaveConfigAsync(config);
                result.ThrowIfFailed();

                _cache[CacheKey(type, config.ConfigId)] = config;
                Publish(config, ConfigChangeType.Updated);
                return result;
            }
            catch (ConfigurationException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存配置失败: {Id}", config?.ConfigId);
                throw new ConfigurationException($"保存配置失败: {ex.Message}", config?.ConfigId, ex);
            }
        }

        public async Task<OperationResult> DeleteAsync<T>(string configId) where T : class, IConfig
        {
            if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("configId 不能为空", nameof(configId));
            try
            {
                T instance = null;
                try { instance = (await GetAsync<T>(configId)).Data; } catch { }

                var provider = RequireProvider<T>();
                var result = await provider.DeleteAsync(configId);
                result.ThrowIfFailed();

                _cache.TryRemove(CacheKey(typeof(T), configId), out _);
                if (instance != null) Publish(instance, ConfigChangeType.Deleted);
                return OperationResult.Succeed();
            }
            catch (ConfigurationException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除配置失败: {Id}", configId);
                throw new ConfigurationException($"删除配置失败: {ex.Message}", configId, ex);
            }
        }

        public async Task<OperationResult> DeleteAsync(IConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.ConfigId)) return OperationResult.Succeed();

            try
            {
                var type = config.GetType();
                if (!_providers.TryGetValue(type, out var provider))
                    throw new ProviderNotRegisteredException(type);

                if (!await provider.ExistsAsync(config.ConfigId)) return OperationResult.Succeed();

                var result = await provider.DeleteAsync(config.ConfigId);
                result.ThrowIfFailed();

                _cache.TryRemove(CacheKey(type, config.ConfigId), out _);
                Publish(config, ConfigChangeType.Deleted);
                return OperationResult.Succeed();
            }
            catch (ConfigurationException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除配置失败: {Id}", config.ConfigId);
                throw new ConfigurationException($"删除配置失败: {ex.Message}", config.ConfigId, ex);
            }
        }

        public async Task<BatchOperationResult> SaveManyAsync<T>(IEnumerable<T> configs) where T : class, IConfig
        {
            var result = new BatchOperationResult();
            foreach (var cfg in configs)
            {
                try
                {
                    var r = await SaveAsync(cfg);
                    if (r.Success) result.SuccessCount++;
                    else { result.FailureCount++; result.Failures[cfg.ConfigId] = r.Message; }
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Failures[cfg?.ConfigId ?? "unknown"] = ex.Message;
                }
            }
            return result;
        }

        public async Task<BatchOperationResult> DeleteManyAsync<T>(IEnumerable<string> configIds) where T : class, IConfig
        {
            var result = new BatchOperationResult();
            foreach (var id in configIds)
            {
                try
                {
                    var r = await DeleteAsync<T>(id);
                    if (r.Success) result.SuccessCount++;
                    else { result.FailureCount++; result.Failures[id] = r.Message; }
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Failures[id] = ex.Message;
                }
            }
            return result;
        }

        // ── 缓存 ─────────────────────────────────────────────────────────────

        public void InvalidateCache()
        {
            _cache.Clear();
            _logger?.LogInformation("已清空全部配置缓存");
        }

        public void InvalidateCache<T>() where T : class, IConfig
        {
            var prefix = typeof(T).FullName + ":";
            foreach (var key in _cache.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _cache.TryRemove(key, out _);
            _logger?.LogInformation("已清空 {Type} 配置缓存", typeof(T).Name);
        }

        // ── 变更通知 ─────────────────────────────────────────────────────────

        public void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var list = _subscribers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list) list.Add(callback);
        }

        public void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig
        {
            if (callback == null) return;
            if (_subscribers.TryGetValue(typeof(T), out var list))
                lock (list) list.Remove(callback);
        }

        // ── 私有辅助 ─────────────────────────────────────────────────────────

        private static string CacheKey(Type type, string configId) => $"{type.FullName}:{configId}";

        private IConfigProvider<T> GetProvider<T>() where T : class, IConfig
            => _providers.TryGetValue(typeof(T), out var p) ? (IConfigProvider<T>)p : null;

        private IConfigProvider<T> RequireProvider<T>() where T : class, IConfig
            => GetProvider<T>() ?? throw new ProviderNotRegisteredException(typeof(T));

        private static void Validate(IConfig config)
        {
            if (config is IValidatableConfig v)
            {
                var r = v.Validate();
                if (!r.IsSuccess) throw new ConfigValidationException(r);
            }
        }

        private void Publish(IConfig config, ConfigChangeType changeType)
        {
            var runtimeType = config.GetType();

            void InvokeList(List<Delegate> list)
            {
                List<Delegate> snapshot;
                lock (list) snapshot = list.ToList();

                foreach (var d in snapshot)
                {
                    try { d.DynamicInvoke(config, changeType); }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "配置变更通知回调异常 [{Type}/{ChangeType}]", runtimeType.Name, changeType);
                    }
                }
            }

            // 按订阅类型是否可赋值自当前运行时类型派发，使 Subscribe<基类/接口> 能收到子类保存通知，
            // 并兼容插件与主程序各加载一份同名程序集时运行时类型与 Subscribe<具体T> 键不一致的情况。
            foreach (var kv in _subscribers)
            {
                if (!kv.Key.IsAssignableFrom(runtimeType))
                    continue;
                InvokeList(kv.Value);
            }
        }
    }

    ///// <summary>
    ///// OperationResult 扩展 — 失败时抛出 ConfigurationException。
    ///// </summary>
    //public static class OperationResultExtensions
    //{
    //    public static void ThrowIfFailed(this OperationResult result)
    //    {
    //        if (!result.Success) throw new ConfigurationException(result.Message);
    //    }

    //    public static void ThrowIfFailed<T>(this OperationResult<T> result)
    //    {
    //        if (!result.Success) throw new ConfigurationException(result.Message);
    //    }
    //}
}

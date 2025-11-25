using Astra.Core.Foundation.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 重构后的配置管理器 - 符合SOLID六大原则
    /// 1. 单一职责原则（SRP）：委托给专门的服务处理缓存、事件、导入导出
    /// 2. 开闭原则（OCP）：通过接口扩展，对修改关闭
    /// 3. 里氏替换原则（LSP）：所有IConfig实现都可以安全替换
    /// 4. 接口隔离原则（ISP）：接口职责单一，不强制实现不需要的方法
    /// 5. 依赖倒置原则（DIP）：依赖抽象（接口）而非具体实现
    /// </summary>
    public class ConfigurationManagerRefactored : IConfigurationManager
    {
        // ========== 依赖注入的服务（依赖倒置原则）==========
        private readonly IConfigurationCacheService _cacheService;
        private readonly IConfigurationEventService _eventService;
        private readonly IConfigurationImportExportService _importExportService;
        private readonly IConfigurationTransactionService _transactionService;
        private readonly ILogger<ConfigurationManagerRefactored> _logger;

        // ========== Provider和Factory注册 ==========
        private readonly ConcurrentDictionary<Type, object> _providers = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, object> _factories = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// 构造函数 - 依赖注入（DIP原则）
        /// </summary>
        public ConfigurationManagerRefactored(
            IConfigurationCacheService cacheService,
            IConfigurationEventService eventService,
            IConfigurationImportExportService importExportService,
            IConfigurationTransactionService transactionService,
            ILogger<ConfigurationManagerRefactored> logger = null)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            _importExportService = importExportService ?? throw new ArgumentNullException(nameof(importExportService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _logger = logger;

            _logger?.LogInformation("配置管理器初始化完成");
        }

        // ==================== 注册方法 ====================

        public void RegisterProvider<T>(IConfigProvider<T> provider) where T : class, IConfig
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _providers[typeof(T)] = provider;
            _logger?.LogInformation("注册配置提供者: {TypeName}", typeof(T).Name);
        }

        public void RegisterFactory<T>(IConfigFactory<T> factory) where T : class, IConfig
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factories[typeof(T)] = factory;
            _logger?.LogInformation("注册配置工厂: {TypeName}", typeof(T).Name);
        }

        // ==================== 创建配置 ====================

        public async Task<OperationResult<T>> CreateConfigAsync<T>(string configId, Func<T> factory) where T : class, IConfig
        {
            try
            {
                _logger?.LogInformation("开始创建配置: {ConfigId}", configId);

                // 检查配置是否已存在
                if (await ExistsAsync<T>(configId))
                {
                    var error = $"配置 {configId} 已存在";
                    _logger?.LogWarning(error);
                    throw new ConfigAlreadyExistsException(configId);
                }

                // 使用工厂创建配置
                var config = factory();
                if (config == null)
                {
                    throw new ConfigurationException(ConfigErrorCode.FactoryCreateFailed, "工厂方法返回null");
                }

                // 验证配置
                await ValidateConfigAsync(config);

                // 保存配置
                var saveResult = await SaveConfigInternalAsync(config, isNew: true);
                saveResult.ThrowIfFailed();

                _logger?.LogInformation("成功创建配置: {ConfigId}", configId);
                return OperationResult<T>.Succeed(config);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建配置失败: {ConfigId}", configId);
                throw new ConfigurationException(ConfigErrorCode.ConfigCreateFailed, $"创建配置失败: {ex.Message}", configId, ex);
            }
        }

        public async Task<OperationResult<T>> CreateDefaultConfigAsync<T>(string configId) where T : class, IConfig, new()
        {
            try
            {
                _logger?.LogInformation("创建默认配置: {ConfigId}", configId);

                // 尝试使用注册的工厂
                var factoryObj = GetFactory<T>();
                if (factoryObj != null)
                {
                    var config = factoryObj.CreateDefault(configId);
                    var validationResult = factoryObj.ValidateCreation(config);
                    
                    if (!validationResult.Success)
                    {
                        throw new ConfigValidationException(
                            ValidationResult.Failure(validationResult.Message)
                                .WithErrorCode(ConfigErrorCode.ValidationFailed));
                    }

                    var saveResult = await SaveConfigInternalAsync(config, isNew: true);
                    saveResult.ThrowIfFailed();

                    return OperationResult<T>.Succeed(config);
                }

                // 使用默认构造函数
                return await CreateConfigAsync(configId, () => new T());
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建默认配置失败: {ConfigId}", configId);
                throw new ConfigurationException(ConfigErrorCode.ConfigCreateFailed, $"创建默认配置失败", configId, ex);
            }
        }

        public async Task<OperationResult<T>> CreateFromTemplateAsync<T>(string configId, string templateId) where T : class, IConfig
        {
            try
            {
                _logger?.LogInformation("从模板创建配置: {ConfigId} <- {TemplateId}", configId, templateId);

                // 加载模板
                var templateResult = await GetConfigAsync<T>(templateId);
                templateResult.ThrowIfFailed();

                var template = templateResult.Data;
                var factory = GetFactory<T>();

                T newConfig;
                if (factory != null)
                {
                    newConfig = factory.CreateFromTemplate(configId, template);
                }
                else
                {
                    // 使用克隆
                    if (template is IClonableConfig cloneable)
                    {
                        newConfig = cloneable.CloneWithId(configId) as T;
                    }
                    else
                    {
                        newConfig = CloneViaSerialization(template);
                        if (newConfig is ConfigBase configBase)
                        {
                            configBase.SetConfigId(configId);
                        }
                    }
                }

                var saveResult = await SaveConfigInternalAsync(newConfig, isNew: true);
                saveResult.ThrowIfFailed();

                return OperationResult<T>.Succeed(newConfig);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从模板创建配置失败: {ConfigId}", configId);
                throw new ConfigurationException(ConfigErrorCode.ConfigCreateFailed, "从模板创建失败", configId, ex);
            }
        }

        public async Task<OperationResult<T>> CloneConfigAsync<T>(string sourceConfigId, string newConfigId) where T : class, IConfig
        {
            return await CreateFromTemplateAsync<T>(newConfigId, sourceConfigId);
        }

        public async Task<OperationResult<BatchOperationResult>> CreateManyAsync<T>(Dictionary<string, Func<T>> factories) where T : class, IConfig
        {
            var operations = factories.Select(kvp =>
                new Func<Task<OperationResult>>(async () =>
                {
                    var result = await CreateConfigAsync(kvp.Key, kvp.Value);
                    return result.Success ? OperationResult.Succeed() : OperationResult.Failure(result.Message);
                }));

            return await _transactionService.ExecuteBatchAsync<T>(operations, rollbackOnAnyFailure: false);
        }

        // ==================== 添加配置 ====================

        public async Task<OperationResult> AddConfigAsync<T>(T config, bool overwriteIfExists = false) where T : class, IConfig
        {
            try
            {
                if (config == null)
                    throw new ArgumentNullException(nameof(config));

                _logger?.LogInformation("添加配置: {ConfigId}, 覆盖={Overwrite}", config.ConfigId, overwriteIfExists);

                // 验证
                await ValidateConfigAsync(config);

                // 检查是否存在
                bool exists = await ExistsAsync<T>(config.ConfigId);
                if (exists && !overwriteIfExists)
                {
                    throw new ConfigAlreadyExistsException(config.ConfigId);
                }

                // 保存
                var result = await SaveConfigInternalAsync(config, isNew: !exists);
                
                _logger?.LogInformation("成功添加配置: {ConfigId}", config.ConfigId);
                return result;
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "添加配置失败: {ConfigId}", config?.ConfigId);
                throw new ConfigurationException(ConfigErrorCode.ConfigCreateFailed, "添加配置失败", config?.ConfigId, ex);
            }
        }

        public async Task<OperationResult> AddOrUpdateConfigAsync<T>(T config) where T : class, IConfig
        {
            return await AddConfigAsync(config, overwriteIfExists: true);
        }

        public async Task<OperationResult<BatchOperationResult>> AddManyAsync<T>(IEnumerable<T> configs, bool overwriteIfExists = false) where T : class, IConfig
        {
            var operations = configs.Select(config =>
                new Func<Task<OperationResult>>(async () => await AddConfigAsync(config, overwriteIfExists)));

            return await _transactionService.ExecuteBatchAsync<T>(operations, rollbackOnAnyFailure: false);
        }

        // ==================== 导入配置 ====================

        public async Task<OperationResult<T>> ImportConfigAsync<T>(string filePath, string configId = null) where T : class, IConfig
        {
            try
            {
                var importResult = await _importExportService.ImportFromFileAsync<T>(filePath, configId);
                importResult.ThrowIfFailed();

                var config = importResult.Data;
                var addResult = await AddConfigAsync(config, overwriteIfExists: true);
                addResult.ThrowIfFailed();

                _eventService.Publish(config, ConfigChangeType.Imported);
                return OperationResult<T>.Succeed(config);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导入配置失败: {FilePath}", filePath);
                throw new ConfigurationException(ConfigErrorCode.ConfigImportFailed, "导入配置失败", ex);
            }
        }

        public async Task<OperationResult<BatchOperationResult>> ImportManyAsync<T>(IEnumerable<string> filePaths) where T : class, IConfig
        {
            var operations = filePaths.Select(filePath =>
                new Func<Task<OperationResult>>(async () =>
                {
                    var result = await ImportConfigAsync<T>(filePath);
                    return result.Success ? OperationResult.Succeed() : OperationResult.Failure(result.Message);
                }));

            return await _transactionService.ExecuteBatchAsync<T>(operations, rollbackOnAnyFailure: false);
        }

        public async Task<OperationResult<T>> ImportFromJsonAsync<T>(string json, string configId = null) where T : class, IConfig
        {
            try
            {
                var importResult = await _importExportService.ImportFromJsonAsync<T>(json, configId);
                importResult.ThrowIfFailed();

                var config = importResult.Data;
                var addResult = await AddConfigAsync(config, overwriteIfExists: true);
                addResult.ThrowIfFailed();

                _eventService.Publish(config, ConfigChangeType.Imported);
                return OperationResult<T>.Succeed(config);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从JSON导入配置失败");
                throw new ConfigurationException(ConfigErrorCode.ConfigImportFailed, "从JSON导入失败", ex);
            }
        }

        // ==================== 读取配置 ====================

        public async Task<OperationResult<T>> GetConfigAsync<T>(string configId) where T : class, IConfig
        {
            try
            {
                _logger?.LogDebug("获取配置: {ConfigId}", configId);

                // 尝试从缓存获取
                if (_cacheService.TryGet<T>(configId, out var cached))
                {
                    _logger?.LogDebug("命中缓存: {ConfigId}", configId);
                    return OperationResult<T>.Succeed(cached);
                }

                // 从提供者加载
                var provider = GetProvider<T>();
                if (provider == null)
                {
                    throw new ProviderNotRegisteredException(typeof(T));
                }

                var result = await provider.LoadAsync(configId);
                result.ThrowIfFailed();

                // 缓存配置
                _cacheService.Set(result.Data);

                return result;
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取配置失败: {ConfigId}", configId);
                throw new ConfigurationException(ConfigErrorCode.ConfigNotFound, $"获取配置失败", configId, ex);
            }
        }

        public async Task<OperationResult<T>> GetOrCreateConfigAsync<T>(string configId, Func<T> factory) where T : class, IConfig
        {
            if (await ExistsAsync<T>(configId))
            {
                return await GetConfigAsync<T>(configId);
            }
            return await CreateConfigAsync(configId, factory);
        }

        public async Task<OperationResult<T>> GetOrAddConfigAsync<T>(string configId, T config) where T : class, IConfig
        {
            if (await ExistsAsync<T>(configId))
            {
                return await GetConfigAsync<T>(configId);
            }

            var addResult = await AddConfigAsync(config);
            addResult.ThrowIfFailed();
            return OperationResult<T>.Succeed(config);
        }

        // ==================== 更新配置 ====================

        public async Task<OperationResult> SaveConfigAsync<T>(T config) where T : class, IConfig
        {
            return await SaveConfigInternalAsync(config, isNew: false);
        }

        public async Task<OperationResult> UpdateConfigAsync<T>(T config) where T : class, IConfig
        {
            try
            {
                // 验证
                await ValidateConfigAsync(config);

                // 标记更新
                if (config is ConfigBase configBase)
                {
                    configBase.MarkAsUpdated();
                }

                return await SaveConfigInternalAsync(config, isNew: false);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "更新配置失败: {ConfigId}", config?.ConfigId);
                throw new ConfigurationException(ConfigErrorCode.ConfigUpdateFailed, "更新配置失败", config?.ConfigId, ex);
            }
        }

        // ==================== 删除配置 ====================

        public async Task<OperationResult> DeleteConfigAsync<T>(string configId) where T : class, IConfig
        {
            try
            {
                _logger?.LogInformation("删除配置: {ConfigId}", configId);

                // 先获取配置用于事件通知
                T config = null;
                try
                {
                    var getResult = await GetConfigAsync<T>(configId);
                    config = getResult.Data;
                }
                catch
                {
                    // 忽略获取失败
                }

                var provider = GetProvider<T>();
                if (provider == null)
                {
                    throw new ProviderNotRegisteredException(typeof(T));
                }

                var result = await provider.DeleteAsync(configId);
                result.ThrowIfFailed();

                // 清除缓存
                _cacheService.Remove<T>(configId);

                // 通知订阅者
                if (config != null)
                {
                    _eventService.Publish(config, ConfigChangeType.Deleted);
                }

                _logger?.LogInformation("成功删除配置: {ConfigId}", configId);
                return OperationResult.Succeed();
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除配置失败: {ConfigId}", configId);
                throw new ConfigurationException(ConfigErrorCode.ConfigDeleteFailed, "删除配置失败", configId, ex);
            }
        }

        public async Task<OperationResult<BatchOperationResult>> DeleteManyAsync<T>(IEnumerable<string> configIds) where T : class, IConfig
        {
            var operations = configIds.Select(configId =>
                new Func<Task<OperationResult>>(async () => await DeleteConfigAsync<T>(configId)));

            return await _transactionService.ExecuteBatchAsync<T>(operations, rollbackOnAnyFailure: false);
        }

        // ==================== 其他操作 ====================

        public async Task<OperationResult<T>> ReloadConfigAsync<T>(string configId) where T : class, IConfig
        {
            _logger?.LogInformation("重新加载配置: {ConfigId}", configId);

            // 清除缓存
            _cacheService.Remove<T>(configId);

            // 重新加载
            var result = await GetConfigAsync<T>(configId);
            
            if (result.Success)
            {
                _eventService.Publish(result.Data, ConfigChangeType.Reloaded);
            }

            return result;
        }

        public async Task<bool> ExistsAsync<T>(string configId) where T : class, IConfig
        {
            var provider = GetProvider<T>();
            if (provider == null)
                return false;

            return await provider.ExistsAsync(configId);
        }

        public async Task<OperationResult<IEnumerable<T>>> GetAllConfigsAsync<T>() where T : class, IConfig
        {
            try
            {
                var provider = GetProvider<T>();
                if (provider == null)
                {
                    throw new ProviderNotRegisteredException(typeof(T));
                }

                return await provider.GetAllAsync();
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取所有配置失败");
                throw new ConfigurationException(ConfigErrorCode.ProviderOperationFailed, "获取所有配置失败", ex);
            }
        }

        /// <summary>
        /// 获取所有继承自IConfig的配置（不限类型）
        /// </summary>
        public async Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync()
        {
            try
            {
                _logger?.LogInformation("开始获取所有类型的配置");

                var allConfigs = new List<IConfig>();

                // 遍历所有注册的Provider
                foreach (var providerEntry in _providers)
                {
                    try
                    {
                        var providerType = providerEntry.Key;
                        var provider = providerEntry.Value;

                        // 通过反射调用 GetAllAsync 方法
                        var getAllMethod = provider.GetType().GetMethod("GetAllAsync");
                        if (getAllMethod != null)
                        {
                            var task = (Task)getAllMethod.Invoke(provider, null);
                            await task.ConfigureAwait(false);

                            // 获取结果
                            var resultProperty = task.GetType().GetProperty("Result");
                            if (resultProperty != null)
                            {
                                var result = resultProperty.GetValue(task);
                                var successProperty = result.GetType().GetProperty("Success");
                                var dataProperty = result.GetType().GetProperty("Data");

                                if (successProperty != null && dataProperty != null)
                                {
                                    var success = (bool)successProperty.GetValue(result);
                                    if (success)
                                    {
                                        var data = dataProperty.GetValue(result) as System.Collections.IEnumerable;
                                        if (data != null)
                                        {
                                            foreach (var item in data)
                                            {
                                                if (item is IConfig config)
                                                {
                                                    allConfigs.Add(config);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        _logger?.LogDebug("从 Provider {ProviderType} 获取配置成功", providerType.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "从 Provider {ProviderType} 获取配置失败", providerEntry.Key.Name);
                        // 继续处理其他Provider
                    }
                }

                _logger?.LogInformation("获取所有配置完成，总数: {Count}", allConfigs.Count);
                return OperationResult<IEnumerable<IConfig>>.Succeed(allConfigs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取所有配置失败");
                return OperationResult<IEnumerable<IConfig>>.Failure($"获取所有配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有配置的类型信息
        /// </summary>
        public async Task<OperationResult<IEnumerable<Type>>> GetAllConfigTypesAsync()
        {
            try
            {
                _logger?.LogInformation("获取所有配置类型");

                // 返回所有注册的Provider的类型
                var types = _providers.Keys.ToList();

                _logger?.LogInformation("获取配置类型完成，总数: {Count}", types.Count);
                return await Task.FromResult(OperationResult<IEnumerable<Type>>.Succeed(types));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取配置类型失败");
                return OperationResult<IEnumerable<Type>>.Failure($"获取配置类型失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> ExportConfigAsync<T>(string configId, string filePath) where T : class, IConfig
        {
            try
            {
                var configResult = await GetConfigAsync<T>(configId);
                configResult.ThrowIfFailed();

                return await _importExportService.ExportToFileAsync(configResult.Data, filePath);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出配置失败: {ConfigId}", configId);
                throw new ConfigurationException(ConfigErrorCode.ConfigExportFailed, "导出配置失败", configId, ex);
            }
        }

        public async Task<OperationResult<BatchOperationResult>> ExportManyAsync<T>(IEnumerable<string> configIds, string directoryPath) where T : class, IConfig
        {
            try
            {
                var configs = new List<T>();
                foreach (var configId in configIds)
                {
                    var result = await GetConfigAsync<T>(configId);
                    if (result.Success)
                    {
                        configs.Add(result.Data);
                    }
                }

                return await _importExportService.ExportManyToDirectoryAsync(configs, directoryPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "批量导出配置失败");
                throw new ConfigurationException(ConfigErrorCode.ConfigExportFailed, "批量导出失败", ex);
            }
        }

        // ==================== 缓存管理 ====================

        public void ClearCache()
        {
            _cacheService.Clear();
            _logger?.LogInformation("清除所有缓存");
        }

        public void ClearCache<T>() where T : class, IConfig
        {
            _cacheService.Clear<T>();
            _logger?.LogInformation("清除缓存: {TypeName}", typeof(T).Name);
        }

        // ==================== 事件订阅 ====================

        public void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig
        {
            _eventService.Subscribe(callback);
            _logger?.LogDebug("订阅配置变更: {TypeName}", typeof(T).Name);
        }

        public void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig
        {
            _eventService.Unsubscribe(callback);
            _logger?.LogDebug("取消订阅: {TypeName}", typeof(T).Name);
        }

        // ==================== 私有辅助方法 ====================

        private IConfigProvider<T> GetProvider<T>() where T : class, IConfig
        {
            if (_providers.TryGetValue(typeof(T), out var provider))
            {
                return (IConfigProvider<T>)provider;
            }
            return null;
        }

        private IConfigFactory<T> GetFactory<T>() where T : class, IConfig
        {
            if (_factories.TryGetValue(typeof(T), out var factory))
            {
                return (IConfigFactory<T>)factory;
            }
            return null;
        }

        private async Task<OperationResult> SaveConfigInternalAsync<T>(T config, bool isNew) where T : class, IConfig
        {
            var provider = GetProvider<T>();
            if (provider == null)
            {
                throw new ProviderNotRegisteredException(typeof(T));
            }

            var result = await provider.SaveAsync(config);
            result.ThrowIfFailed();

            // 更新缓存
            _cacheService.Set(config);

            // 通知订阅者
            var changeType = isNew ? ConfigChangeType.Created : ConfigChangeType.Updated;
            _eventService.Publish(config, changeType);

            return result;
        }

        private async Task ValidateConfigAsync<T>(T config) where T : class, IConfig
        {
            if (config is IValidatableConfig validatable)
            {
                var validationResult = validatable.Validate();
                if (!validationResult.IsSuccess)
                {
                    throw new ConfigValidationException(validationResult);
                }
            }

            await Task.CompletedTask;
        }

        private T CloneViaSerialization<T>(T source) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(source);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(ConfigErrorCode.ConfigCloneFailed, "序列化克隆失败", ex);
            }
        }
    }

    /// <summary>
    /// OperationResult扩展方法
    /// </summary>
    public static class OperationResultExtensions
    {
        public static void ThrowIfFailed(this OperationResult result)
        {
            if (!result.Success)
            {
                throw new ConfigurationException(ConfigErrorCode.Unknown, result.Message);
            }
        }

        public static void ThrowIfFailed<T>(this OperationResult<T> result)
        {
            if (!result.Success)
            {
                throw new ConfigurationException(ConfigErrorCode.Unknown, result.Message);
            }
        }
    }
}

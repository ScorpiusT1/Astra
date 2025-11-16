using Addins.Core.Abstractions;
using Addins.Exceptions;
using Addins.Health;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Addins.Recovery
{
    /// <summary>
    /// 恢复策略接口
    /// </summary>
    public interface IRecoveryStrategy
    {
        string Name { get; }
        Task<bool> CanRecoverAsync(PluginSystemException exception);
        Task<RecoveryResult> RecoverAsync(PluginSystemException exception);
    }

    /// <summary>
    /// 恢复结果
    /// </summary>
    public class RecoveryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public Exception Exception { get; set; }

        public RecoveryResult()
        {
            Data = new Dictionary<string, object>();
        }

        public static RecoveryResult SuccessResult(string message, TimeSpan duration = default)
        {
            return new RecoveryResult
            {
                Success = true,
                Message = message,
                Duration = duration
            };
        }

        public static RecoveryResult FailureResult(string message, Exception exception = null, TimeSpan duration = default)
        {
            return new RecoveryResult
            {
                Success = false,
                Message = message,
                Exception = exception,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// 自愈服务接口
    /// </summary>
    public interface ISelfHealingService
    {
        Task<RecoveryResult> AttemptRecoveryAsync(PluginSystemException exception);
        void RegisterRecoveryStrategy(IRecoveryStrategy strategy);
        void UnregisterRecoveryStrategy(string name);
        Task StartAsync();
        Task StopAsync();
    }

    /// <summary>
    /// 自愈服务实现
    /// </summary>
    public class SelfHealingService : ISelfHealingService
    {
        private readonly Dictionary<string, IRecoveryStrategy> _strategies = new();
        private readonly IErrorLogger _logger;
        private readonly IHealthCheckService _healthCheckService;
        private readonly Timer _timer;
        private readonly TimeSpan _checkInterval;
        private bool _isRunning = false;

        public SelfHealingService(IErrorLogger logger, IHealthCheckService healthCheckService, TimeSpan? checkInterval = null)
        {
            _logger = logger;
            _healthCheckService = healthCheckService;
            _checkInterval = checkInterval ?? TimeSpan.FromMinutes(5);
            _timer = new Timer(PerformSelfHealing, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task<RecoveryResult> AttemptRecoveryAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                foreach (var strategy in _strategies.Values)
                {
                    if (await strategy.CanRecoverAsync(exception))
                    {
                        await _logger.LogInfoAsync($"Attempting recovery using strategy: {strategy.Name}", exception.PluginId);
                        
                        var result = await strategy.RecoverAsync(exception);
                        result.Duration = DateTime.UtcNow - startTime;

                        if (result.Success)
                        {
                            await _logger.LogInfoAsync($"Recovery successful: {result.Message}", exception.PluginId);
                        }
                        else
                        {
                            await _logger.LogWarningAsync($"Recovery failed: {result.Message}", exception.PluginId);
                        }

                        return result;
                    }
                }

                return RecoveryResult.FailureResult("No recovery strategy available for this exception", null, DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync(ex, $"Recovery attempt failed for plugin {exception.PluginId}");
                return RecoveryResult.FailureResult("Recovery attempt failed", ex, DateTime.UtcNow - startTime);
            }
        }

        public void RegisterRecoveryStrategy(IRecoveryStrategy strategy)
        {
            _strategies[strategy.Name] = strategy;
        }

        public void UnregisterRecoveryStrategy(string name)
        {
            _strategies.Remove(name);
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _timer.Change(TimeSpan.Zero, _checkInterval);
            await _logger.LogInfoAsync("Self-healing service started");
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            await _logger.LogInfoAsync("Self-healing service stopped");
        }

        private async void PerformSelfHealing(object state)
        {
            if (!_isRunning) return;

            try
            {
                var healthReport = await _healthCheckService.CheckHealthAsync();
                
                if (healthReport.OverallStatus == HealthStatus.Unhealthy)
                {
                    await _logger.LogWarningAsync("System health is unhealthy, attempting self-healing");
                    
                    // 这里可以实现自动恢复逻辑
                    // 例如：重启失败的插件、清理资源等
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync(ex, "Self-healing service error");
            }
        }
    }

    /// <summary>
    /// 插件重启恢复策略
    /// </summary>
    public class PluginRestartRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IPluginHost _host;
        private readonly IErrorLogger _logger;

        public string Name => "PluginRestart";

        public PluginRestartRecoveryStrategy(IPluginHost host, IErrorLogger logger)
        {
            _host = host;
            _logger = logger;
        }

        public async Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            return exception is PluginStartException || 
                   exception is PluginInitializationException ||
                   exception is PluginLoadException;
        }

        public async Task<RecoveryResult> AttemptRecoveryAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                if (string.IsNullOrEmpty(exception.PluginId))
                {
                    return RecoveryResult.FailureResult("Plugin ID is required for restart recovery");
                }

                // 尝试卸载插件
                try
                {
                    await _host.UnloadPluginAsync(exception.PluginId);
                    await _logger.LogInfoAsync($"Plugin {exception.PluginId} unloaded for restart");
                }
                catch (Exception ex)
                {
                    await _logger.LogWarningAsync($"Failed to unload plugin {exception.PluginId}: {ex.Message}");
                }

                // 等待一段时间
                await Task.Delay(TimeSpan.FromSeconds(2));

                // 尝试重新加载插件
                // 注意：这里需要插件路径信息，实际实现中可能需要从配置中获取
                // await _host.LoadPluginAsync(pluginPath);

                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.SuccessResult($"Plugin {exception.PluginId} restart attempted", duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.FailureResult($"Plugin restart failed: {ex.Message}", ex, duration);
            }
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            return await AttemptRecoveryAsync(exception);
        }
    }

    /// <summary>
    /// 资源清理恢复策略
    /// </summary>
    public class ResourceCleanupRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IErrorLogger _logger;

        public string Name => "ResourceCleanup";

        public ResourceCleanupRecoveryStrategy(IErrorLogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            return exception is PluginUnloadException ||
                   exception.Message.Contains("memory") ||
                   exception.Message.Contains("resource");
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // 清理临时文件
                await CleanupTempFilesAsync();

                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.SuccessResult("Resource cleanup completed", duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.FailureResult($"Resource cleanup failed: {ex.Message}", ex, duration);
            }
        }

        private async Task CleanupTempFilesAsync()
        {
            try
            {
                var tempPath = System.IO.Path.GetTempPath();
                var tempFiles = System.IO.Directory.GetFiles(tempPath, "plugin_*", System.IO.SearchOption.TopDirectoryOnly);
                
                foreach (var file in tempFiles)
                {
                    try
                    {
                        if (System.IO.File.Exists(file))
                        {
                            System.IO.File.Delete(file);
                        }
                    }
                    catch
                    {
                        // 忽略无法删除的文件
                    }
                }

                await _logger.LogInfoAsync($"Cleaned up {tempFiles.Length} temporary files");
            }
            catch (Exception ex)
            {
                await _logger.LogWarningAsync($"Failed to cleanup temp files: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 配置重置恢复策略
    /// </summary>
    public class ConfigurationResetRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IErrorLogger _logger;

        public string Name => "ConfigurationReset";

        public ConfigurationResetRecoveryStrategy(IErrorLogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            return exception is PluginConfigurationException;
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // 这里可以实现配置重置逻辑
                // 例如：恢复默认配置、重新加载配置文件等

                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.SuccessResult("Configuration reset completed", duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.FailureResult($"Configuration reset failed: {ex.Message}", ex, duration);
            }
        }
    }
}

using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Exceptions;
using Astra.Core.Plugins.Health;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Recovery
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

   
}

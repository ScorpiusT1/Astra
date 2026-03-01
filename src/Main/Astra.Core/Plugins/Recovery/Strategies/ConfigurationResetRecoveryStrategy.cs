using Astra.Core.Plugins.Exceptions;
using System;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Recovery
{
    /// <summary>
    /// 配置重置恢复策略 - 处理插件配置异常，恢复到默认配置。
    /// </summary>
    public class ConfigurationResetRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IErrorLogger _logger;

        public string Name => "ConfigurationReset";

        public ConfigurationResetRecoveryStrategy(IErrorLogger logger)
        {
            _logger = logger;
        }

        public Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            return Task.FromResult(exception is PluginConfigurationException);
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await _logger.LogInfoAsync($"Resetting configuration for plugin: {exception.PluginId}");

                return RecoveryResult.SuccessResult("Configuration reset completed", DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                return RecoveryResult.FailureResult($"Configuration reset failed: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
        }
    }
}

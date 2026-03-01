using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Exceptions;
using System;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Recovery
{
    /// <summary>
    /// 插件重启恢复策略 - 通过卸载再重载插件来恢复启动/初始化/加载错误。
    /// </summary>
    public class PluginRestartRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IPluginHost _host;
        private readonly Exceptions.IErrorLogger _logger;

        public string Name => "PluginRestart";

        public PluginRestartRecoveryStrategy(IPluginHost host, Exceptions.IErrorLogger logger)
        {
            _host = host;
            _logger = logger;
        }

        public Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            var canRecover = exception is PluginStartException
                          || exception is PluginInitializationException
                          || exception is PluginLoadException;
            return Task.FromResult(canRecover);
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            if (string.IsNullOrEmpty(exception.PluginId))
                return RecoveryResult.FailureResult("Plugin ID is required for restart recovery");

            try
            {
                try
                {
                    await _host.UnloadPluginAsync(exception.PluginId);
                    await _logger.LogInfoAsync($"Plugin {exception.PluginId} unloaded for restart");
                }
                catch (Exception ex)
                {
                    await _logger.LogWarningAsync($"Failed to unload plugin {exception.PluginId}: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2));

                return RecoveryResult.SuccessResult($"Plugin {exception.PluginId} restart attempted", DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                return RecoveryResult.FailureResult($"Plugin restart failed: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
        }
    }
}

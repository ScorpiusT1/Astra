using Astra.Core.Plugins.Exceptions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Recovery
{
    /// <summary>
    /// 资源清理恢复策略 - 强制 GC + 清理临时文件，适用于内存/资源类异常。
    /// </summary>
    public class ResourceCleanupRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IErrorLogger _logger;

        public string Name => "ResourceCleanup";

        public ResourceCleanupRecoveryStrategy(IErrorLogger logger)
        {
            _logger = logger;
        }

        public Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            var canRecover = exception is PluginUnloadException
                          || exception.Message.Contains("memory", StringComparison.OrdinalIgnoreCase)
                          || exception.Message.Contains("resource", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(canRecover);
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                await CleanupTempFilesAsync();

                return RecoveryResult.SuccessResult("Resource cleanup completed", DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                return RecoveryResult.FailureResult($"Resource cleanup failed: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
        }

        private async Task CleanupTempFilesAsync()
        {
            try
            {
                var tempFiles = Directory.GetFiles(Path.GetTempPath(), "plugin_*", SearchOption.TopDirectoryOnly);

                foreach (var file in tempFiles)
                {
                    try { File.Delete(file); }
                    catch { }
                }

                await _logger.LogInfoAsync($"Cleaned up {tempFiles.Length} temporary files");
            }
            catch (Exception ex)
            {
                await _logger.LogWarningAsync($"Failed to cleanup temp files: {ex.Message}");
            }
        }
    }
}

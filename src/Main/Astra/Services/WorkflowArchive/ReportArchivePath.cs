using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Configuration;
using Astra.Core.Archiving;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Foundation.Common;
using Microsoft.Extensions.Logging;

namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 解析测试报告/归档根目录（与 <see cref="DefaultWorkflowArchiveService"/> 逻辑一致）。
    /// </summary>
    public static class ReportArchivePath
    {
        /// <inheritdoc cref="ReportArchiveLayout.TestDataFolderName" />
        public const string TestDataFolderName = ReportArchiveLayout.TestDataFolderName;

        /// <summary>
        /// 异步解析根目录；供 UI 等场景使用，避免在 Dispatcher 上阻塞。
        /// </summary>
        public static async Task<string> ResolveRootAsync(
            IConfigurationManager? configurationManager,
            WorkflowArchiveOptions options,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new WorkflowArchiveOptions();

            if (configurationManager != null)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await configurationManager.GetAllAsync<SoftwareConfig>().ConfigureAwait(false);
                    if (result.Success && result.Data != null)
                    {
                        var sc = result.Data.FirstOrDefault();
                        var custom = sc?.ReportOutputRootDirectory?.Trim();
                        if (!string.IsNullOrWhiteSpace(custom))
                        {
                            try
                            {
                                return Path.GetFullPath(custom);
                            }
                            catch (Exception ex)
                            {
                                logger?.LogWarning(ex, "报告根目录无效，将使用程序所在磁盘根目录: {Path}", custom);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "读取软件配置中的报告根目录失败，使用程序所在磁盘根目录。");
                }

                return PathHelper.GetReportDefaultRootDirectory();
            }

            return ResolveFallbackRoot(options, logger);
        }

        /// <summary>
        /// 与 <see cref="SoftwareConfig.ReportOutputRootDirectory"/> 对齐；留空则为程序所在磁盘根目录。
        /// <para>
        /// 实现上整段在线程池执行：在 UI 线程上对异步配置读取做 <c>.GetResult()</c> 仍可能死锁，
        /// 仅靠 <see cref="Task.ConfigureAwait"/> 不足以排除第三方或内部对同步上下文的依赖。
        /// </para>
        /// </summary>
        public static string ResolveRoot(
            IConfigurationManager? configurationManager,
            WorkflowArchiveOptions options,
            ILogger? logger = null)
        {
            try
            {
                return Task.Run(() => ResolveRootAsync(configurationManager, options, logger, default))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (AggregateException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        private static string ResolveFallbackRoot(WorkflowArchiveOptions options, ILogger? logger)
        {
            var fallback = string.IsNullOrWhiteSpace(options.RootDirectory)
                ? PathHelper.GetReportDefaultRootDirectory()
                : options.RootDirectory.Trim();
            try
            {
                return Path.GetFullPath(fallback);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "WorkflowArchiveOptions.RootDirectory 无效，使用程序所在磁盘根目录。");
                return PathHelper.GetReportDefaultRootDirectory();
            }
        }
    }
}

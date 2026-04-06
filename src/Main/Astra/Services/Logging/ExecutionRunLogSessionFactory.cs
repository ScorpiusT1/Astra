using Astra.Core.Logs;
using Astra.Core.Reporting;
using Astra.Engine.Execution.Logging;
using System;
using System.IO;

namespace Astra.Services.Logging
{
    /// <summary>
    /// 在每次托管执行时创建 <see cref="ExecutionRunLogSession"/> 与文件型 ChunkSink。
    /// </summary>
    public sealed class ExecutionRunLogSessionFactory : IExecutionRunLogSessionFactory
    {
        private readonly string _rootDirectory;
        private readonly ICombinedReportCollector? _combinedReportCollector;

        public ExecutionRunLogSessionFactory(string? rootDirectory = null, ICombinedReportCollector? combinedReportCollector = null)
        {
            _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Astra", "RunLogs")
                : rootDirectory.Trim();
            _combinedReportCollector = combinedReportCollector;
        }

        public IExecutionRunLogSession Create(string executionId, string? serialNumber, string? preferredLogDirectory = null, string? workFlowKey = null)
        {
            var snPart = string.IsNullOrWhiteSpace(serialNumber)
                ? "SN_PENDING"
                : ExecutionRunLogFileChunkSink.SanitizeFilePart(serialNumber);
            var execCompact = (executionId ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal);
            if (execCompact.Length > 8)
                execCompact = execCompact.Substring(0, 8);
            if (string.IsNullOrEmpty(execCompact))
                execCompact = "exec";

            var logDir = string.IsNullOrWhiteSpace(preferredLogDirectory)
                ? _rootDirectory
                : preferredLogDirectory.Trim();
            try
            {
                logDir = Path.GetFullPath(logDir);
            }
            catch
            {
                logDir = _rootDirectory;
            }

            Directory.CreateDirectory(logDir);

            // 合并批次：归档服务在首个子流程上设置 SharedOutputDirectory；运行日志固定落在该目录，避免子流程间 preferred 目录不一致。
            if (_combinedReportCollector is { IsActive: true, SharedOutputDirectory: { } sharedOutDir } col)
            {
                string? sharedPath = col.SharedRunLogFilePath;
                if (sharedPath == null)
                {
                    var batchTs = string.IsNullOrWhiteSpace(col.SharedFileTimestamp)
                        ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
                        : col.SharedFileTimestamp!;

                    string dirForBatchLog;
                    try
                    {
                        dirForBatchLog = Path.GetFullPath(sharedOutDir.Trim());
                    }
                    catch
                    {
                        dirForBatchLog = logDir;
                    }

                    Directory.CreateDirectory(dirForBatchLog);
                    sharedPath = col.TryAllocateSharedRunLogFilePath(dirForBatchLog, batchTs, snPart);
                }

                if (!string.IsNullOrWhiteSpace(sharedPath))
                {
                    string fullShared;
                    try
                    {
                        fullShared = Path.GetFullPath(sharedPath.Trim());
                    }
                    catch
                    {
                        fullShared = sharedPath.Trim();
                    }

                    var batchSink = new ExecutionRunLogFileChunkSink(fullShared, executionId, useBatchSharedHub: true, workFlowKey, serialNumber);
                    return new ExecutionRunLogSession(executionId, batchSink);
                }
            }

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            var fileName = $"{snPart}_{ts}_{execCompact}.log";
            var path = Path.Combine(logDir, fileName);
            var sink = new ExecutionRunLogFileChunkSink(path, executionId, useBatchSharedHub: false, workFlowKey, serialNumber);
            return new ExecutionRunLogSession(executionId, sink);
        }
    }
}

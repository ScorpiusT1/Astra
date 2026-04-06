using Astra.Core.Logs;
using System;
using System.IO;
using System.Text;

namespace Astra.Services.Logging
{
    /// <summary>
    /// 单次执行日志落盘：流程边界与节点成块写入；UI 由 <see cref="IExecutionLogSink"/> 链式委托实时输出。
    /// </summary>
    public sealed class ExecutionRunLogFileChunkSink : IExecutionRunLogChunkSink, IRunLogFileRenameSink
    {
        private readonly object _lock = new();
        private StreamWriter? _writer;
        private string _filePath;
        private readonly bool _useBatchSharedHub;
        private readonly string? _batchHubNormalizedPath;
        private bool _disposed;

        public ExecutionRunLogFileChunkSink(string filePath, string executionId, bool useBatchSharedHub = false, string? workFlowKey = null, string? serialNumber = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("executionId required", nameof(executionId));

            _useBatchSharedHub = useBatchSharedHub;
            if (_useBatchSharedHub)
            {
                try
                {
                    _batchHubNormalizedPath = Path.GetFullPath(_filePath.Trim());
                }
                catch
                {
                    _batchHubNormalizedPath = _filePath.Trim();
                }

                ExecutionBatchRunLogFileHub.EnterSession(_batchHubNormalizedPath, executionId, workFlowKey, serialNumber);
                return;
            }

            _batchHubNormalizedPath = null;

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _writer = new StreamWriter(new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            var header = new StringBuilder()
                .AppendLine("# Astra Run Log v1")
                .Append("# sn=").AppendLine(FormatSnForLog(serialNumber))
                .Append("# execution_id=").AppendLine(executionId.Trim())
                .Append("# file=").AppendLine(_filePath)
                .Append("# started=").AppendLine(DateTimeOffset.Now.ToString("O"))
                .ToString();

            WriteUnderLock(header);
        }

        /// <summary>写入日志头注释用的 SN 文本（与界面/全局变量一致，不做文件名净化）；无 SN 时为 NO_SN。</summary>
        internal static string FormatSnForLog(string? serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return "NO_SN";
            var t = serialNumber.Trim().Replace("\r", " ").Replace("\n", " ");
            return t.Length > 256 ? t.Substring(0, 256) : t;
        }

        public void WriteImmediate(string text)
        {
            if (_disposed || string.IsNullOrEmpty(text))
                return;
            WriteUnderLock(text);
        }

        public void WriteNodeBlock(string text)
        {
            if (_disposed || string.IsNullOrEmpty(text))
                return;
            WriteUnderLock(text);
        }

        public void TryRenameWithSerialNumber(string serialNumber)
        {
            if (_disposed || _useBatchSharedHub || string.IsNullOrWhiteSpace(serialNumber))
                return;

            var safeSn = SanitizeFilePart(serialNumber);
            if (string.IsNullOrEmpty(safeSn))
                return;

            lock (_lock)
            {
                if (_writer == null || string.IsNullOrEmpty(_filePath))
                    return;

                var dir = Path.GetDirectoryName(_filePath);
                var ext = Path.GetExtension(_filePath);
                var baseName = Path.GetFileNameWithoutExtension(_filePath);
                var newName = baseName.Contains("SN_PENDING", StringComparison.OrdinalIgnoreCase)
                    ? baseName.Replace("SN_PENDING", safeSn, StringComparison.OrdinalIgnoreCase)
                    : $"{baseName}_{safeSn}";
                var newPath = string.IsNullOrEmpty(dir)
                    ? newName + ext
                    : Path.Combine(dir, newName + ext);

                if (string.Equals(newPath, _filePath, StringComparison.OrdinalIgnoreCase))
                    return;

                try
                {
                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                    if (File.Exists(newPath))
                        File.Delete(newPath);
                    File.Move(_filePath, newPath);
                    _filePath = newPath;
                    _writer = new StreamWriter(new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
                    {
                        AutoFlush = true
                    };
                    var note = $"{DateTimeOffset.Now:O} | INFO | # log_file_renamed_with_sn={safeSn}{Environment.NewLine}";
                    _writer.Write(note);
                    _writer.Flush();
                }
                catch
                {
                    try
                    {
                        _writer = new StreamWriter(new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
                        {
                            AutoFlush = true
                        };
                    }
                    catch
                    {
                        // 忽略
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_useBatchSharedHub && !string.IsNullOrEmpty(_batchHubNormalizedPath))
            {
                ExecutionBatchRunLogFileHub.LeaveSession(_batchHubNormalizedPath);
                return;
            }

            lock (_lock)
            {
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                }
                catch
                {
                    // ignore
                }

                _writer = null;
            }
        }

        private void WriteUnderLock(string text)
        {
            if (_useBatchSharedHub && !string.IsNullOrEmpty(_batchHubNormalizedPath))
            {
                ExecutionBatchRunLogFileHub.Append(_batchHubNormalizedPath, text);
                return;
            }

            lock (_lock)
            {
                if (_writer == null)
                    return;
                _writer.Write(text);
                if (!text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    _writer.WriteLine();
                _writer.Flush();
            }
        }

        public static string SanitizeFilePart(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "UNKNOWN";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(s.Length);
            foreach (var c in s.Trim())
            {
                if (Array.IndexOf(invalid, c) >= 0 || c == ' ')
                    sb.Append('_');
                else
                    sb.Append(c);
            }

            var r = sb.ToString();
            return r.Length > 64 ? r.Substring(0, 64) : r;
        }
    }
}

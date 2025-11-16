using Astra.Core.Logs.ErrorHandling;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Logs.Output
{
    /// <summary>
    /// 文件日志输出实现
    /// 将日志写入文件，支持同步和异步两种方式
    /// 
    /// 设计说明：
    /// - 同时实现 ILogOutput 和 IAsyncLogOutput，支持同步和异步两种方式
    /// - 在异步模式下，Logger 会自动使用 WriteAsync 方法以获得更好的性能
    /// - 在同步模式下，可以使用 Write 方法（但会阻塞线程）
    /// </summary>
    public class FileLogOutput : ILogOutput, IAsyncLogOutput, IDisposable
    {
        private readonly string _filePath;
        private readonly ILoggerErrorHandler _errorHandler;
        private readonly object _lockObject = new object();
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private bool _disposed = false;

        public FileLogOutput(string filePath, ILoggerErrorHandler errorHandler = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            _filePath = filePath;
            _errorHandler = errorHandler ?? new DefaultLoggerErrorHandler();

            // 确保目录存在
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                _errorHandler.OnError("创建日志目录失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 同步写入日志条目
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <remarks>
        /// 注意：同步写入会阻塞线程。在异步模式下，Logger 会自动使用 WriteAsync 方法。
        /// 建议使用异步模式以获得更好的性能。
        /// </remarks>
        public void Write(LogEntry entry)
        {
            if (_disposed)
                return;

            try
            {
                lock (_lockObject)
                {
                    // 同步写入：直接写入格式化后的消息（消息已经在 Logger 中格式化）
                    File.AppendAllText(_filePath, entry.Message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // 文件写入失败时，使用错误处理器
                _errorHandler.OnError("文件日志写入失败", ex);
            }
        }

        public async Task WriteAsync(LogEntry entry)
        {
            if (_disposed)
                return;

            await _asyncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // 异步写入：使用异步方法，避免阻塞线程
                var content = entry.Message + Environment.NewLine;
                await File.AppendAllTextAsync(_filePath, content).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 文件写入失败时，使用错误处理器
                _errorHandler.OnError("文件日志异步写入失败", ex);
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        public void Flush()
        {
            // File.AppendAllText/AppendAllTextAsync 是直接写入，无需刷新
        }

        public Task FlushAsync()
        {
            // File.AppendAllText/AppendAllTextAsync 是直接写入，无需刷新
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _asyncLock?.Dispose();
                _disposed = true;
            }
        }
    }
}


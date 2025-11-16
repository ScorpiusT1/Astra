using Astra.Core.Logs.ErrorHandling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Logs.Output
{
    /// <summary>
    /// 组合日志输出
    /// 支持同时输出到多个目标（文件、控制台等）
    /// </summary>
    public class CompositeLogOutput : ILogOutput, IDisposable
    {
        private readonly List<ILogOutput> _outputs;
        private readonly ILoggerErrorHandler _errorHandler;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public CompositeLogOutput(ILoggerErrorHandler errorHandler = null, params ILogOutput[] outputs)
        {
            if (outputs == null || outputs.Length == 0)
                throw new ArgumentException("至少需要提供一个输出目标", nameof(outputs));

            _outputs = new List<ILogOutput>(outputs);
            _errorHandler = errorHandler ?? new DefaultLoggerErrorHandler();
        }

        public void Write(LogEntry entry)
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                foreach (var output in _outputs)
                {
                    try
                    {
                        output.Write(entry);
                    }
                    catch (Exception ex)
                    {
                        // 某个输出失败不影响其他输出，使用错误处理器记录
                        _errorHandler.OnError($"CompositeLogOutput: 输出失败 - {output.GetType().Name}", ex);
                    }
                }
            }
        }

        public void Flush()
        {
            lock (_lockObject)
            {
                foreach (var output in _outputs)
                {
                    try
                    {
                        output.Flush();
                    }
                    catch (Exception ex)
                    {
                        _errorHandler.OnError($"CompositeLogOutput: 刷新失败 - {output.GetType().Name}", ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    foreach (var output in _outputs)
                    {
                        try
                        {
                            output?.Dispose();
                        }
                        catch
                        {
                            // 忽略释放时的异常
                        }
                    }
                    _outputs.Clear();
                }
                _disposed = true;
            }
        }
    }
}


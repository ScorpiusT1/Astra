using Astra.Core.Plugins.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Exceptions
{
    /// <summary>
    /// 错误日志接口
    /// </summary>
    public interface IErrorLogger
    {
        Task LogErrorAsync(PluginSystemException exception);
        Task LogErrorAsync(Exception exception, string context = null);
        Task LogWarningAsync(string message, string context = null);
        Task LogInfoAsync(string message, string context = null);
        Task<IEnumerable<ErrorLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null);
        Task ClearLogsAsync();
    }

    /// <summary>
    /// 错误日志条目
    /// </summary>
    public class ErrorLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string PluginId { get; set; }
        public string Operation { get; set; }
        public string ExceptionType { get; set; }
        public string StackTrace { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// 控制台错误日志记录器
    /// </summary>
    public class ConsoleErrorLogger : IErrorLogger
    {
        public Task LogErrorAsync(PluginSystemException exception)
        {
            Console.WriteLine($"[ERROR] {exception.Timestamp:yyyy-MM-dd HH:mm:ss} - {exception.GetType().Name}");
            Console.WriteLine($"  Plugin: {exception.PluginId ?? "N/A"}");
            Console.WriteLine($"  Operation: {exception.Operation ?? "N/A"}");
            Console.WriteLine($"  Message: {exception.Message}");
            
            if (exception.InnerException != null)
            {
                Console.WriteLine($"  Inner Exception: {exception.InnerException.Message}");
            }
            
            if (exception.Context.Count > 0)
            {
                Console.WriteLine("  Context:");
                foreach (var kvp in exception.Context)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }
            
            Console.WriteLine($"  Stack Trace: {exception.StackTrace}");
            Console.WriteLine();
            
            return Task.CompletedTask;
        }

        public Task LogErrorAsync(Exception exception, string context = null)
        {
            Console.WriteLine($"[ERROR] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {exception.GetType().Name}");
            Console.WriteLine($"  Context: {context ?? "N/A"}");
            Console.WriteLine($"  Message: {exception.Message}");
            Console.WriteLine($"  Stack Trace: {exception.StackTrace}");
            Console.WriteLine();
            
            return Task.CompletedTask;
        }

        public Task LogWarningAsync(string message, string context = null)
        {
            Console.WriteLine($"[WARNING] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {message}");
            if (!string.IsNullOrEmpty(context))
            {
                Console.WriteLine($"  Context: {context}");
            }
            Console.WriteLine();
            
            return Task.CompletedTask;
        }

        public Task LogInfoAsync(string message, string context = null)
        {
            Console.WriteLine($"[INFO] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {message}");
            if (!string.IsNullOrEmpty(context))
            {
                Console.WriteLine($"  Context: {context}");
            }
            Console.WriteLine();
            
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ErrorLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null)
        {
            // 控制台日志记录器不支持查询历史日志
            return Task.FromResult<IEnumerable<ErrorLogEntry>>(new List<ErrorLogEntry>());
        }

        public Task ClearLogsAsync()
        {
            Console.Clear();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 文件错误日志记录器
    /// </summary>
    public class FileErrorLogger : IErrorLogger
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();

        public FileErrorLogger(string logFilePath = "plugin-errors.log")
        {
            _logFilePath = logFilePath;
        }

        public async Task LogErrorAsync(PluginSystemException exception)
        {
            var entry = new ErrorLogEntry
            {
                Timestamp = exception.Timestamp,
                Level = "ERROR",
                Message = exception.Message,
                PluginId = exception.PluginId,
                Operation = exception.Operation,
                ExceptionType = exception.GetType().Name,
                StackTrace = exception.StackTrace,
                Context = exception.Context,
                Source = "Astra.Core.Plugins"
            };

            await WriteLogEntryAsync(entry);
        }

        public async Task LogErrorAsync(Exception exception, string context = null)
        {
            var entry = new ErrorLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "ERROR",
                Message = exception.Message,
                ExceptionType = exception.GetType().Name,
                StackTrace = exception.StackTrace,
                Context = new Dictionary<string, object> { ["Context"] = context ?? "N/A" },
                Source = "Astra.Core.Plugins"
            };

            await WriteLogEntryAsync(entry);
        }

        public async Task LogWarningAsync(string message, string context = null)
        {
            var entry = new ErrorLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "WARNING",
                Message = message,
                Context = new Dictionary<string, object> { ["Context"] = context ?? "N/A" },
                Source = "Astra.Core.Plugins"
            };

            await WriteLogEntryAsync(entry);
        }

        public async Task LogInfoAsync(string message, string context = null)
        {
            var entry = new ErrorLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "INFO",
                Message = message,
                Context = new Dictionary<string, object> { ["Context"] = context ?? "N/A" },
                Source = "Astra.Core.Plugins"
            };

            await WriteLogEntryAsync(entry);
        }

        public async Task<IEnumerable<ErrorLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null)
        {
            if (!File.Exists(_logFilePath))
                return new List<ErrorLogEntry>();

            var entries = new List<ErrorLogEntry>();
            var lines = await File.ReadAllLinesAsync(_logFilePath);

            foreach (var line in lines)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<ErrorLogEntry>(line);
                    if (entry != null)
                    {
                        if (from.HasValue && entry.Timestamp < from.Value)
                            continue;
                        if (to.HasValue && entry.Timestamp > to.Value)
                            continue;
                        
                        entries.Add(entry);
                    }
                }
                catch
                {
                    // 忽略无效的日志条目
                }
            }

            return entries;
        }

        public async Task ClearLogsAsync()
        {
            if (File.Exists(_logFilePath))
            {
                await File.WriteAllTextAsync(_logFilePath, string.Empty);
            }
        }

        private async Task WriteLogEntryAsync(ErrorLogEntry entry)
        {
            var json = JsonSerializer.Serialize(entry);
            
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }
        }
    }

    /// <summary>
    /// 结构化错误日志记录器
    /// </summary>
    public class StructuredErrorLogger : IErrorLogger
    {
        private readonly IErrorLogger _baseLogger;
        private readonly Dictionary<string, object> _globalContext;

        public StructuredErrorLogger(IErrorLogger baseLogger)
        {
            _baseLogger = baseLogger;
            _globalContext = new Dictionary<string, object>();
        }

        public void AddGlobalContext(string key, object value)
        {
            _globalContext[key] = value;
        }

        public async Task LogErrorAsync(PluginSystemException exception)
        {
            // 添加全局上下文
            foreach (var kvp in _globalContext)
            {
                exception.Context[kvp.Key] = kvp.Value;
            }

            await _baseLogger.LogErrorAsync(exception);
        }

        public async Task LogErrorAsync(Exception exception, string context = null)
        {
            await _baseLogger.LogErrorAsync(exception, context);
        }

        public async Task LogWarningAsync(string message, string context = null)
        {
            await _baseLogger.LogWarningAsync(message, context);
        }

        public async Task LogInfoAsync(string message, string context = null)
        {
            await _baseLogger.LogInfoAsync(message, context);
        }

        public async Task<IEnumerable<ErrorLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null)
        {
            return await _baseLogger.GetErrorLogsAsync(from, to);
        }

        public async Task ClearLogsAsync()
        {
            await _baseLogger.ClearLogsAsync();
        }
    }
}

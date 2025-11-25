using Astra.Core.Logs.Output;
using Astra.Core.Logs.Formatting;
using Astra.Core.Logs.ErrorHandling;
using Astra.Core.Logs.Filters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志器实现
    /// 重构后符合 SOLID 原则，使用依赖注入
    /// </summary>
    public class Logger : ILogger
    {
        private readonly LogConfig _config;
        private readonly ILogOutput _output;
        private readonly ILogFormatter _formatter;
        private readonly ILoggerErrorHandler _errorHandler;
        private readonly Channel<LogEntry> _logChannel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _workerTask;
        private readonly object _eventLock = new object();
        private readonly object _filterLock = new object();
        private readonly List<ILogFilter> _filters = new List<ILogFilter>();
        private readonly LoggerContext _context = new LoggerContext();
        private bool _disposed = false;
        private readonly DateTime _createdTime;

        /// <summary>
        /// 获取配置（用于扩展方法访问 WorkflowId 等）
        /// </summary>
        internal LogConfig Config => _config;

        /// <summary>
        /// 获取创建时间（用于扩展方法计算 duration）
        /// </summary>
        internal DateTime CreatedTime => _createdTime;

        /// <summary>
        /// 获取日志上下文
        /// </summary>
        public LoggerContext Context => _context;

        /// <summary>
        /// 日志事件 - 用于界面更新
        /// 支持多个订阅者
        /// </summary>
        public event EventHandler<LogEntryEventArgs> OnLog;

        /// <summary>
        /// 构造函数（使用依赖注入）
        /// </summary>
        public Logger(
            LogConfig config,
            ILogOutput output = null,
            ILogFormatter formatter = null,
            ILoggerErrorHandler errorHandler = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 验证配置
            var validateResult = _config.Validate();
            if (!validateResult.Success)
            {
                throw new ArgumentException($"配置验证失败: {validateResult.ErrorMessage}", nameof(config));
            }

            _createdTime = DateTime.Now;

            // 使用依赖注入或创建默认实现
            _formatter = formatter ?? new TextLogFormatter(_config.DateTimeFormat);
            _errorHandler = errorHandler ?? new DefaultLoggerErrorHandler();

            // 创建输出目标
            _output = output ?? CreateDefaultOutput();

            // 创建有界通道作为日志队列
            var options = new BoundedChannelOptions(_config.MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _logChannel = Channel.CreateBounded<LogEntry>(options);

            _cts = new CancellationTokenSource();

            // 写入流程头信息
            if (!string.IsNullOrEmpty(_config.FilePath) && _config.WriteWorkflowHeader)
            {
                WriteWorkflowHeader();
            }

            // 启动异步工作线程
            if (_config.AsyncMode)
            {
                _workerTask = Task.Run(() => ProcessLogQueueAsync(_cts.Token));
            }

            // 清理过期日志
            if (!string.IsNullOrEmpty(_config.LogRootDirectory) &&
                _config.RetentionDays > 0 &&
                !string.IsNullOrEmpty(_config.WorkflowId))
            {
                CleanupOldLogs();
            }

            // 根据配置初始化默认过滤器
            InitializeDefaultFilters();
        }

        /// <summary>
        /// 根据配置初始化默认过滤器
        /// </summary>
        private void InitializeDefaultFilters()
        {
            // 级别过滤器（基于配置的 Level）
            AddFilter(new LevelFilter(_config.Level));

            // 分类过滤器（基于配置的 EnabledCategories）
            if (_config.EnabledCategories != null && _config.EnabledCategories.Count > 0)
            {
                AddFilter(new CategoryFilter(_config.EnabledCategories.ToArray()));
            }
        }

        /// <summary>
        /// 添加日志过滤器
        /// </summary>
        /// <param name="filter">过滤器实例</param>
        /// <returns>当前 Logger 实例，支持链式调用</returns>
        public Logger AddFilter(ILogFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            lock (_filterLock)
            {
                _filters.Add(filter);
            }
            return this;
        }

        /// <summary>
        /// 移除日志过滤器
        /// </summary>
        /// <param name="filter">要移除的过滤器实例</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveFilter(ILogFilter filter)
        {
            if (filter == null)
                return false;

            lock (_filterLock)
            {
                return _filters.Remove(filter);
            }
        }

        /// <summary>
        /// 清除所有过滤器
        /// </summary>
        public void ClearFilters()
        {
            lock (_filterLock)
            {
                _filters.Clear();
            }
        }

        /// <summary>
        /// 创建默认输出（根据配置）
        /// </summary>
        private ILogOutput CreateDefaultOutput()
        {
            var outputs = new List<ILogOutput>();

            // 控制台输出
            if (_config.Console)
            {
                outputs.Add(new ConsoleLogOutput(useColor: true));
            }

            // 文件输出（传递错误处理器）
            if (!string.IsNullOrEmpty(_config.FilePath))
            {
                outputs.Add(new FileLogOutput(_config.FilePath, _errorHandler));
            }

            // 如果没有输出，创建一个空输出（避免空引用）
            if (outputs.Count == 0)
            {
                return new NullLogOutput();
            }

            // 如果只有一个输出，直接返回
            if (outputs.Count == 1)
            {
                return outputs[0];
            }

            // 多个输出使用组合模式（传递错误处理器）
            return new CompositeLogOutput(_errorHandler, outputs.ToArray());
        }

        #region ILogger 接口实现

        public void Debug(string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null)
        {
            Log(LogLevel.Debug, category, message, data, null, triggerUIEvent);
        }

        public void Info(string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null)
        {
            Log(LogLevel.Info, category, message, data, null, triggerUIEvent);
        }

        public void Warn(string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null)
        {
            Log(LogLevel.Warning, category, message, data, null, triggerUIEvent);
        }

        public void Error(string message, Exception ex = null, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null)
        {
            Log(LogLevel.Error, category, message, data, ex, triggerUIEvent);
        }

        public void Critical(string message, Exception ex = null, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null)
        {
            Log(LogLevel.Critical, category, message, data, ex, triggerUIEvent);
        }

        public async Task ShutdownAsync()
        {
            if (!_disposed)
            {
                _logChannel.Writer.Complete();

                if (_workerTask != null)
                {
                    await _workerTask;
                }

                // 写入流程尾部信息
                if (!string.IsNullOrEmpty(_config.FilePath) && _config.WriteWorkflowHeader)
                {
                    WriteWorkflowFooter();
                }

                _cts.Cancel();
                _disposed = true;
            }
        }

        #endregion

        #region 私有方法

        private void Log(LogLevel level, LogCategory category, string message, Dictionary<string, object> data = null, Exception ex = null, bool? triggerUIEvent = null)
        {
            // 合并上下文数据
            var mergedData = MergeContextData(data);

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level.ToString().ToUpper(),
                Category = category,
                Logger = _config.Name,
                Message = message,
                Data = mergedData,
                Exception = ex,
                TriggerUIEvent = DetermineTriggerUIEvent(level, category, triggerUIEvent)
            };

            // 使用过滤器进行过滤
            if (!ShouldLog(entry))
                return;

            ProcessLogEntry(entry);
        }

        /// <summary>
        /// 合并上下文数据和传入的数据
        /// </summary>
        private Dictionary<string, object> MergeContextData(Dictionary<string, object> data)
        {
            var contextData = _context.GetAll();
            
            // 如果上下文为空且传入数据也为空，返回 null
            if (contextData.Count == 0 && (data == null || data.Count == 0))
                return null;

            // 如果只有上下文数据，返回上下文的副本
            if (data == null || data.Count == 0)
                return contextData;

            // 合并数据：传入的数据优先级高于上下文数据
            var merged = new Dictionary<string, object>(contextData);
            foreach (var kv in data)
            {
                merged[kv.Key] = kv.Value;
            }

            return merged;
        }

        /// <summary>
        /// 判断是否应该记录日志（使用所有过滤器）
        /// </summary>
        private bool ShouldLog(LogEntry entry)
        {
            if (entry == null)
                return false;

            lock (_filterLock)
            {
                // 所有过滤器都必须通过（AND 逻辑）
                return _filters.Count == 0 || _filters.All(f => f.ShouldLog(entry));
            }
        }

        private bool DetermineTriggerUIEvent(LogLevel level, LogCategory category, bool? triggerUIEvent)
        {
            if (triggerUIEvent.HasValue)
                return triggerUIEvent.Value;

            if (_config.UIEventLevels != null && !_config.UIEventLevels.Contains(level))
                return false;

            if (_config.UIEventCategories != null && !_config.UIEventCategories.Contains(category))
                return false;

            return _config.DefaultTriggerUIEvent;
        }

        private void ProcessLogEntry(LogEntry entry)
        {
            if (_config.AsyncMode)
            {
                _logChannel.Writer.TryWrite(entry);
            }
            else
            {
                WriteLog(entry);
                if (entry.TriggerUIEvent)
                {
                    lock (_eventLock)
                    {
                        OnLog?.Invoke(this, new LogEntryEventArgs(entry));
                    }
                }
            }
        }

        private async Task ProcessLogQueueAsync(CancellationToken token)
        {
            await foreach (var entry in _logChannel.Reader.ReadAllAsync(token))
            {
                try
                {
                    // 在异步模式下，优先使用异步写入方法
                    await WriteLogAsync(entry).ConfigureAwait(false);
                    if (entry.TriggerUIEvent)
                    {
                        lock (_eventLock)
                        {
                            OnLog?.Invoke(this, new LogEntryEventArgs(entry));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _errorHandler.OnError("处理日志队列时发生错误", ex);
                }
            }
        }

        private void WriteLog(LogEntry entry)
        {
            try
            {
                // 使用格式化器格式化日志
                var formatted = _formatter.Format(entry);
                
                // 创建临时条目用于输出（包含格式化后的消息）
                var outputEntry = new LogEntry
                {
                    Timestamp = entry.Timestamp,
                    Level = entry.Level,
                    Category = entry.Category,
                    Logger = entry.Logger,
                    Message = formatted, // 使用格式化后的消息
                    Data = entry.Data,
                    Exception = entry.Exception,
                    NodeInfo = entry.NodeInfo,
                    TriggerUIEvent = entry.TriggerUIEvent
                };

                // 如果输出器支持异步，在同步模式下也使用异步方法（同步等待）
                if (_output is IAsyncLogOutput asyncOutput)
                {
                    // 同步等待异步操作完成
                    asyncOutput.WriteAsync(outputEntry).GetAwaiter().GetResult();
                }
                else
                {
                    _output.Write(outputEntry);
                }
            }
            catch (Exception ex)
            {
                _errorHandler.OnError("写入日志时发生错误", ex);
            }
        }

        private async Task WriteLogAsync(LogEntry entry)
        {
            try
            {
                // 使用格式化器格式化日志
                var formatted = _formatter.Format(entry);
                
                // 创建临时条目用于输出（包含格式化后的消息）
                var outputEntry = new LogEntry
                {
                    Timestamp = entry.Timestamp,
                    Level = entry.Level,
                    Category = entry.Category,
                    Logger = entry.Logger,
                    Message = formatted, // 使用格式化后的消息
                    Data = entry.Data,
                    Exception = entry.Exception,
                    NodeInfo = entry.NodeInfo,
                    TriggerUIEvent = entry.TriggerUIEvent
                };

                // 如果输出器支持异步，使用异步方法；否则使用同步方法
                if (_output is IAsyncLogOutput asyncOutput)
                {
                    await asyncOutput.WriteAsync(outputEntry).ConfigureAwait(false);
                }
                else
                {
                    _output.Write(outputEntry);
                }
            }
            catch (Exception ex)
            {
                _errorHandler.OnError("异步写入日志时发生错误", ex);
            }
        }

        private void WriteWorkflowHeader()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine($"LogStream Workflow Log");
                sb.AppendLine($"Workflow ID: {_config.WorkflowId ?? "N/A"}");
                sb.AppendLine($"Logger ConfigName: {_config.Name}");
                sb.AppendLine($"Start Time: {_createdTime:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Log Level: {_config.Level}");
                sb.AppendLine($"Log File: {_config.FilePath}");

                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var driveLetter = Path.GetPathRoot(appPath);
                sb.AppendLine($"Application Path: {appPath}");
                sb.AppendLine($"Drive: {driveLetter}");

                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                File.WriteAllText(_config.FilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                _errorHandler.OnError("写入流程头信息时发生错误", ex);
            }
        }

        private void WriteWorkflowFooter()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine($"Workflow Completed");
                sb.AppendLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Duration: {(DateTime.Now - _createdTime).TotalSeconds:F2} seconds");
                sb.AppendLine("=".PadRight(80, '='));

                File.AppendAllText(_config.FilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                _errorHandler.OnError("写入流程尾部信息时发生错误", ex);
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                if (string.IsNullOrEmpty(_config.LogRootDirectory))
                    return;

                if (!Directory.Exists(_config.LogRootDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-_config.RetentionDays);
                var dateFolders = Directory.GetDirectories(_config.LogRootDirectory);

                foreach (var dateFolder in dateFolders)
                {
                    var folderName = Path.GetFileName(dateFolder);
                    if (DateTime.TryParseExact(folderName, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out DateTime folderDate))
                    {
                        if (folderDate < cutoffDate)
                        {
                            Directory.Delete(dateFolder, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandler.OnError("清理过期日志时发生错误", ex);
            }
        }

        #endregion

        #region IDisposable 和 IAsyncDisposable

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await ShutdownAsync();
                _cts?.Dispose();
                _output?.Dispose();
                _disposed = true;
            }
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 获取程序所在盘符的默认日志根目录
        /// </summary>
        private static string GetDefaultLogRootDirectory()
        {
            try
            {
                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                string driveRoot = Path.GetPathRoot(appPath);
                return Path.Combine(driveRoot, "Logs", "RunTimeLog");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"获取默认日志目录失败: {ex.Message}，使用备用目录");
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            }
        }

        /// <summary>
        /// 创建默认日志器
        /// </summary>
        public static ILogger Create(string name = "app", LogLevel level = LogLevel.Info)
        {
            var config = new LogConfigBuilder()
                .WithName(name)
                .WithLevel(level)
                .Build();
            return new Logger(config);
        }

        /// <summary>
        /// 创建流程专用日志器
        /// </summary>
        public static ILogger CreateForWorkflow(
            string workflowId,
            string workflowName = null,
            string logRootDirectory = null,
            LogLevel level = LogLevel.Info,
            bool console = true)
        {
            if (string.IsNullOrEmpty(logRootDirectory))
            {
                logRootDirectory = GetDefaultLogRootDirectory();
            }

            var now = DateTime.Now;
            var dateFolder = now.ToString("yyyy-MM-dd");
            var logDirectory = Path.Combine(logRootDirectory, dateFolder);
            var timestamp = now.ToString("HHmmss");
            var fileName = $"workflow_{workflowId}_{timestamp}.log";
            var filePath = Path.Combine(logDirectory, fileName);

            var config = new LogConfigBuilder()
                .ForWorkflow(workflowId, workflowName)
                .WithLevel(level)
                .WithConsole(console)
                .WithFile(filePath)
                .WithLogRootDirectory(logRootDirectory)
                .Build();

            return new Logger(config);
        }

        #endregion
    }

    /// <summary>
    /// 空日志输出（用于测试或禁用输出）
    /// </summary>
    internal class NullLogOutput : ILogOutput
    {
        public void Write(LogEntry entry) { }
        public void Flush() { }
        public void Dispose() { }
    }
}


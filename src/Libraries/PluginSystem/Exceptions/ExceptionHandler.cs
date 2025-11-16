using Addins.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Addins.Exceptions
{
    /// <summary>
    /// 异常处理策略
    /// </summary>
    public enum ExceptionHandlingStrategy
    {
        /// <summary>
        /// 忽略异常，继续执行
        /// </summary>
        Ignore,
        /// <summary>
        /// 记录异常并继续
        /// </summary>
        LogAndContinue,
        /// <summary>
        /// 重试操作
        /// </summary>
        Retry,
        /// <summary>
        /// 回退到备用方案
        /// </summary>
        Fallback,
        /// <summary>
        /// 停止插件
        /// </summary>
        StopPlugin,
        /// <summary>
        /// 停止整个系统
        /// </summary>
        StopSystem,
        /// <summary>
        /// 抛出异常
        /// </summary>
        Throw
    }

    /// <summary>
    /// 异常处理配置
    /// </summary>
    public class ExceptionHandlingConfig
    {
        public ExceptionHandlingStrategy Strategy { get; set; } = ExceptionHandlingStrategy.LogAndContinue;
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
        public double BackoffMultiplier { get; set; } = 2.0;
        public bool EnableCircuitBreaker { get; set; } = true;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public Func<Exception, bool> ShouldRetry { get; set; }
        public Func<Exception, Task> FallbackAction { get; set; }
    }

    /// <summary>
    /// 异常处理器接口
    /// </summary>
    public interface IExceptionHandler
    {
        Task<T> HandleAsync<T>(Func<Task<T>> operation, string operationName, string pluginId = null, ExceptionHandlingConfig config = null);
        Task HandleAsync(Func<Task> operation, string operationName, string pluginId = null, ExceptionHandlingConfig config = null);
        void RegisterHandler<T>(Func<T, Task> handler) where T : PluginSystemException;
        void RegisterRecoveryAction<T>(Func<T, Task> recoveryAction) where T : PluginSystemException;
    }

    /// <summary>
    /// 异常处理器实现
    /// </summary>
    public class ExceptionHandler : IExceptionHandler
    {
        private readonly Dictionary<Type, Func<PluginSystemException, Task>> _handlers = new();
        private readonly Dictionary<Type, Func<PluginSystemException, Task>> _recoveryActions = new();
        private readonly IErrorLogger _logger;
        private readonly CircuitBreaker _circuitBreaker;

        public ExceptionHandler(IErrorLogger logger = null)
        {
            _logger = logger ?? new ConsoleErrorLogger();
            _circuitBreaker = new CircuitBreaker();
        }

        public async Task<T> HandleAsync<T>(Func<Task<T>> operation, string operationName, string pluginId = null, ExceptionHandlingConfig config = null)
        {
            config ??= new ExceptionHandlingConfig();
            
            for (int attempt = 0; attempt <= config.MaxRetryAttempts; attempt++)
            {
                try
                {
                    if (config.EnableCircuitBreaker && _circuitBreaker.IsOpen)
                    {
                        throw new PluginSystemFatalException("Circuit breaker is open");
                    }

                    var result = await operation();
                    
                    if (config.EnableCircuitBreaker)
                    {
                        _circuitBreaker.RecordSuccess();
                    }
                    
                    return result;
                }
                catch (Exception ex) when (ex is PluginSystemException)
                {
                    var pluginEx = ex as PluginSystemException;
                    pluginEx.Context["OperationName"] = operationName;
                    pluginEx.Context["Attempt"] = attempt;
                    pluginEx.Context["MaxAttempts"] = config.MaxRetryAttempts;

                    await HandlePluginException(pluginEx, config, attempt);
                    
                    if (attempt == config.MaxRetryAttempts)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    var pluginEx = new PluginSystemFatalException($"Unexpected error in {operationName}", ex);
                    pluginEx.Context["PluginId"] = pluginId;
                    pluginEx.Context["Operation"] = operationName;
                    pluginEx.Context["Attempt"] = attempt;
                    pluginEx.Context["MaxAttempts"] = config.MaxRetryAttempts;

                    await HandlePluginException(pluginEx, config, attempt);
                    
                    if (attempt == config.MaxRetryAttempts)
                    {
                        throw pluginEx;
                    }
                }

                if (attempt < config.MaxRetryAttempts)
                {
                    var delay = CalculateRetryDelay(attempt, config);
                    await Task.Delay(delay);
                }
            }

            throw new PluginSystemFatalException($"Operation {operationName} failed after {config.MaxRetryAttempts} attempts");
        }

        public async Task HandleAsync(Func<Task> operation, string operationName, string pluginId = null, ExceptionHandlingConfig config = null)
        {
            await HandleAsync(async () =>
            {
                await operation();
                return true;
            }, operationName, pluginId, config);
        }

        public void RegisterHandler<T>(Func<T, Task> handler) where T : PluginSystemException
        {
            _handlers[typeof(T)] = ex => handler((T)ex);
        }

        public void RegisterRecoveryAction<T>(Func<T, Task> recoveryAction) where T : PluginSystemException
        {
            _recoveryActions[typeof(T)] = ex => recoveryAction((T)ex);
        }

        private async Task HandlePluginException(PluginSystemException ex, ExceptionHandlingConfig config, int attempt)
        {
            // 记录异常
            await _logger.LogErrorAsync(ex);

            // 执行自定义处理器
            if (_handlers.TryGetValue(ex.GetType(), out var handler))
            {
                await handler(ex);
            }

            // 执行恢复操作
            if (_recoveryActions.TryGetValue(ex.GetType(), out var recoveryAction))
            {
                await recoveryAction(ex);
            }

            // 执行回退操作
            if (config.FallbackAction != null)
            {
                await config.FallbackAction(ex);
            }

            // 记录到熔断器
            if (config.EnableCircuitBreaker)
            {
                _circuitBreaker.RecordFailure();
            }
        }

        private TimeSpan CalculateRetryDelay(int attempt, ExceptionHandlingConfig config)
        {
            var delay = TimeSpan.FromMilliseconds(
                config.RetryDelay.TotalMilliseconds * Math.Pow(config.BackoffMultiplier, attempt));
            
            return delay > config.MaxRetryDelay ? config.MaxRetryDelay : delay;
        }
    }

    /// <summary>
    /// 熔断器模式实现
    /// </summary>
    public class CircuitBreaker
    {
        private int _failureCount = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private CircuitBreakerState _state = CircuitBreakerState.Closed;

        public bool IsOpen => _state == CircuitBreakerState.Open;
        public bool IsHalfOpen => _state == CircuitBreakerState.HalfOpen;
        public bool IsClosed => _state == CircuitBreakerState.Closed;

        public void RecordSuccess()
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }

        public void RecordFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            
            if (_failureCount >= 5) // 默认阈值
            {
                _state = CircuitBreakerState.Open;
            }
        }

        public void TryReset()
        {
            if (_state == CircuitBreakerState.Open && 
                DateTime.UtcNow - _lastFailureTime > TimeSpan.FromMinutes(1)) // 默认超时
            {
                _state = CircuitBreakerState.HalfOpen;
            }
        }
    }

    public enum CircuitBreakerState
    {
        Closed,
        Open,
        HalfOpen
    }
}

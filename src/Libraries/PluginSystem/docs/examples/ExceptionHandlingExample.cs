using Addins.Exceptions;
using Addins.Health;
using Addins.Recovery;
using System;
using System.Threading.Tasks;

namespace Addins.Examples
{
    /// <summary>
    /// 异常处理和错误恢复机制使用示例
    /// </summary>
    public class ExceptionHandlingExample
    {
        public static async Task DemonstrateExceptionHandling()
        {
            Console.WriteLine("=== PluginSystem 异常处理机制演示 ===\n");

            // 1. 创建异常处理器
            var logger = new FileErrorLogger("plugin-exceptions.log");
            var exceptionHandler = new ExceptionHandler(logger);

            // 2. 注册自定义异常处理器
            exceptionHandler.RegisterHandler<PluginLoadException>(async ex =>
            {
                Console.WriteLine($"自定义处理器: 插件加载失败 - {ex.PluginId}");
                await logger.LogWarningAsync($"Plugin load failed: {ex.Message}", ex.PluginId);
            });

            // 3. 注册恢复操作
            exceptionHandler.RegisterRecoveryAction<PluginLoadException>(async ex =>
            {
                Console.WriteLine($"恢复操作: 尝试清理资源并重试加载插件 {ex.PluginId}");
                // 这里可以实现具体的恢复逻辑
            });

            // 4. 演示重试机制
            await DemonstrateRetryMechanism(exceptionHandler);

            // 5. 演示熔断器模式
            await DemonstrateCircuitBreaker(exceptionHandler);

            // 6. 演示健康检查
            await DemonstrateHealthCheck();

            // 7. 演示自愈机制
            await DemonstrateSelfHealing();

            Console.WriteLine("\n=== 异常处理机制演示完成 ===");
        }

        private static async Task DemonstrateRetryMechanism(IExceptionHandler exceptionHandler)
        {
            Console.WriteLine("\n--- 重试机制演示 ---");

            var config = new ExceptionHandlingConfig
            {
                Strategy = ExceptionHandlingStrategy.Retry,
                MaxRetryAttempts = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                BackoffMultiplier = 2.0,
                ShouldRetry = ex => ex is PluginLoadException
            };

            try
            {
                await exceptionHandler.HandleAsync(async () =>
                {
                    // 模拟可能失败的操作
                    if (DateTime.Now.Millisecond % 2 == 0)
                    {
                        throw new PluginLoadException("模拟加载失败", "test-plugin", "test.dll");
                    }
                    
                    Console.WriteLine("操作成功执行");
                    return true;
                }, "RetryDemo", "test-plugin", config);
            }
            catch (PluginSystemException ex)
            {
                Console.WriteLine($"重试后仍然失败: {ex.Message}");
            }
        }

        private static async Task DemonstrateCircuitBreaker(IExceptionHandler exceptionHandler)
        {
            Console.WriteLine("\n--- 熔断器模式演示 ---");

            var config = new ExceptionHandlingConfig
            {
                Strategy = ExceptionHandlingStrategy.Retry,
                MaxRetryAttempts = 2,
                EnableCircuitBreaker = true,
                CircuitBreakerThreshold = 3,
                CircuitBreakerTimeout = TimeSpan.FromSeconds(10)
            };

            // 模拟连续失败，触发熔断器
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await exceptionHandler.HandleAsync(async () =>
                    {
                        throw new PluginSystemFatalException("模拟系统错误");
                    }, "CircuitBreakerDemo", config: config);
                }
                catch (PluginSystemException ex)
                {
                    Console.WriteLine($"尝试 {i + 1}: {ex.Message}");
                }
            }
        }

        private static async Task DemonstrateHealthCheck()
        {
            Console.WriteLine("\n--- 健康检查演示 ---");

            var logger = new ConsoleErrorLogger();
            var healthCheckService = new HealthCheckService(logger);

            // 注册系统资源健康检查
            healthCheckService.RegisterHealthCheck(new SystemResourceHealthCheck());

            // 执行健康检查
            var report = await healthCheckService.CheckHealthAsync();
            
            Console.WriteLine($"整体健康状态: {report.OverallStatus}");
            Console.WriteLine($"检查耗时: {report.TotalDuration.TotalMilliseconds}ms");
            
            foreach (var result in report.Results)
            {
                Console.WriteLine($"  {result.Name}: {result.Status} - {result.Message}");
                if (result.Data.Count > 0)
                {
                    foreach (var data in result.Data)
                    {
                        Console.WriteLine($"    {data.Key}: {data.Value}");
                    }
                }
            }
        }

        private static async Task DemonstrateSelfHealing()
        {
            Console.WriteLine("\n--- 自愈机制演示 ---");

            var logger = new ConsoleErrorLogger();
            var healthCheckService = new HealthCheckService(logger);
            var selfHealingService = new SelfHealingService(logger, healthCheckService);

            // 注册恢复策略
            selfHealingService.RegisterRecoveryStrategy(new ResourceCleanupRecoveryStrategy(logger));
            selfHealingService.RegisterRecoveryStrategy(new ConfigurationResetRecoveryStrategy(logger));

            // 模拟异常并尝试恢复
            var exception = new PluginConfigurationException("配置错误", "test-plugin", "database.connection");
            
            var recoveryResult = await selfHealingService.AttemptRecoveryAsync(exception);
            
            Console.WriteLine($"恢复结果: {(recoveryResult.Success ? "成功" : "失败")}");
            Console.WriteLine($"恢复消息: {recoveryResult.Message}");
            Console.WriteLine($"恢复耗时: {recoveryResult.Duration.TotalMilliseconds}ms");
        }
    }

    /// <summary>
    /// 高级异常处理配置示例
    /// </summary>
    public class AdvancedExceptionHandlingExample
    {
        public static ExceptionHandlingConfig CreateRobustConfig()
        {
            return new ExceptionHandlingConfig
            {
                Strategy = ExceptionHandlingStrategy.Retry,
                MaxRetryAttempts = 5,
                RetryDelay = TimeSpan.FromSeconds(2),
                MaxRetryDelay = TimeSpan.FromMinutes(2),
                BackoffMultiplier = 1.5,
                EnableCircuitBreaker = true,
                CircuitBreakerThreshold = 5,
                CircuitBreakerTimeout = TimeSpan.FromMinutes(5),
                ShouldRetry = ex => 
                {
                    // 只对特定异常进行重试
                    return ex is PluginLoadException || 
                           ex is PluginInitializationException ||
                           (ex is PluginSystemException pse && pse.Message.Contains("timeout"));
                },
                FallbackAction = async ex =>
                {
                    // 回退操作：记录错误并尝试清理
                    Console.WriteLine($"执行回退操作: {ex.Message}");
                    // 这里可以实现具体的回退逻辑
                }
            };
        }

        public static async Task DemonstrateCustomRecoveryStrategy()
        {
            Console.WriteLine("\n--- 自定义恢复策略演示 ---");

            var logger = new ConsoleErrorLogger();
            var healthCheckService = new HealthCheckService(logger);
            var selfHealingService = new SelfHealingService(logger, healthCheckService);

            // 注册自定义恢复策略
            selfHealingService.RegisterRecoveryStrategy(new CustomRecoveryStrategy(logger));

            // 测试自定义恢复策略
            var exception = new PluginCommunicationException("通信失败", "test-plugin", "message.topic");
            var result = await selfHealingService.AttemptRecoveryAsync(exception);
            
            Console.WriteLine($"自定义恢复结果: {result.Message}");
        }
    }

    /// <summary>
    /// 自定义恢复策略示例
    /// </summary>
    public class CustomRecoveryStrategy : IRecoveryStrategy
    {
        private readonly IErrorLogger _logger;

        public string Name => "CustomRecovery";

        public CustomRecoveryStrategy(IErrorLogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> CanRecoverAsync(PluginSystemException exception)
        {
            return exception is PluginCommunicationException;
        }

        public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await _logger.LogInfoAsync($"执行自定义恢复策略: {exception.Message}", exception.PluginId);
                
                // 模拟恢复操作
                await Task.Delay(1000);
                
                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.SuccessResult("自定义恢复策略执行成功", duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return RecoveryResult.FailureResult($"自定义恢复策略失败: {ex.Message}", ex, duration);
            }
        }
    }
}

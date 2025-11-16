# PluginSystem 异常处理和错误恢复机制

## 概述

PluginSystem 现在提供了全面的异常处理和错误恢复机制，包括：

- **统一异常处理框架** - 标准化的异常类型和处理策略
- **重试机制** - 自动重试失败的操作
- **熔断器模式** - 防止级联故障
- **健康检查** - 监控系统状态
- **自愈机制** - 自动恢复和修复
- **结构化日志** - 详细的错误记录和分析

## 核心组件

### 1. 异常类型体系

```csharp
// 插件系统异常基类
public abstract class PluginSystemException : Exception
{
    public string PluginId { get; }
    public string Operation { get; }
    public DateTime Timestamp { get; }
    public Dictionary<string, object> Context { get; }
}

// 具体异常类型
- PluginLoadException          // 插件加载异常
- PluginInitializationException // 插件初始化异常
- PluginStartException         // 插件启动异常
- PluginStopException          // 插件停止异常
- PluginUnloadException        // 插件卸载异常
- PluginValidationException    // 插件验证异常
- PluginDependencyException    // 插件依赖异常
- PluginPermissionException    // 插件权限异常
- PluginCommunicationException // 插件通信异常
- PluginConfigurationException // 插件配置异常
- PluginTimeoutException       // 插件超时异常
- PluginSystemFatalException   // 系统致命异常
```

### 2. 异常处理策略

```csharp
public enum ExceptionHandlingStrategy
{
    Ignore,           // 忽略异常，继续执行
    LogAndContinue,   // 记录异常并继续
    Retry,           // 重试操作
    Fallback,        // 回退到备用方案
    StopPlugin,      // 停止插件
    StopSystem,      // 停止整个系统
    Throw            // 抛出异常
}
```

### 3. 异常处理器

```csharp
public interface IExceptionHandler
{
    Task<T> HandleAsync<T>(Func<Task<T>> operation, string operationName, string pluginId = null, ExceptionHandlingConfig config = null);
    Task HandleAsync(Func<Task> operation, string operationName, string pluginId = null, ExceptionHandlingConfig config = null);
    void RegisterHandler<T>(Func<T, Task> handler) where T : PluginSystemException;
    void RegisterRecoveryAction<T>(Func<T, Task> recoveryAction) where T : PluginSystemException;
}
```

## 使用示例

### 基本异常处理

```csharp
var exceptionHandler = new ExceptionHandler(new FileErrorLogger());

// 注册自定义处理器
exceptionHandler.RegisterHandler<PluginLoadException>(async ex =>
{
    Console.WriteLine($"插件加载失败: {ex.PluginId}");
});

// 使用异常处理器
await exceptionHandler.HandleAsync(async () =>
{
    // 可能失败的操作
    await LoadPluginAsync(pluginPath);
}, "LoadPlugin", pluginId);
```

### 重试机制

```csharp
var config = new ExceptionHandlingConfig
{
    Strategy = ExceptionHandlingStrategy.Retry,
    MaxRetryAttempts = 3,
    RetryDelay = TimeSpan.FromSeconds(2),
    BackoffMultiplier = 2.0,
    ShouldRetry = ex => ex is PluginLoadException
};

await exceptionHandler.HandleAsync(async () =>
{
    await LoadPluginAsync(pluginPath);
}, "LoadPlugin", pluginId, config);
```

### 熔断器模式

```csharp
var config = new ExceptionHandlingConfig
{
    EnableCircuitBreaker = true,
    CircuitBreakerThreshold = 5,
    CircuitBreakerTimeout = TimeSpan.FromMinutes(1)
};

// 当连续失败次数达到阈值时，熔断器会打开
// 阻止进一步的调用，直到超时时间过去
```

### 健康检查

```csharp
var healthCheckService = new HealthCheckService(logger);

// 注册健康检查
healthCheckService.RegisterHealthCheck(new SystemResourceHealthCheck());
healthCheckService.RegisterHealthCheck(new PluginHealthCheck(host, pluginId));

// 执行健康检查
var report = await healthCheckService.CheckHealthAsync();
Console.WriteLine($"系统状态: {report.OverallStatus}");
```

### 自愈机制

```csharp
var selfHealingService = new SelfHealingService(logger, healthCheckService);

// 注册恢复策略
selfHealingService.RegisterRecoveryStrategy(new PluginRestartRecoveryStrategy(host, logger));
selfHealingService.RegisterRecoveryStrategy(new ResourceCleanupRecoveryStrategy(logger));

// 尝试恢复
var result = await selfHealingService.AttemptRecoveryAsync(exception);
if (result.Success)
{
    Console.WriteLine($"恢复成功: {result.Message}");
}
```

## 配置选项

### 异常处理配置

```csharp
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
```

## 日志记录

### 错误日志接口

```csharp
public interface IErrorLogger
{
    Task LogErrorAsync(PluginSystemException exception);
    Task LogErrorAsync(Exception exception, string context = null);
    Task LogWarningAsync(string message, string context = null);
    Task LogInfoAsync(string message, string context = null);
    Task<IEnumerable<ErrorLogEntry>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null);
    Task ClearLogsAsync();
}
```

### 日志记录器实现

- **ConsoleErrorLogger** - 控制台输出
- **FileErrorLogger** - 文件记录
- **StructuredErrorLogger** - 结构化日志

## 恢复策略

### 内置恢复策略

1. **PluginRestartRecoveryStrategy** - 插件重启
2. **ResourceCleanupRecoveryStrategy** - 资源清理
3. **ConfigurationResetRecoveryStrategy** - 配置重置

### 自定义恢复策略

```csharp
public class CustomRecoveryStrategy : IRecoveryStrategy
{
    public string Name => "CustomRecovery";

    public async Task<bool> CanRecoverAsync(PluginSystemException exception)
    {
        return exception is PluginCommunicationException;
    }

    public async Task<RecoveryResult> RecoverAsync(PluginSystemException exception)
    {
        // 实现自定义恢复逻辑
        return RecoveryResult.SuccessResult("恢复成功");
    }
}
```

## 最佳实践

### 1. 异常处理策略选择

- **LoadPlugin** - 使用重试机制，最多3次
- **UnloadPlugin** - 使用日志记录并继续
- **Critical Operations** - 使用熔断器模式
- **Non-Critical Operations** - 使用忽略策略

### 2. 健康检查配置

- 定期检查系统资源使用情况
- 监控插件状态和性能
- 设置合理的检查间隔（1-5分钟）

### 3. 恢复策略设计

- 优先使用轻量级恢复策略
- 避免在恢复过程中产生新的异常
- 记录所有恢复操作的详细信息

### 4. 日志记录建议

- 使用结构化日志格式
- 包含足够的上下文信息
- 定期清理和归档日志文件

## 集成到现有系统

### HostBuilder 集成

```csharp
var host = new HostBuilder()
    .WithPluginDirectory("./Plugins")
    .ConfigureServices(services =>
    {
        // 注册异常处理服务
        services.RegisterSingleton<IErrorLogger>(new FileErrorLogger());
        services.RegisterSingleton<IExceptionHandler>(() => new ExceptionHandler(services.Resolve<IErrorLogger>()));
        services.RegisterSingleton<IHealthCheckService>(() => new HealthCheckService(services.Resolve<IErrorLogger>()));
        services.RegisterSingleton<ISelfHealingService>(() => new SelfHealingService(
            services.Resolve<IErrorLogger>(), 
            services.Resolve<IHealthCheckService>()));
    })
    .Build();
```

### 插件开发建议

```csharp
public class MyPlugin : IPlugin
{
    public async Task InitializeAsync(IPluginContext context)
    {
        try
        {
            // 初始化逻辑
        }
        catch (Exception ex)
        {
            // 抛出标准化的异常
            throw new PluginInitializationException($"初始化失败: {ex.Message}", Id, ex);
        }
    }
}
```

## 监控和调试

### 健康检查报告

```csharp
var report = await healthCheckService.CheckHealthAsync();
foreach (var result in report.Results)
{
    Console.WriteLine($"{result.Name}: {result.Status} - {result.Message}");
    if (result.Data.Count > 0)
    {
        foreach (var data in result.Data)
        {
            Console.WriteLine($"  {data.Key}: {data.Value}");
        }
    }
}
```

### 错误日志查询

```csharp
var logs = await logger.GetErrorLogsAsync(DateTime.Now.AddHours(-1), DateTime.Now);
foreach (var log in logs)
{
    Console.WriteLine($"[{log.Timestamp}] {log.Level}: {log.Message}");
}
```

这个增强的异常处理机制为 PluginSystem 提供了企业级的可靠性和可维护性，确保系统在遇到问题时能够自动恢复并继续运行。

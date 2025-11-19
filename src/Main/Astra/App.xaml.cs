// ⚠️ 此文件已被重构,请参考 Refactored/App.xaml.cs
// 保留此文件仅作为临时备份参考
// 重构内容：
//   - ThemeInitializationService: 主题初始化
//   - ApplicationStartupService: 启动流程
//   - ServiceRegistrationConfigurator: 服务注册

using Astra.Services.Startup;
using Astra.Services.Session;
using Astra.UI.Helpers;
using Astra.UI.Styles.Controls;
using Microsoft.Extensions.DependencyInjection;
using NavStack.Regions;
using NavStack.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Astra.ViewModels;
using Astra.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Astra.Services.Monitoring;
using Astra.Views;
using Astra.Core.Plugins.Abstractions;


namespace Astra
{
    /// <summary>
    /// 重构后的 App.xaml.cs - 职责单一、清晰简洁
    /// 
    /// ✅ 职责分离：
    ///   - ThemeInitializationService: 主题初始化
    ///   - ApplicationStartupService: 启动流程
    ///   - ServiceRegistrationConfigurator: 服务注册
    ///   - SingleInstanceService: 单实例检查
    ///   
    /// ✅ 代码简洁：从 327 行减少到 ~80 行
    /// ✅ 易于维护：每个服务职责清晰
    /// ✅ 易于测试：可以独立测试每个服务
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        private readonly ThemeInitializationService _themeService;
        private readonly SingleInstanceService _singleInstanceService;
        private ApplicationStartupService _startupService;
        private ILogger<App> _logger = NullLogger<App>.Instance;

        public App()
        {
            _themeService = new ThemeInitializationService();
            _singleInstanceService = new SingleInstanceService();
            RegisterGlobalExceptionHandlers();
            MonitorApplicationExit();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 检查是否已有实例运行
            if (!_singleInstanceService.EnsureSingleInstance())
            {
                // 如果已有实例运行，显示提示并退出
                ModernMessageBox.Show(
                    "应用程序已在运行",
                    "应用程序已经在运行中，无法启动多个实例。",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 初始化主题
            _themeService.Initialize();

            // 异步启动应用程序
            // ⭐ 启动流程：启动 SplashScreen → 注册服务 → 执行任务 → 构建 ServiceProvider → 加载插件 → 创建主窗口 → 显示主窗口
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    _startupService = new ApplicationStartupService(Dispatcher, Shutdown);
                    ServiceProvider = await _startupService.StartAsync();

                    // 初始化日志（如果之前未初始化）
                    if (_logger is NullLogger<App> || _logger == null)
                    {
                        try
                        {
                            var resolvedLogger = ServiceProvider?.GetService<ILogger<App>>();
                            if (resolvedLogger != null)
                            {
                                _logger = resolvedLogger;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 日志初始化失败不影响启动，使用默认日志
                            System.Diagnostics.Debug.WriteLine($"[App] 日志初始化失败: {ex.Message}");
                        }
                    }

                    // 启动健康检查与遥测记录
                    if (ServiceProvider != null)
                    {
                        try
                        {
                            var health = ServiceProvider.GetService<IHealthCheckService>() 
                                ?? new BasicHealthCheckService(ServiceProvider);
                            var telemetry = ServiceProvider.GetService<ITelemetryService>();
                            var result = await health.CheckAsync();
                            telemetry?.TrackEvent("Startup.Health", new { result.IsHealthy, result.Message });

                            if (!result.IsHealthy)
                            {
                                ToastHelper.ShowError($"启动健康检查失败: {result.Message}");
                            }
                        }
                        catch (Exception hx)
                        {
                            _logger.LogError(hx, "[App] 启动健康检查失败");
                        }
                    }

                    // 启动完成后注册主窗口关闭事件
                    // ⭐ 主窗口已在 ApplicationStartupService 中创建，直接使用 Application.Current.MainWindow
                    if (Application.Current.MainWindow != null)
                    {
                        Application.Current.MainWindow.Closing += OnMainWindowClosing;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[App] 启动过程中发生错误");
                    ModernMessageBox.Show(
                        "启动失败",
                        $"应用程序启动时发生错误：\n\n{ex.Message}",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown(-1);
                }
            });
        }


        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger.LogInformation("应用程序正在退出，退出代码: {ExitCode}", e.ApplicationExitCode);

                // 快速清理关键资源（如果之前未清理）
                if (ServiceProvider != null && !_isCleaningUp)
                {
                    try
                    {
                        CleanupCriticalResources();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "OnExit 中清理资源时出错");
                    }
                }

                // 释放 ServiceProvider
                if (ServiceProvider is IAsyncDisposable disposable)
                {
                    try
                    {

                        Task.Run(async () =>
                        {
                            await disposable.DisposeAsync().ConfigureAwait(false);

                        }).Wait(TimeSpan.FromSeconds(3)); // 等待最多3秒，然后继续退出
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "释放 ServiceProvider 时出错");
                    }
                    finally
                    {
                        ServiceProvider = null;
                    }
                }

                _logger.LogInformation("应用程序退出完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnExit 中发生错误");
            }
            finally
            {
                base.OnExit(e);
            }
        }

        /// <summary>
        /// 注册全局异常处理器
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            // UI线程未处理异常
            DispatcherUnhandledException += (s, e) =>
            {
                e.Handled = true;
                HandleUnhandledException(e.Exception, "UI线程未处理异常");
            };

            // 应用程序域未处理异常
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                HandleUnhandledException(e.ExceptionObject as Exception, "应用程序域未处理异常");
            };

            // 任务调度器未处理异常
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                e.SetObserved();
                HandleUnhandledException(e.Exception, "任务调度器未处理异常");
            };
        }

        /// <summary>
        /// 处理未捕获的异常
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="source">异常来源</param>
        private void HandleUnhandledException(Exception exception, string source)
        {
            try
            {
                var errorMessage = $"{source}:\n{exception?.Message ?? "未知错误"}";

                // 在UI线程中显示错误提示
                if (Dispatcher.CheckAccess())
                {
                    ToastHelper.ShowError(errorMessage);
                }
                else
                {
                    Dispatcher.Invoke(() => ToastHelper.ShowError(errorMessage));
                }

                // 记录详细错误信息到调试输出
                _logger.LogError(exception, "未捕获异常: {Source}", source);
            }
            catch (Exception ex)
            {
                // 如果异常处理本身出错，至少记录到调试输出
                _logger.LogError(ex, "异常处理器本身出错");
            }
        }

        /// <summary>
        /// 监听应用程序退出事件
        /// </summary>
        private void MonitorApplicationExit()
        {
            Exit += (s, e) =>
            {
                _logger.LogInformation("应用退出: ExitCode={ExitCode}", e.ApplicationExitCode);
            };
        }

        /// <summary>
        /// 主窗口关闭事件处理
        /// </summary>
        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 取消默认关闭行为
            e.Cancel = true;

            // 显示确认对话框
            var result = ModernMessageBox.Show(
                "确认退出",
                "您确定要关闭应用程序吗？",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 用户确认退出，先清理窗口和 ViewModel，再清理其他资源
                CleanupWindowAndDataBindings();
                CleanupCriticalResources();

                // 允许窗口关闭
                e.Cancel = false;

                // 关闭应用程序
                Shutdown();

                // 如果 Shutdown() 没有立即生效，在后台线程中等待并强制退出
                // 这样可以确保即使有后台任务，程序也能完全退出
                Task.Run(async () =>
                {
                    await Task.Delay(500);

                    if (!Environment.HasShutdownStarted)
                    {
                        _logger.LogWarning("应用程序未正常关闭，强制退出");
                        try
                        {
                            Environment.Exit(0);
                        }
                        catch { }
                    }
                });
            }
            // 如果用户选择"否"，则什么都不做，窗口保持打开状态
        }

        /// <summary>
        /// 清理窗口和数据绑定（在关闭前执行，避免 NullReferenceException）
        /// </summary>
        private void CleanupWindowAndDataBindings()
        {
            try
            {
                if (MainWindow is MainView mainView)
                {
                    _logger.LogDebug("开始清理窗口和数据绑定...");

                    // 1. 先清理 ViewModel（这会触发数据绑定的清理）
                    if (mainView.DataContext is IDisposable disposableViewModel)
                    {
                        try
                        {
                            disposableViewModel.Dispose();
                            _logger.LogDebug("ViewModel 已释放");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "释放 ViewModel 时出错");
                        }
                    }

                    // 2. 将 DataContext 设置为 null，防止数据绑定访问已释放的对象
                    mainView.DataContext = null;

                    // 3. 解绑关闭事件，避免重复触发
                    mainView.Closing -= OnMainWindowClosing;

                    _logger.LogDebug("窗口和数据绑定清理完成");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理窗口和数据绑定时出错");
            }
        }

        /// <summary>
        /// 快速清理关键资源（同步，快速操作）
        /// </summary>
        private void CleanupCriticalResources()
        {
            // ⚠️ 避免死锁：使用 Task.Run 在后台线程执行同步等待
            // 这样可以避免在 UI 线程上使用 GetAwaiter().GetResult() 造成的死锁风险
            try
            {
                Task.Run(async () =>
                {
                    await CleanupCriticalResourcesAsync().ConfigureAwait(false);
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理关键资源时发生错误");
            }
        }

        /// <summary>
        /// 异步清理关键资源
        /// </summary>
        private async Task CleanupCriticalResourcesAsync()
        {
            // 防止重复清理
            if (_isCleaningUp)
                return;

            _isCleaningUp = true;

            try
            {
                _logger.LogInformation("开始清理关键资源...");

                if (ServiceProvider == null)
                {
                    _logger.LogWarning("ServiceProvider 为 null，跳过资源清理");
                    return;
                }

                // 1. 停止用户会话服务中的定时器（快速操作）
                try
                {
                    var sessionService = ServiceProvider.GetService<IUserSessionService>();
                    if (sessionService != null)
                    {
                        sessionService.Logout("应用程序关闭");
                        _logger.LogDebug("用户会话服务已停止");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理用户会话服务时出错");
                }

                // 2. 停止健康检查服务（快速操作，设置超时）
                try
                {
                    var healthCheckService = ServiceProvider.GetService<Astra.Core.Plugins.Health.IHealthCheckService>();
                    if (healthCheckService is Astra.Core.Plugins.Health.HealthCheckService healthService)
                    {
                        // 使用 ConfigureAwait(false) 避免回到原始上下文，防止死锁
                        var stopTask = healthService.StopAsync();
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(1));
                        var completedTask = await Task.WhenAny(stopTask, timeoutTask).ConfigureAwait(false);

                        if (completedTask == timeoutTask)
                        {
                            _logger.LogWarning("停止健康检查服务超时，继续关闭");
                        }
                        else
                        {
                            await stopTask.ConfigureAwait(false);
                            _logger.LogDebug("健康检查服务已停止");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "停止健康检查服务时出错: {Error}", ex.Message);
                }

                // 3. 快速卸载所有插件（设置超时，避免无限等待）
                try
                {
                    var pluginHost = ServiceProvider.GetService<IPluginHost>();
                    if (pluginHost != null)
                    {
                        var plugins = pluginHost.LoadedPlugins.ToList();
                        _logger.LogInformation("快速卸载 {Count} 个插件", plugins.Count);

                        // 尝试同步卸载插件，但设置超时
                        var unloadTasks = new List<Task>();
                        foreach (var plugin in plugins)
                        {
                            try
                            {
                                var unloadTask = pluginHost.UnloadPluginAsync(plugin.Id);
                                unloadTasks.Add(unloadTask);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "启动插件 {PluginId} 卸载任务时出错", plugin.Id);
                            }
                        }

                        // 等待所有卸载任务完成，但设置总超时（最多等待 2 秒）
                        if (unloadTasks.Count > 0)
                        {
                            try
                            {
                                var allUnloadTask = Task.WhenAll(unloadTasks);
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
                                var completedTask = await Task.WhenAny(allUnloadTask, timeoutTask).ConfigureAwait(false);

                                if (completedTask == timeoutTask)
                                {
                                    _logger.LogWarning("插件卸载超时，继续关闭应用程序");
                                }
                                else
                                {
                                    _logger.LogDebug("所有插件卸载完成");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "等待插件卸载时出错");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理插件时出错: {Error}", ex.Message);
                }

                _logger.LogInformation("关键资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理关键资源时发生错误");
            }
        }


        private bool _isCleaningUp = false;

    }

}

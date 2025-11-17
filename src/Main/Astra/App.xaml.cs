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
            Dispatcher.InvokeAsync(async () =>
            {
                _startupService = new ApplicationStartupService(Dispatcher, Shutdown);
                ServiceProvider = await _startupService.StartAsync();

				// 初始化日志
				try
				{
					var resolvedLogger = ServiceProvider.GetService<ILogger<App>>();

					if (resolvedLogger != null)
					{
						_logger = resolvedLogger;
					}
				}
				catch { }

				// 启动健康检查与遥测记录
				try
				{
					var health = ServiceProvider.GetService<IHealthCheckService>() ?? new BasicHealthCheckService(ServiceProvider);
					var telemetry = ServiceProvider.GetService<Astra.Services.Monitoring.ITelemetryService>();
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

				// ⭐ 迁移插件注册的设备到主应用的 DeviceManager
				try
				{
					await MigratePluginDevicesToMainDeviceManagerAsync();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[App] 迁移插件设备失败");
					System.Diagnostics.Debug.WriteLine($"[App] 迁移插件设备失败: {ex.Message}");
				}

                // 启动完成后注册主窗口关闭事件
                if (MainWindow != null)
                {
                    MainWindow.Closing += OnMainWindowClosing;

					// 注册区域与导航（集中在引导器中）
					await Astra.Bootstrap.NavigationBootstrapper.InitializeAsync(MainWindow, ServiceProvider, Dispatcher, _logger);
                }
            });
        }

        /// <summary>
        /// 检查并迁移插件注册的设备到主应用的 DeviceManager（如果需要）
        /// 
        /// ⚠️ 为什么可能需要迁移？
        /// 1. 插件加载时，App.ServiceProvider 还没有设置（插件加载是在启动任务中进行的）
        /// 2. 插件在 OnEnableAsync 时会尝试从 App.ServiceProvider 获取 DeviceManager
        /// 3. 如果 App.ServiceProvider 此时为 null，插件会使用 PluginContext 的 DeviceManager
        /// 4. 这会导致插件系统和主应用使用不同的 DeviceManager 实例
        /// 
        /// ✅ 架构改进（已完成）：
        /// - ServiceCollectionAdapter 现在支持接受已构建的 IServiceProvider（如果可用）
        /// - PluginHostFactory.CreateDefaultHost 现在可以接受可选的 externalServiceProvider 参数
        /// - 插件在 OnEnableAsync 时会尝试从 App.ServiceProvider 获取 DeviceManager
        /// 
        /// 💡 此方法作为后备方案：
        /// - 如果插件已经成功使用主应用的 DeviceManager，则不需要迁移
        /// - 如果插件使用了 PluginContext 的 DeviceManager，则需要迁移设备
        /// </summary>
        private async Task MigratePluginDevicesToMainDeviceManagerAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App] ========== 检查插件设备迁移 ==========");
                
                // 获取主应用的 DeviceManager
                var mainDeviceManager = ServiceProvider?.GetService<Astra.Core.Devices.Management.IDeviceManager>();
                if (mainDeviceManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("[App] ⚠️ 主应用 DeviceManager 为 null，无法检查迁移");
                    return;
                }
                
                var mainDeviceCount = mainDeviceManager.GetDeviceCount();
                System.Diagnostics.Debug.WriteLine($"[App] 主应用 DeviceManager 实例哈希码: {mainDeviceManager.GetHashCode()}");
                System.Diagnostics.Debug.WriteLine($"[App] 主应用当前设备数量: {mainDeviceCount}");
                
                // 如果主应用已经有设备，说明插件已经成功使用主应用的 DeviceManager，不需要迁移
                if (mainDeviceCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] ✅ 主应用已有 {mainDeviceCount} 个设备，插件已正确使用主应用的 DeviceManager，无需迁移");
                    return;
                }
                
                // 获取插件宿主
                var pluginHost = ServiceProvider?.GetService<Astra.Core.Plugins.Abstractions.IPluginHost>();
                if (pluginHost == null)
                {
                    System.Diagnostics.Debug.WriteLine("[App] ⚠️ 插件宿主为 null，无法检查迁移");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[App] 已加载插件数量: {pluginHost.LoadedPlugins.Count}");
                
                // 遍历所有已加载的插件，检查是否有设备需要迁移
                int totalDevicesToMigrate = 0;
                foreach (var plugin in pluginHost.LoadedPlugins)
                {
                    try
                    {
                        // 尝试通过反射获取插件内部的设备列表
                        var pluginType = plugin.GetType();
                        var devicesField = pluginType.GetField("_devices", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (devicesField != null)
                        {
                            var devices = devicesField.GetValue(plugin) as System.Collections.Generic.IEnumerable<Astra.Core.Devices.Interfaces.IDevice>;
                            if (devices != null)
                            {
                                foreach (var device in devices)
                                {
                                    if (device != null && !mainDeviceManager.DeviceExists(device.DeviceId))
                                    {
                                        totalDevicesToMigrate++;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] 检查插件 {plugin.Name} 时出错: {ex.Message}");
                    }
                }
                
                // 如果没有设备需要迁移，直接返回
                if (totalDevicesToMigrate == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[App] ✅ 没有设备需要迁移，插件已正确使用主应用的 DeviceManager");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[App] 发现 {totalDevicesToMigrate} 个设备需要迁移，开始迁移...");
                
                // 执行迁移
                int migratedCount = 0;
                foreach (var plugin in pluginHost.LoadedPlugins)
                {
                    try
                    {
                        var pluginType = plugin.GetType();
                        var devicesField = pluginType.GetField("_devices", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (devicesField != null)
                        {
                            var devices = devicesField.GetValue(plugin) as System.Collections.Generic.IEnumerable<Astra.Core.Devices.Interfaces.IDevice>;
                            if (devices != null)
                            {
                                foreach (var device in devices)
                                {
                                    if (device != null && !mainDeviceManager.DeviceExists(device.DeviceId))
                                    {
                                        var result = mainDeviceManager.RegisterDevice(device);
                                        if (result.Success)
                                        {
                                            migratedCount++;
                                            System.Diagnostics.Debug.WriteLine($"[App] ✅ 迁移设备: {device.DeviceName} (ID: {device.DeviceId})");
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[App] ❌ 迁移设备失败: {device.DeviceName}, 原因: {result.ErrorMessage}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] 迁移插件 {plugin.Name} 的设备时出错: {ex.Message}");
                    }
                }
                
                var finalDeviceCount = mainDeviceManager.GetDeviceCount();
                System.Diagnostics.Debug.WriteLine($"[App] 迁移完成，共迁移 {migratedCount} 个设备，主应用设备总数: {finalDeviceCount}");
                System.Diagnostics.Debug.WriteLine("[App] ========== 设备迁移完成 ==========");
                
                // ⭐ 如果迁移了设备，延迟触发 ConfigViewModel 刷新
                if (migratedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine("[App] 延迟触发 ConfigViewModel 刷新...");
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Delay(1000);
                        
                        try
                        {
                            var configViewModel = ServiceProvider?.GetService<ViewModels.ConfigViewModel>();
                            if (configViewModel != null)
                            {
                                System.Diagnostics.Debug.WriteLine("[App] 找到 ConfigViewModel，触发刷新");
                                configViewModel.RefreshConfigTreeCommand?.Execute(null);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[App] ConfigViewModel 尚未创建，将通过事件自动刷新");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[App] 触发 ConfigViewModel 刷新时出错: {ex.Message}");
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] ❌ 检查/迁移插件设备时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
                // 不抛出异常，避免影响应用启动
            }
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
                if (ServiceProvider is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                        _logger.LogDebug("ServiceProvider 已释放");
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

                // 释放单实例服务资源
                try
                {
                    _singleInstanceService?.Dispose();
                    _logger.LogDebug("单实例服务已释放");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "释放单实例服务时出错");
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

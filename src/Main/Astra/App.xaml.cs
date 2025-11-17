// ⚠️ 此文件已被重构,请参考 Refactored/App.xaml.cs
// 保留此文件仅作为临时备份参考
// 重构内容：
//   - ThemeInitializationService: 主题初始化
//   - ApplicationStartupService: 启动流程
//   - ServiceRegistrationConfigurator: 服务注册

using Astra.Services.Startup;
using Astra.UI.Helpers;
using Astra.UI.Styles.Controls;
using Microsoft.Extensions.DependencyInjection;
using NavStack.Regions;
using NavStack.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Astra.ViewModels;
using Astra.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Astra.Services.Monitoring;


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

                // 启动完成后注册主窗口关闭事件
                if (MainWindow != null)
                {
                    MainWindow.Closing += OnMainWindowClosing;

					// 注册区域与导航（集中在引导器中）
					await Astra.Bootstrap.NavigationBootstrapper.InitializeAsync(MainWindow, ServiceProvider, Dispatcher, _logger);
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                // 释放单实例服务资源
                _singleInstanceService?.Dispose();
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
                // 用户确认退出，关闭应用程序
                Shutdown();
            }
            // 如果用户选择"否"，则什么都不做，窗口保持打开状态
        }
    }

}

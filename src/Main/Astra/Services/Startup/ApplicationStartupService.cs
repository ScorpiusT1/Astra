using Astra.Core.Access.Services;
using Astra.Bootstrap.Services;
using Astra.Bootstrap.Tasks;
using Astra.Services.Session;
using Astra.UI.Helpers;
using Astra.Utilities;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Services.Startup
{
    /// <summary>
    /// 应用程序启动服务 - 负责应用程序的启动流程
    /// </summary>
    public class ApplicationStartupService
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<int> _shutdownAction;
        private ApplicationBootstrapper<MainView> _bootstrapper;

        public ApplicationStartupService(Dispatcher dispatcher, Action<int> shutdownAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _shutdownAction = shutdownAction ?? throw new ArgumentNullException(nameof(shutdownAction));
        }

        /// <summary>
        /// 执行应用程序启动流程
        /// </summary>
        public async Task<IServiceProvider> StartAsync()
        {
            try
            {
                _bootstrapper = CreateBootstrapper();
                ConfigureSplashScreen(_bootstrapper);
                ConfigureServices(_bootstrapper);
                AddTasks(_bootstrapper);

                Debug.WriteLine("开始执行启动流程...");

                var result = await _bootstrapper.RunAsync();

                return await HandleStartupResult(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("========================================");
                Debug.WriteLine($"启动过程发生异常: {ex.GetType().Name}");
                Debug.WriteLine($"消息: {ex.Message}");
                Debug.WriteLine($"堆栈: {ex.StackTrace}");
                Debug.WriteLine("========================================");

                ShowFatalError(ex);
                _shutdownAction(-1);
                return null;
            }
        }

        private ApplicationBootstrapper<MainView> CreateBootstrapper()
        {
            return new ApplicationBootstrapper<MainView>();
        }

        private void ConfigureSplashScreen(ApplicationBootstrapper<MainView> bootstrapper)
        {
            bootstrapper.ConfigureSplashScreen(options =>
            {
                options.Title = AssemblyInfo.ProductName;
                options.Subtitle = "正在初始化系统模块...";
                options.LogoText = "AS";
                options.Width = 700;
                options.Height = 450;

                options.AllowCancel = true;
                options.ConfirmCancel = false;
                options.CancelConfirmMessage = "确定要退出吗？这将终止应用程序启动。";
                options.AllowDrag = true;

                options.Copyright = AssemblyInfo.GetFullCopyright();
                options.Version = AssemblyInfo.Version;
                options.Website = "https://www.bydglobal.com";
            });
        }

        private void ConfigureServices(ApplicationBootstrapper<MainView> bootstrapper)
        {
            bootstrapper.ConfigureServices(services =>
            {
                Debug.WriteLine("开始注册服务...");

                // 使用服务注册配置器
                var configurator = new ServiceRegistrationConfigurator();
                configurator.ConfigureServices(services);

                Debug.WriteLine("服务注册完成");
            });
        }

        private void AddTasks(ApplicationBootstrapper<MainView> bootstrapper)
        {
            bootstrapper
                .AddTask(new ConfigurationLoadTask())
                .AddTask(new PluginLoadTask());
        }

        private async Task<IServiceProvider> HandleStartupResult(BootstrapResult result)
        {
            if (result.IsCancelled)
            {
                Debug.WriteLine("用户取消了启动");
                _shutdownAction(0);
                return null;
            }

            Debug.WriteLine($"启动流程完成: IsSuccess={result.IsSuccess}, IsCancelled={result.IsCancelled}");

            if (result.IsSuccess)
            {
                return await ShowMainWindow(result);
            }
            else
            {
                Debug.WriteLine("启动失败");
                ShowStartupError(result);
                _shutdownAction(-1);
                return null;
            }
        }

        private async Task<IServiceProvider> ShowMainWindow(BootstrapResult result)
        {
            Debug.WriteLine("启动成功，准备显示主窗口...");

            var context = _bootstrapper.GetContext();
            var serviceProvider = context.ServiceProvider;

            Debug.WriteLine($"ServiceProvider: {serviceProvider != null}");

            // ⭐ 从 ServiceProvider 获取主窗口实例
            var mainView = serviceProvider.GetRequiredService<MainView>();
            Debug.WriteLine($"MainWindow: {mainView != null}");

            await _dispatcher.InvokeAsync(() =>
            {
                if (mainView != null)
                {
                    Debug.WriteLine($"设置 MainWindow: {mainView.GetType().Name}");
                    Application.Current.MainWindow = mainView;

                    Debug.WriteLine("调用 Show()...");
                    mainView.Show();

                    Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

                    Debug.WriteLine($"窗口可见性: {mainView.IsVisible}");
                    Debug.WriteLine($"窗口状态: {mainView.WindowState}");

                    mainView.Activate();
                    mainView.Focus();

                    Debug.WriteLine("主窗口已显示");
                }
                else
                {
                    Debug.WriteLine("⚠️ 警告：MainWindow 为 null");
                    throw new InvalidOperationException("主窗口创建失败：mainView 为 null");
                }
            }, DispatcherPriority.Send);

            // ⭐ 启动后自动登录操作员账号
            await AutoLoginOperator(serviceProvider);

            return serviceProvider;
        }

        private void ShowStartupError(BootstrapResult result)
        {
            var message = "应用程序启动失败";

            if (result.FatalException != null)
            {
                message += $"\n\n错误：{result.FatalException.Message}";
            }

            if (result.FailedTasks.Count > 0)
            {
                message += "\n\n失败的任务：";
                foreach (var (task, error) in result.FailedTasks)
                {
                    message += $"\n- {task.Name}: {error.Message}";
                }
            }

            MessageBoxHelper.ShowError(message, "启动失败");
        }

        /// <summary>
        /// 自动登录操作员账号
        /// </summary>
        private async Task AutoLoginOperator(IServiceProvider serviceProvider)
        {
            try
            {
                var userManagementService = serviceProvider.GetService<IUserManagementService>();
                var sessionService = serviceProvider.GetService<IUserSessionService>();

                if (userManagementService == null || sessionService == null)
                {
                    Debug.WriteLine("[AutoLogin] 服务未初始化，跳过自动登录");
                    return;
                }

                // ⭐ 尝试获取最后一次登录的操作员
                var lastOperator = userManagementService.GetLastLoginOperator();

                if (lastOperator != null)
                {
                    Debug.WriteLine($"[AutoLogin] 找到最后登录的操作员: {lastOperator.Username}");
                    
                    // 自动登录
                    await _dispatcher.InvokeAsync(() =>
                    {
                        sessionService.Login(lastOperator);
                        Debug.WriteLine($"[AutoLogin] ✅ 自动登录成功: {lastOperator.Username}, 权限: {lastOperator.Role}");
                    });
                }
                else
                {
                    Debug.WriteLine("[AutoLogin] 数据库中没有操作员账号，不自动登录");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoLogin] 自动登录失败: {ex.Message}");
                // 自动登录失败不影响程序启动，只记录日志
            }
        }

        private void ShowFatalError(Exception ex)
        {
            MessageBoxHelper.ShowError(
                $"应用程序启动时发生致命错误:\n\n{ex.Message}",
                "严重错误"
            );
        }
    }
}

using Astra.Core.Access.Services;
using Astra.Bootstrap.Services;
using Astra.Bootstrap.Tasks;
using Astra.Services.Session;
using Astra.UI.Helpers;
using Astra.Utilities;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NavStack.Core;
using NavStack.Services;

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

                var result = await _bootstrapper.RunAsync();

                return await HandleStartupResult(result);
            }
            catch (Exception ex)
            {
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
                // 使用服务注册配置器
                var configurator = new ServiceRegistrationConfigurator();
                configurator.ConfigureServices(services);
            });
        }

        private void AddTasks(ApplicationBootstrapper<MainView> bootstrapper)
        {
            bootstrapper
                .AddTask(new ConfigurationLoadTask())
                .AddTask(new PluginLoadTask())
                .AddTask(new NavigationInitializationTask());
        }

        private async Task<IServiceProvider> HandleStartupResult(BootstrapResult result)
        {
            if (result.IsCancelled)
            {
                _shutdownAction(0);
                return null;
            }

            if (result.IsSuccess)
            {
                return await ShowMainWindow(result);
            }
            else
            {
                ShowStartupError(result);
                _shutdownAction(-1);
                return null;
            }
        }

        private async Task<IServiceProvider> ShowMainWindow(BootstrapResult result)
        {
            var context = _bootstrapper.GetContext();
            var serviceProvider = context.ServiceProvider;

            // ⭐ 从上下文或 ServiceProvider 获取主窗口实例
            var mainView = context.GetData<MainView>("MainView") 
                ?? serviceProvider.GetRequiredService<MainView>();

            await _dispatcher.InvokeAsync(() =>
            {
                if (mainView != null)
                {
                    Application.Current.MainWindow = mainView;
                    mainView.Show();
                    Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    mainView.Activate();
                    mainView.Focus();
                }
                else
                {
                    throw new InvalidOperationException("主窗口创建失败：mainView 为 null");
                }
            }, DispatcherPriority.Send);

            // ⭐ 等待窗口完全加载，确保 MainFrame 已初始化
            await _dispatcher.InvokeAsync(async () =>
            {
                if (mainView != null)
                {
                    // 如果窗口尚未加载，等待 Loaded 事件
                    if (!mainView.IsLoaded)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        RoutedEventHandler handler = null;
                        handler = (s, e) =>
                        {
                            mainView.Loaded -= handler;
                            tcs.SetResult(true);
                        };
                        mainView.Loaded += handler;
                        await tcs.Task;
                    }

                    // ⭐ 确保导航初始化：如果 MainView.OnLoaded 没有触发导航，这里手动触发
                    if (mainView.DataContext is Astra.ViewModels.MainViewViewModel viewModel)
                    {
                        // 检查是否已经导航过
                        var navManager = serviceProvider.GetService<NavStack.Services.INavigationManager>();
                        if (navManager != null && mainView.MainFrame != null)
                        {
                            try
                            {
                                // ⭐ 关键：确保使用最终 ServiceProvider 的 NavigationManager 和 RegionManager
                                // NavigationInitializationTask 可能使用了临时 ServiceProvider，导致路由映射丢失
                                
                                // 1. 注册区域
                                var regionManager = serviceProvider.GetService<NavStack.Regions.IRegionManager>();
                                if (regionManager != null && mainView.MainFrame != null)
                                {
                                    regionManager.RegisterRegion(Astra.Utilities.RegionNames.MainRegion, mainView.MainFrame);
                                }
                                
                                // 2. ⭐ 重新注册路由映射（确保使用最终 ServiceProvider 的 NavigationManager）
                                if (navManager != null)
                                {
                                    // ⭐ 验证 MainViewModel 使用的 NavigationManager 实例是否相同
                                    var vmNavManager = viewModel.Navigation.GetType()
                                        .GetField("_navigationManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                        ?.GetValue(viewModel.Navigation);
                                    
                                    if (vmNavManager != null && vmNavManager.GetHashCode() != navManager.GetHashCode())
                                    {
                                        // 使用 MainViewModel 实际使用的实例注册路由
                                        navManager = vmNavManager as NavStack.Services.INavigationManager;
                                    }
                                    
                                    navManager.RegisterForRegion(
                                        Astra.Utilities.RegionNames.MainRegion, 
                                        Astra.Utilities.NavigationKeys.Home, 
                                        typeof(Astra.Views.HomeView), 
                                        typeof(Astra.ViewModels.HomeViewModel));
                                    navManager.RegisterForRegion(
                                        Astra.Utilities.RegionNames.MainRegion, 
                                        Astra.Utilities.NavigationKeys.Config, 
                                        typeof(Astra.Views.ConfigView), 
                                        typeof(Astra.ViewModels.ConfigViewModel));
                                    navManager.RegisterForRegion(
                                        Astra.Utilities.RegionNames.MainRegion, 
                                        Astra.Utilities.NavigationKeys.Debug, 
                                        typeof(Astra.Views.DebugView), 
                                        typeof(Astra.ViewModels.DebugViewModel));
                                    navManager.RegisterForRegion(
                                        Astra.Utilities.RegionNames.MainRegion, 
                                        Astra.Utilities.NavigationKeys.Sequence, 
                                        typeof(Astra.Views.SequenceView), 
                                        typeof(Astra.ViewModels.SequenceViewModel));
                                    navManager.RegisterForRegion(
                                        Astra.Utilities.RegionNames.MainRegion, 
                                        Astra.Utilities.NavigationKeys.Permission, 
                                        typeof(Astra.Views.PermissionView), 
                                        typeof(Astra.ViewModels.PermissionViewModel));
                                }

                                // 确保 IFrameNavigationService.Frame 已设置
                                var frameNavService = serviceProvider.GetService<IFrameNavigationService>();
                                if (frameNavService != null && mainView.MainFrame != null)
                                {
                                    frameNavService.Frame = mainView.MainFrame;
                                }

                                // 如果还没有导航，触发导航初始化
                                if (string.IsNullOrEmpty(viewModel.Navigation.CurrentPageKey))
                                {
                                    await viewModel.Navigation.InitializeNavigationAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                // 导航初始化失败不影响程序启动，只记录日志
                            }
                        }
                    }
                }
            }, DispatcherPriority.Loaded);

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
                    return;
                }

                // ⭐ 尝试获取最后一次登录的操作员
                var lastOperator = userManagementService.GetLastLoginOperator();

                if (lastOperator != null)
                {
                    // 自动登录
                    await _dispatcher.InvokeAsync(() =>
                    {
                        sessionService.Login(lastOperator);
                    });
                }
            }
            catch (Exception ex)
            {
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

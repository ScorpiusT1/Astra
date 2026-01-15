using Astra.Core.Access.Services;
using Astra.Bootstrap.Services;
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
        /// 流程：启动 SplashScreen → 注册服务 → 构建 ServiceProvider → 加载插件 → 创建主窗口 → 显示主窗口 → 窗口加载完成后初始化导航
        /// </summary>
        public async Task<IServiceProvider> StartAsync()
        {
            try
            {
                _bootstrapper = CreateBootstrapper();
                ConfigureSplashScreen(_bootstrapper);
                ConfigureServices(_bootstrapper);
  
                // ⭐ 使用泛型版本的 RunAsync，返回 BootstrapResult<MainView>
                // 流程：启动 SplashScreen → 注册服务 → 构建 ServiceProvider → 加载插件 → 创建主窗口 → 显示主窗口 → 窗口加载完成后初始化导航
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

        /// <summary>
        /// 处理启动结果
        /// ⭐ 使用泛型类型 BootstrapResult<MainView>，确保类型安全
        /// </summary>
        private async Task<IServiceProvider> HandleStartupResult(BootstrapResult<MainView> result)
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

        /// <summary>
        /// 显示主窗口
        /// ⭐ 优先使用 BootstrapResult 中的主窗口实例（已由 ApplicationBootstrapper<T> 创建）
        /// </summary>
        private async Task<IServiceProvider> ShowMainWindow(BootstrapResult<MainView> result)
        {
            var context = _bootstrapper.GetContext();
            var serviceProvider = context.ServiceProvider;

            if (serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider 未初始化");
            }

            // ⭐ 优先使用 BootstrapResult 中的主窗口实例（已由 ApplicationBootstrapper<T> 在插件加载完成后创建）
            var mainView = result.MainView;

            if (mainView == null)
            {
                // 如果 BootstrapResult 中没有，尝试从上下文或 ServiceProvider 获取（兜底逻辑）
                mainView = context.GetData<MainView>("MainView") 
                    ?? serviceProvider.GetRequiredService<MainView>();
            }

            if (mainView == null)
            {
                throw new InvalidOperationException("主窗口创建失败：mainView 为 null");
            }

            // ⭐ 在 UI 线程显示主窗口（插件加载完成后）
            await _dispatcher.InvokeAsync(() =>
            {
                Application.Current.MainWindow = mainView;
                mainView.Show();                   
                mainView.Activate();
                mainView.Focus();
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

                    // ⭐ 窗口加载完成后，初始化导航服务
                    // 这是导航初始化的主要入口点，确保在窗口完全启动后才执行
                    await InitializeNavigationServiceAsync(mainView, serviceProvider);
                }
            }, DispatcherPriority.Loaded);

            // ⭐ 启动后自动登录超级管理员账号
            await AutoLoginSuperAdministrator(serviceProvider);

            return serviceProvider;
        }

        /// <summary>
        /// 初始化导航服务（主入口）
        /// ⭐ 在窗口完全加载后执行，确保 MainFrame 已初始化
        /// </summary>
        private async Task InitializeNavigationServiceAsync(MainView mainView, IServiceProvider serviceProvider)
        {
            if (!(mainView.DataContext is Astra.ViewModels.MainViewViewModel viewModel))
            {
                System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] MainViewViewModel 为 null，跳过导航初始化");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] 开始初始化导航服务...");

                // 获取导航服务
                var navManager = serviceProvider.GetService<NavStack.Services.INavigationManager>();
                var regionManager = serviceProvider.GetService<NavStack.Regions.IRegionManager>();
                var frameNavService = serviceProvider.GetService<IFrameNavigationService>();

                if (navManager == null || regionManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] 导航服务未注册，跳过导航初始化");
                    return;
                }

                if (mainView.MainFrame == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] MainFrame 为 null，跳过导航初始化");
                    return;
                }

                // 1. 注册导航区域
                try
                {
                    regionManager.RegisterRegion(Astra.Utilities.RegionNames.MainRegion, mainView.MainFrame);
                    System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] 导航区域注册成功");
                }
                catch (Exception ex)
                {
                    // 区域可能已注册，忽略异常
                    System.Diagnostics.Debug.WriteLine($"[ApplicationStartupService] 注册导航区域时发生异常（可能已注册）: {ex.Message}");
                }

                // 2. 注册路由映射（使用最终 ServiceProvider 的 NavigationManager）
                RegisterNavigationRoutes(navManager);
                System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] 路由映射注册成功");

                // 3. 设置 IFrameNavigationService.Frame
                if (frameNavService != null)
                {
                    frameNavService.Frame = mainView.MainFrame;
                    System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] IFrameNavigationService.Frame 已设置");
                }

                // 4. 初始化 ViewModel 订阅
                if (viewModel.Navigation != null)
                {
                    viewModel.Navigation.EnsureRegionSubscriptions();
                    System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] ViewModel 订阅初始化成功");
                }

                // 5. 触发导航初始化（导航到默认页面）
                // ⭐ 检查导航是否已经初始化（MainView.OnLoaded 可能已经触发）
                if (viewModel.Navigation != null)
                {
                    if (string.IsNullOrEmpty(viewModel.Navigation.CurrentPageKey))
                    {
                        // 如果还没有导航，触发导航初始化
                        await viewModel.Navigation.InitializeNavigationAsync();
                        System.Diagnostics.Debug.WriteLine("[ApplicationStartupService] 导航初始化完成");
                    }
                    else
                    {
                        // 导航已初始化（可能是 MainView.OnLoaded 已经触发）
                        System.Diagnostics.Debug.WriteLine($"[ApplicationStartupService] 导航已初始化（当前页面: {viewModel.Navigation.CurrentPageKey}），跳过");
                    }
                }
            }
            catch (Exception ex)
            {
                // 导航初始化失败不影响程序启动，只记录日志
                System.Diagnostics.Debug.WriteLine($"[ApplicationStartupService] 导航初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ApplicationStartupService] 堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 注册导航路由映射
        /// ⭐ 集中管理路由注册，便于维护
        /// </summary>
        private void RegisterNavigationRoutes(NavStack.Services.INavigationManager navManager)
        {
            var routes = new[]
            {
                (Astra.Utilities.NavigationKeys.Home, typeof(Astra.Views.HomeView), typeof(Astra.ViewModels.HomeViewModel)),
                (Astra.Utilities.NavigationKeys.Config, typeof(Astra.Views.ConfigView), typeof(Astra.ViewModels.ConfigViewModel)),
                (Astra.Utilities.NavigationKeys.Debug, typeof(Astra.Views.DebugView), typeof(Astra.ViewModels.DebugViewModel)),
                (Astra.Utilities.NavigationKeys.Sequence, typeof(Astra.Views.SequenceView), typeof(Astra.ViewModels.SequenceViewModel)),
                (Astra.Utilities.NavigationKeys.Permission, typeof(Astra.Views.PermissionView), typeof(Astra.ViewModels.PermissionViewModel)),
            };

            foreach (var (key, viewType, viewModelType) in routes)
            {
                navManager.RegisterForRegion(
                    Astra.Utilities.RegionNames.MainRegion, 
                    key, 
                    viewType, 
                    viewModelType);
            }
        }

        /// <summary>
        /// 显示启动错误
        /// ⭐ 使用基类类型（用于错误显示）
        /// </summary>
        private void ShowStartupError(BootstrapResult result)
        {
            var message = "应用程序启动失败";

            if (result.FatalException != null)
            {
                message += $"\n\n错误：{result.FatalException.Message}";
            }

            // ⭐ 已移除失败任务显示（任务系统已移除）

            MessageBoxHelper.ShowError(message, "启动失败");
        }

        /// <summary>
        /// 自动登录超级管理员账号
        /// </summary>
        private async Task AutoLoginSuperAdministrator(IServiceProvider serviceProvider)
        {
            try
            {
                var userManagementService = serviceProvider.GetService<IUserManagementService>();
                var sessionService = serviceProvider.GetService<IUserSessionService>();

                if (userManagementService == null || sessionService == null)
                {
                    return;
                }

                // ⭐ 使用超级管理员账号和密码进行登录
                const string SUPER_ADMIN_USERNAME = "SupperAdmin";
                const string SUPER_ADMIN_PASSWORD = "Admin.123";

                try
                {
                    // 验证超级管理员账号和密码
                    var superAdmin = userManagementService.Login(SUPER_ADMIN_USERNAME, SUPER_ADMIN_PASSWORD);

                    if (superAdmin != null)
                    {
                        // 自动登录
                        await _dispatcher.InvokeAsync(() =>
                        {
                            sessionService.Login(superAdmin);
                        });
                    }
                }
                catch (Exception loginEx)
                {
                    // 登录失败不影响程序启动，只记录日志
                    System.Diagnostics.Debug.WriteLine($"[ApplicationStartupService] 自动登录超级管理员失败: {loginEx.Message}");
                }
            }
            catch (Exception ex)
            {
                // 自动登录失败不影响程序启动，只记录日志
                System.Diagnostics.Debug.WriteLine($"[ApplicationStartupService] 自动登录超级管理员异常: {ex.Message}");
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

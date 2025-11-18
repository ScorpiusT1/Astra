using Astra.Bootstrap.Core;
using Astra.Utilities;
using Astra.ViewModels;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NavStack.Core;
using NavStack.Regions;
using NavStack.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Astra.Bootstrap.Tasks
{
    /// <summary>
    /// 导航初始化任务
    /// 负责在应用启动时初始化导航系统，包括区域注册和路由映射
    /// </summary>
    public class NavigationInitializationTask : BootstrapTaskBase
    {
        public override string Name => "导航初始化";
        public override string Description => "正在初始化导航系统...";
        public override double Weight => 0.5;
        public override int Priority => 1001; // 在主窗口创建之后执行
        public override bool IsCritical => false; // 导航初始化失败不影响主程序启动

        protected override async Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            context.Logger?.LogInfo("[NavigationInitializationTask] 开始执行");
            
            ReportProgress(progress, 5, "检查主窗口...");

            // 从上下文获取主窗口
            var mainWindow = context.GetData<Window>("MainView");
            if (mainWindow == null)
            {
                context.Logger?.LogWarning("主窗口未创建，跳过导航初始化");
                ReportProgress(progress, 100, "主窗口未创建，跳过导航初始化");
                return;
            }

            if (mainWindow is not MainView mainView)
            {
                context.Logger?.LogWarning($"主窗口类型不正确，期望 MainView，实际: {mainWindow.GetType().Name}");
                ReportProgress(progress, 100, "主窗口类型不正确");
                return;
            }

            // ⭐ ServiceProvider 在所有任务完成后才构建，这里需要临时构建一个
            IServiceProvider serviceProvider = context.ServiceProvider;
            if (serviceProvider == null)
            {
                if (context.Services == null)
                {
                    context.Logger?.LogWarning("Services 未初始化，跳过导航初始化");
                    ReportProgress(progress, 100, "Services 未初始化");
                    return;
                }
                
                // 临时构建 ServiceProvider 用于导航初始化
                serviceProvider = context.Services.BuildServiceProvider();
                context.Logger?.LogInfo("临时构建 ServiceProvider 用于导航初始化");
            }

            var logger = serviceProvider.GetService<ILogger<NavigationInitializationTask>>();
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            ReportProgress(progress, 10, "读取默认页面配置...");

            // 读取默认页配置（可选）
            string defaultPageKey = NavigationKeys.Home;
            try
            {
                var appNavOptions = serviceProvider.GetService<IOptions<Astra.Configuration.AppNavOptions>>();
                if (!string.IsNullOrWhiteSpace(appNavOptions?.Value?.DefaultPage))
                {
                    defaultPageKey = appNavOptions.Value.DefaultPage;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "读取默认页面配置失败，使用默认值");
            }

            ReportProgress(progress, 20, "等待窗口初始化...");

            // ⭐ 确保窗口已加载，MainFrame 已初始化
            // 在 UI 线程上等待窗口 Loaded 事件
            bool windowLoaded = false;
            await dispatcher.InvokeAsync(() =>
            {
                if (mainView.IsLoaded)
                {
                    windowLoaded = true;
                }
                else
                {
                    // 如果窗口尚未加载，订阅 Loaded 事件
                    mainView.Loaded += (s, e) => windowLoaded = true;
                }
            }, DispatcherPriority.Loaded);

            // 等待窗口加载完成（最多等待 2 秒）
            int waitCount = 0;
            while (!windowLoaded && waitCount < 20)
            {
                await Task.Delay(100, cancellationToken);
                waitCount++;
            }

            if (!windowLoaded)
            {
                context.Logger?.LogWarning("⚠️ 窗口加载超时，继续尝试初始化导航");
            }

            ReportProgress(progress, 30, "注册导航区域...");

            try
            {
                // ⭐ 确保在 UI 线程上访问 MainFrame
                Frame mainFrame = null;
                await dispatcher.InvokeAsync(() =>
                {
                    mainFrame = mainView.MainFrame;
                }, DispatcherPriority.Loaded);

                if (mainFrame == null)
                {
                    context.Logger?.LogWarning("⚠️ MainFrame 为 null，可能窗口尚未完全初始化");
                    // 不返回，继续尝试其他初始化步骤
                }

                var regionManager = serviceProvider.GetService<IRegionManager>();
                if (regionManager != null && mainFrame != null)
                {
                    regionManager.RegisterRegion(RegionNames.MainRegion, mainFrame);
                    context.Logger?.LogInfo("导航区域注册成功");
                }
                else
                {
                    if (regionManager == null)
                    {
                        context.Logger?.LogWarning("IRegionManager 未注册");
                    }
                    if (mainFrame == null)
                    {
                        context.Logger?.LogWarning("MainFrame 为 null，无法注册区域");
                    }
                }

                // ⭐ 确保 IFrameNavigationService.Frame 已设置
                var frameNavService = serviceProvider.GetService<IFrameNavigationService>();
                if (frameNavService != null && mainFrame != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        frameNavService.Frame = mainFrame;
                        context.Logger?.LogInfo("IFrameNavigationService.Frame 已设置");
                    }, DispatcherPriority.Loaded);
                }

                ReportProgress(progress, 40, "初始化 ViewModel 订阅...");

                // ⭐ 修复：DataContext 是 MainViewViewModel，不是 MainViewModel
                var viewModel = mainView.DataContext as MainViewViewModel;
                if (viewModel != null)
                {
                    var navViewModel = viewModel.Navigation;
                    navViewModel?.EnsureRegionSubscriptions();
                    context.Logger?.LogInfo("ViewModel 订阅初始化成功");
                }
                else
                {
                    context.Logger?.LogWarning("⚠️ MainViewViewModel 为 null，无法初始化订阅");
                }

                ReportProgress(progress, 60, "建立导航路由映射...");

                var navManager = serviceProvider.GetService<INavigationManager>();
                if (navManager != null)
                {
                    // 注册导航路由映射：NavigationKey -> (regionName, pageKey)
                    // 注意：页面类型已在 NavigationModule 中注册，此处只建立路由映射
                    navManager.RegisterForRegion(RegionNames.MainRegion, NavigationKeys.Home, typeof(HomeView), typeof(HomeViewModel));
                    navManager.RegisterForRegion(RegionNames.MainRegion, NavigationKeys.Config, typeof(ConfigView), typeof(ConfigViewModel));
                    navManager.RegisterForRegion(RegionNames.MainRegion, NavigationKeys.Debug, typeof(DebugView), typeof(DebugViewModel));
                    navManager.RegisterForRegion(RegionNames.MainRegion, NavigationKeys.Sequence, typeof(SequenceView), typeof(SequenceViewModel));
                    navManager.RegisterForRegion(RegionNames.MainRegion, NavigationKeys.Permission, typeof(PermissionView), typeof(PermissionViewModel));

                    context.Logger?.LogInfo("导航路由映射建立成功");

                    ReportProgress(progress, 80, "准备导航初始化...");

                    // ⭐ 注意：实际导航到默认页面由 MainView.OnLoaded 事件中的 InitializeNavigationAsync() 处理
                    // 这里只确保路由映射已建立，窗口显示后会自动触发导航
                    context.Logger?.LogInfo("导航路由映射完成，等待窗口显示后自动导航");
                    
                    // 可选：如果窗口已显示，可以立即导航
                    bool isWindowVisible = false;
                    await dispatcher.InvokeAsync(() =>
                    {
                        isWindowVisible = mainView.IsVisible || mainView.IsLoaded;
                    }, DispatcherPriority.Loaded);

                    if (isWindowVisible)
                    {
                        try
                        {
                            // 如果窗口已显示，立即导航
                            var navResult = await navManager.NavigateAsync(defaultPageKey);
                            if (navResult.Success)
                            {
                                context.Logger?.LogInfo($"导航到默认页面成功: {defaultPageKey}");
                                
                                await dispatcher.InvokeAsync(() =>
                                {
                                    var vm2 = mainView.DataContext as MainViewViewModel;
                                    vm2?.Navigation?.SetSelectedByKey(defaultPageKey);
                                }, DispatcherPriority.Loaded);
                            }
                            else
                            {
                                context.Logger?.LogWarning($"导航到默认页面失败: {navResult.Message}");
                            }
                        }
                        catch (Exception navEx)
                        {
                            logger?.LogError(navEx, "导航到默认页面失败");
                            // 导航失败不影响任务完成，只记录错误
                        }
                    }
                    else
                    {
                        context.Logger?.LogInfo("窗口尚未显示，导航将在窗口 Loaded 事件中自动触发");
                    }
                }
                else
                {
                    context.Logger?.LogWarning("INavigationManager 未注册");
                    ReportProgress(progress, 100, "INavigationManager 未注册");
                    return;
                }
            }
            catch (Exception ex)
            {
                context.Logger?.LogError("导航初始化失败",ex);
                logger?.LogError(ex, "导航初始化过程中发生错误");
                // 非关键任务，不抛出异常，只记录错误
            }

            ReportProgress(progress, 100, "导航初始化完成");
        }
    }
}


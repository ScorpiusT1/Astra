using Astra.ViewModels;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using NavStack.Configuration;
using NavStack.Modularity;
using Astra.Core.Access;
using Astra.Core.Access.Models; // 添加权限引用

namespace Astra.Models
{
    /// <summary>
    /// 核心模块 - 实现 INavigationModule 和 INavigationModuleLifecycle 和 INavigationMenuProvider，主要用于注册导航模块
    /// </summary>
    public class NavigationModule : INavigationModule, INavigationModuleLifecycle, INavigationMenuProvider
    {
        public string ModuleName => "Navigation";

        public NavigationModule()
        {
            System.Diagnostics.Debug.WriteLine("[NavigationModule] 构造函数被调用");
        }

        // ===== INavigationModule 接口实现 =====

        public void RegisterTypes(INavigationConfiguration configuration, IServiceCollection services)
        {
            // 注册页面类型到导航配置
            // 注意：路由映射在 NavigationBootstrapper 中完成
            configuration.RegisterPage<HomeView, HomeViewModel>(NavigationKeys.Home);
            configuration.RegisterPage<SequenceView, SequenceViewModel>(NavigationKeys.Sequence);
            configuration.RegisterPage<PermissionView, PermissionViewModel>(NavigationKeys.Permission); // ⭐ 使用重构后的 ViewModel
            configuration.RegisterPage<ConfigView, ConfigViewModel>(NavigationKeys.Config);
            configuration.RegisterPage<DebugView, DebugViewModel>(NavigationKeys.Debug);

            System.Diagnostics.Debug.WriteLine($"[{ModuleName}] 注册类型完成");
        }

        // ===== INavigationModuleLifecycle 接口实现 =====

        public void OnInitialized(IServiceProvider serviceProvider)
        {
            System.Diagnostics.Debug.WriteLine($"[{ModuleName}] 初始化完成");

            // 可以在这里获取需要的服务并进行初始化
            // var someService = serviceProvider.GetService<ISomeService>();
        }

        public void OnShutdown()
        {
            System.Diagnostics.Debug.WriteLine($"[{ModuleName}] 关闭完成");

            // 清理资源
        }

        // ===== INavigationMenuProvider 接口实现 =====

        public IEnumerable<NavigationMenuItem> GetMenuItems()
        {
            return new[]
            {
                new NavigationMenuItem
                {
                    Title = "首页",
                    NavigationKey = NavigationKeys.Home,
                    Icon = "Home", // FontAwesome
                    Order = 1,
                    Description = "系统首页",
                    Group = "Core",
                    RequiredPermissionLevel = 0, // 所有用户可访问
                    PermissionDeniedMessage = "您没有权限访问首页"
                },
              
                new NavigationMenuItem
                {
                    Title = "配置",
                    NavigationKey = NavigationKeys.Config,
                    Icon = "Cog", // FontAwesome (设置/齿轮)
                    Order = 2,
                    Description = "系统配置",
                    Group = "Core",
                    RequiredPermissionLevel = (int)UserRole.Engineer, // 工程师及以上
                    PermissionDeniedMessage = "需要工程师或管理员权限才能修改系统配置"
                },
                new NavigationMenuItem
                {
                    Title = "调试",
                    NavigationKey = NavigationKeys.Debug,
                    Icon = "Wrench", // FontAwesome
                    Order = 3,
                    Description = "调试和诊断工具",
                    Group = "Core",
                    RequiredPermissionLevel = (int)UserRole.Engineer, // 工程师及以上
                    PermissionDeniedMessage = "需要工程师或管理员权限才能使用调试功能"
                },
                  new NavigationMenuItem
                {
                    Title = "流程",
                    NavigationKey = NavigationKeys.Sequence,
                    Icon = "List", // FontAwesome
                    Order = 4,
                    Description = "配置测试流程",
                    Group = "Core",
                    RequiredPermissionLevel = (int)UserRole.Engineer, // 工程师及以上
                    PermissionDeniedMessage = "需要工程师或管理员权限才能配置测试流程"
                },
                new NavigationMenuItem
                {
                    Title = "权限",
                    NavigationKey = NavigationKeys.Permission,
                    Icon = "Users", // FontAwesome
                    Order = 50,
                    Description = "管理用户权限",
                    Group = "Core",
                    RequiredPermissionLevel = 3, // 仅管理员和超级管理员（管理员=3，超级管理员=4）
                    PermissionDeniedMessage = "只有管理员才能管理用户权限"
                },
            };
        }
    }
}

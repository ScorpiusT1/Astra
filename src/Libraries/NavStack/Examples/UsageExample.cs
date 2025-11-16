using NavStack.Configuration;
using NavStack.Extensions;
using NavStack.Modularity;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace NavStack.Examples
{
    public class UsageExample
    {
        // 示例1：手动注册模块
        public static IServiceProvider Example1_ManualRegistration()
        {
            var services = new ServiceCollection();

            services.AddStackNav((moduleManager, config) =>
            {
                // 注册主应用页面
                //config.RegisterPage<HomePage, HomeViewModel>("Home");

                // 注册外部模块
                //moduleManager.RegisterModule(new UserManagementModule());
                //moduleManager.RegisterModule(new ProductModule());
                //moduleManager.RegisterModule(new OrderModule());
            });

            return services.BuildServiceProvider();
        }

        // 示例2：自动发现模块
        public static IServiceProvider Example2_AutoDiscovery()
        {
            var services = new ServiceCollection();

            services.AddStackNavWithAutoDiscovery();

            return services.BuildServiceProvider();
        }

        // 示例3：从目录加载模块
        public static IServiceProvider Example3_LoadFromDirectory()
        {
            var services = new ServiceCollection();

            services.AddStackNav((moduleManager, config) =>
            {
                // 从插件目录加载
                var modules = NavigationModuleLoader.LoadFromDirectory("./Plugins");
                foreach (var module in modules)
                {
                    moduleManager.RegisterModule(module);
                }
            });

            return services.BuildServiceProvider();
        }

        // 示例4：使用菜单聚合器
        public static void Example4_UseMenuAggregator(IServiceProvider serviceProvider)
        {
            var menuAggregator = serviceProvider.GetService<NavigationMenuAggregator>();

            // 获取所有菜单项
            var allMenus = menuAggregator.GetAllMenuItems();

            // 获取分组菜单
            var groupedMenus = menuAggregator.GetGroupedMenuItems();

            foreach (var group in groupedMenus)
            {
                Console.WriteLine($"Group: {group.Key}");
                foreach (var item in group)
                {
                    Console.WriteLine($"  - {item.Title} -> {item.NavigationKey}");
                }
            }
        }
    }

   
}

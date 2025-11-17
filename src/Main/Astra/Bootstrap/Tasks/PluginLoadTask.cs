using Astra.Bootstrap.Core;
using Astra.Core.Plugins.Host;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Devices.Management;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Tasks
{
    /// <summary>
    /// 插件加载任务
    /// 负责在应用启动时发现、验证和加载所有插件
    /// </summary>
    public class PluginLoadTask : BootstrapTaskBase
    {
        private readonly string _pluginDirectory;

        public PluginLoadTask(string pluginDirectory = null)
        {
            _pluginDirectory = pluginDirectory ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        }

        public override string Name => "插件加载";
        public override string Description => "正在加载插件...";
        public override double Weight => 1.5;
        public override int Priority => 50;
        public override bool IsCritical => false; // 插件加载失败不影响主程序

        protected override async Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            ReportProgress(progress, 5, "检查插件目录...");

            // 检查插件目录是否存在
            if (!Directory.Exists(_pluginDirectory))
            {
                context.Logger?.LogWarning($"插件目录不存在：{_pluginDirectory}，将创建目录");
                try
                {
                    Directory.CreateDirectory(_pluginDirectory);
                }
                catch (Exception ex)
                {
                    context.Logger?.LogError($"创建插件目录失败：{ex.Message}", ex);
                    ReportProgress(progress, 100, "插件目录创建失败");
                    return;
                }
            }

            ReportProgress(progress, 10, "初始化插件宿主...");

            // 创建插件宿主配置
            var hostConfig = new HostConfiguration
            {
                Performance = new PerformanceConfiguration
                {
                    MaxConcurrentDiscoveries = 4,
                    MaxConcurrentLoads = 2,
                    EnableConcurrencyControl = true,
                    EnableCaching = true
                },
                Security = new SecurityConfiguration
                {
                    RequireSignature = false,
                    EnableSandbox = false
                }
            };

            // 从 BootstrapContext 获取服务集合，如果没有则创建新的
            var services = context.Services;
            if (services == null)
            {
                context.Logger?.LogWarning("服务集合未初始化，创建临时服务集合");
                services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            }

            // ⭐ 简化检查：只检查服务描述符，不构建 ServiceProvider（避免阻塞）
            // 这样可以确保插件系统和主应用使用同一个 IDeviceManager 实例
            try
            {
                // 只检查服务描述符，不构建 ServiceProvider（避免耗时操作）
                var existingDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IDeviceManager));
                if (existingDescriptor != null)
                {
                    if (existingDescriptor.Lifetime == ServiceLifetime.Singleton)
                    {
                        context.Logger?.LogInfo("IDeviceManager 已注册为单例，插件系统将使用同一实例");
                        System.Diagnostics.Debug.WriteLine("[PluginLoadTask] IDeviceManager 已注册为单例");
                    }
                    else
                    {
                        context.Logger?.LogWarning("IDeviceManager 注册方式可能不正确，建议使用单例注册");
                        System.Diagnostics.Debug.WriteLine("[PluginLoadTask] 警告：IDeviceManager 注册方式可能不正确");
                    }
                }
                else
                {
                    context.Logger?.LogWarning("IDeviceManager 未在服务集合中注册，插件可能无法访问设备管理器");
                    System.Diagnostics.Debug.WriteLine("[PluginLoadTask] 警告：IDeviceManager 未注册");
                }
            }
            catch (Exception ex)
            {
                context.Logger?.LogWarning($"检查 IDeviceManager 注册时出错：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PluginLoadTask] 检查 IDeviceManager 注册时出错：{ex.Message}");
            }

            // 创建插件宿主
            IPluginHost pluginHost;

            try
            {
                pluginHost = PluginHostFactory.CreateDefaultHost(services, hostConfig);
                context.Logger?.LogInfo("插件宿主创建成功");
                
                // ⭐ 简化验证：移除反射操作（避免阻塞，验证逻辑已移到插件初始化时）
                // 插件会在初始化时自动从主应用 ServiceProvider 获取 IDeviceManager
                System.Diagnostics.Debug.WriteLine("[PluginLoadTask] 插件宿主创建成功，IDeviceManager 将在插件初始化时自动获取");
            }
            catch (Exception ex)
            {
                context.Logger?.LogError($"创建插件宿主失败：{ex.Message}", ex);
                ReportProgress(progress, 100, "插件宿主创建失败");
                return;
            }

            // 将插件宿主注册到服务容器中，方便后续从 DI 容器获取
            // 注意：服务提供者是在所有任务执行完成后才构建的，所以这里可以安全地注册
            services.AddSingleton(pluginHost);
            context.Logger?.LogInfo("PluginHost 已注册到服务集合");
            
            // 可选：同时保存到 BootstrapContext 作为后备方案（如果其他代码需要直接访问）
            // context.SetData("PluginHost", pluginHost);

            ReportProgress(progress, 20, "扫描插件目录...");

            // 检查是否有插件文件
            var pluginDirs = Directory.GetDirectories(_pluginDirectory);
            if (pluginDirs.Length == 0)
            {
                context.Logger?.LogInfo("未发现插件目录");
                ReportProgress(progress, 100, "未发现插件");
                return;
            }

            // 查找所有 .addin 文件
            var addinFiles = new List<string>();
            foreach (var dir in pluginDirs)
            {
                var addinFilesInDir = Directory.GetFiles(dir, "*.addin", SearchOption.TopDirectoryOnly);
                addinFiles.AddRange(addinFilesInDir);
            }

            if (addinFiles.Count == 0)
            {
                context.Logger?.LogInfo("未发现插件清单文件");
                ReportProgress(progress, 100, "未发现插件");
                return;
            }

            ReportProgress(progress, 30, $"发现 {addinFiles.Count} 个插件...");

            try
            {
                System.Diagnostics.Debug.WriteLine($"[PluginLoadTask] 开始发现并加载插件，目录: {_pluginDirectory}");
                
                // 使用插件宿主发现并加载插件
                await pluginHost.DiscoverAndLoadPluginsAsync(_pluginDirectory);

                // 获取已加载的插件列表
                var loadedPlugins = pluginHost.LoadedPlugins;
                var loadedCount = loadedPlugins.Count;

                System.Diagnostics.Debug.WriteLine($"[PluginLoadTask] 插件加载完成，已加载 {loadedCount} 个插件");
                
                // 检查每个插件的状态
                foreach (var plugin in loadedPlugins)
                {
                    System.Diagnostics.Debug.WriteLine($"[PluginLoadTask] 插件: {plugin.Name} (ID: {plugin.Id}, Version: {plugin.Version})");
                }

                context.SetData("LoadedPlugins", loadedCount);
                context.SetData("PluginList", loadedPlugins.Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    Version = p.Version
                }).ToList());

                if (loadedCount > 0)
                {
                    var pluginNames = string.Join(", ", loadedPlugins.Select(p => p.Name));
                    context.Logger?.LogInfo($"成功加载 {loadedCount} 个插件: {pluginNames}");
                    ReportProgress(progress, 100, $"已加载 {loadedCount} 个插件");
                    
                    // ⭐ 注意：不在这里检查设备数量，因为 BuildServiceProvider() 会阻塞 UI
                    // 设备数量检查已在插件内部完成，并在调试输出中显示
                    System.Diagnostics.Debug.WriteLine($"[PluginLoadTask] 插件加载完成，设备注册情况请查看插件日志");
                }
                else
                {
                    context.Logger?.LogWarning("未成功加载任何插件");
                    ReportProgress(progress, 100, "未成功加载插件");
                }
            }
            catch (Exception ex)
            {
                context.Logger?.LogError($"加载插件时发生错误：{ex.Message}", ex);
                ReportProgress(progress, 100, $"插件加载失败: {ex.Message}");
                
                // 非关键任务，不抛出异常，只记录错误
            }
        }
    }
}

using Astra.Bootstrap.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Tasks
{
    /// <summary>
    /// 插件加载任务
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
            ReportProgress(progress, 10, "扫描插件目录...");

            await Task.Delay(1000, cancellationToken); // 模拟扫描

            //if (!Directory.Exists(_pluginDirectory))
            //{
            //    context.Logger?.LogWarning($"插件目录不存在：{_pluginDirectory}");
            //    return;
            //}

            //var pluginFiles = Directory.GetFiles(_pluginDirectory, "*.dll");

            //if (pluginFiles.Length == 0)
            //{
            //    ReportProgress(progress, 100, "未发现插件");
            //    return;
            //}

            //ReportProgress(progress, 30, $"发现 {pluginFiles.Length} 个插件...");

            //var loadedPlugins = 0;
            //foreach (var pluginFile in pluginFiles)
            //{
            //    var fileName = Path.GetFileName(pluginFile);
            //    ReportProgress(progress, 30 + (loadedPlugins * 60 / pluginFiles.Length),
            //        $"加载插件：{fileName}");

            //    await Task.Delay(100, cancellationToken); // 模拟加载

            //    // 实际插件加载逻辑
            //    // var assembly = Assembly.LoadFrom(pluginFile);
            //    // ...

            //    loadedPlugins++;
            //}

            //context.SetData("LoadedPlugins", loadedPlugins);

            //ReportProgress(progress, 100, $"已加载 {loadedPlugins} 个插件");
        }
    }
}

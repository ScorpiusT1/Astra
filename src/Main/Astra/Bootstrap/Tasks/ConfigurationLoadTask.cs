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
    /// 配置加载任务
    /// </summary>
    public class ConfigurationLoadTask : BootstrapTaskBase
    {
        private readonly string _configPath;

        public ConfigurationLoadTask(string configPath = null)
        {
            _configPath = configPath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        public override string Name => "配置加载";
        public override string Description => "正在加载应用程序配置...";
        public override double Weight => 1.0;
        public override int Priority => 10; // 高优先级
        public override bool IsCritical => true;

        protected override async Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            ReportProgress(progress, 10, "检查配置文件...");

            //// 检查文件是否存在
            //if (!File.Exists(_configPath))
            //{
            //    throw new FileNotFoundException($"配置文件不存在：{_configPath}");
            //}

            //ReportProgress(progress, 30, "读取配置文件...");
            await Task.Delay(500, cancellationToken); // 模拟IO操作

            //// 读取配置
            //var configContent = await File.ReadAllTextAsync(_configPath, cancellationToken);

            //ReportProgress(progress, 60, "解析配置...");
            //await Task.Delay(200, cancellationToken);

            //// 解析配置（这里简化处理，实际应使用配置框架）
            //// var config = JsonSerializer.Deserialize<AppConfig>(configContent);

            //ReportProgress(progress, 90, "应用配置...");

            //// 将配置保存到上下文
            //context.SetData("ConfigPath", _configPath);
            //context.SetData("ConfigContent", configContent);

            //ReportProgress(progress, 100, "配置加载完成");
        }

        public override async Task RollbackAsync(BootstrapContext context)
        {
            // 清理配置数据
            context.SetData<string>("ConfigPath", null);
            context.SetData<string>("ConfigContent", null);

            await Task.CompletedTask;
        }
    }
}

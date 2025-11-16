using Astra.Bootstrap.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Tasks
{
    /// <summary>
    /// 服务注册任务
    /// </summary>
    public class ServiceRegistrationTask : BootstrapTaskBase
    {
        private readonly Action<IServiceCollection> _configureServices;

        public ServiceRegistrationTask(Action<IServiceCollection> configureServices)
        {
            _configureServices = configureServices ?? throw new ArgumentNullException(nameof(configureServices));
        }

        public override string Name => "服务注册";
        public override string Description => "正在注册应用程序服务...";
        public override double Weight => 2.0;
        public override int Priority => 20;
        public override bool IsCritical => true;

        protected override async Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            ReportProgress(progress, 10, "初始化服务容器...");

            if (context.Services == null)
            {
                context.Services = new ServiceCollection();
            }

            ReportProgress(progress, 30, "注册核心服务...");
            await Task.Delay(100, cancellationToken);

            // 执行服务配置
            _configureServices(context.Services);

            ReportProgress(progress, 70, "验证服务配置...");
            await Task.Delay(100, cancellationToken);

            // 可以在这里进行服务配置验证

            ReportProgress(progress, 100, "服务注册完成");
        }
    }
}

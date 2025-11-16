using Astra.Bootstrap.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Astra.Bootstrap.Tasks
{
    /// <summary>
    /// 主窗口创建任务
    /// </summary>
    public class MainWindowCreationTask<TWindow> : BootstrapTaskBase where TWindow : Window
    {
        public override string Name => "创建主窗口";
        public override string Description => "正在创建主窗口...";
        public override double Weight => 0.5;
        public override int Priority => 1000; // 最低优先级，最后执行
        public override bool IsCritical => true;

        protected override async Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            ReportProgress(progress, 30, "准备创建主窗口...");

            // 确保服务提供者已构建
            if (context.ServiceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider 未初始化");
            }

            ReportProgress(progress, 60, $"创建 {typeof(TWindow).Name}...");

            // 在 UI 线程创建窗口
            TWindow mainWindow = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mainWindow = context.ServiceProvider.GetRequiredService<TWindow>();
            });

            if (mainWindow == null)
            {
                throw new InvalidOperationException($"无法创建主窗口 {typeof(TWindow).Name}");
            }

            // 保存到上下文
            context.SetData("MainView", mainWindow);

            ReportProgress(progress, 100, "主窗口创建完成");
        }
    }
}

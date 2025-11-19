using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Bootstrap.Services
{
    /// <summary>
    /// 泛型应用程序启动管理器
    /// </summary>
    public class ApplicationBootstrapper<TMainWindow> : ApplicationBootstrapper
        where TMainWindow : Window
    {
        /// <summary>
        /// 运行并创建主窗口
        /// </summary>
        public new async Task<BootstrapResult<TMainWindow>> RunAsync()
        {
            var baseResult = await base.RunAsync();
            var result = BootstrapResult<TMainWindow>.FromBase(baseResult);

            if (baseResult.IsSuccess)
            {
                try
                {
                    var context = GetContext();
                    context.Logger?.LogInfo($"准备创建主窗口: {typeof(TMainWindow).Name}");

                    // ⭐ 确保在 UI 线程创建窗口
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        context.Logger?.LogInfo("开始创建主窗口实例...");

                        if (context.ServiceProvider == null)
                        {
                            throw new InvalidOperationException("ServiceProvider 未初始化，无法创建主窗口");
                        }

                        try
                        {
                            // 从 DI 容器获取主窗口
                            result.MainView = context.ServiceProvider.GetRequiredService<TMainWindow>();

                            context.Logger?.LogInfo($"主窗口创建成功: {result.MainView != null}");

                            if (result.MainView != null)
                            {
                                context.Logger?.LogInfo($"窗口类型: {result.MainView.GetType().FullName}");
                                context.Logger?.LogInfo($"窗口标题: {result.MainView.Title}");
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Logger?.LogError("GetRequiredService<TMainView> 失败", ex);
                            throw;
                        }
                    }, DispatcherPriority.Send);

                    if (result.MainView == null)
                    {
                        throw new InvalidOperationException("主窗口创建后为 null");
                    }
                }
                catch (Exception ex)
                {
                    var context = GetContext();
                    context.Logger?.LogError("创建主窗口失败", ex);
                    result.IsSuccess = false;
                    result.FatalException = ex;
                }
            }

            return result;
        }
    }


    /// <summary>
    /// 带主窗口的启动结果
    /// </summary>
    public class BootstrapResult<TWindow> : BootstrapResult where TWindow : Window
    {
        public TWindow MainView { get; set; }

        /// <summary>
        /// 从基础结果创建（内部使用）
        /// </summary>
        internal static BootstrapResult<TWindow> FromBase(BootstrapResult source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new BootstrapResult<TWindow>
            {
                IsSuccess = source.IsSuccess,
                IsCancelled = source.IsCancelled,
                TotalTime = source.TotalTime,
                FatalException = source.FatalException
            };

            return result;
        }
    }
}

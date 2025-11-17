using System.Windows.Controls;
using Astra.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Astra.Views
{
    /// <summary>
    /// ConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();
            
            // NavStack 框架会在创建页面时自动设置 DataContext
            // 但为了确保在构造函数中也能访问 ViewModel，我们可以从 DI 容器获取
            // 注意：如果 NavStack 已经设置了 DataContext，这里不会覆盖它
            
            // 在 Loaded 事件中设置配置内容区域（此时 DataContext 肯定已经设置）
            Loaded += ConfigView_Loaded;
        }

        private void ConfigView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ConfigView] ConfigView_Loaded: 视图已加载");
            
            // NavStack 框架会在创建页面时自动设置 DataContext
            // 如果 DataContext 还没有设置，尝试从 DI 容器获取
            if (DataContext == null && App.ServiceProvider != null)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigView] DataContext 为 null，从 ServiceProvider 获取");
                DataContext = App.ServiceProvider.GetService<ConfigViewModel>();
            }

            // 设置配置内容区域
            if (DataContext is ConfigViewModel viewModel)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigView] ViewModel 已设置，设置配置内容区域");
                viewModel.SetConfigContentRegion(ConfigContentRegion);
                
                // View 加载时刷新配置树（确保获取到最新的设备列表）
                // 延迟一点时间，确保插件已经加载完成
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("[ConfigView] 延迟刷新配置树（等待插件加载）");
                    
                    // 等待更长时间，确保插件和设备都已注册
                    await System.Threading.Tasks.Task.Delay(3000); // 延迟 3 秒，等待插件加载完成
                    
                    // 检查设备数量
                    if (viewModel is ConfigViewModel configViewModel)
                    {
                        var deviceManager = App.ServiceProvider?.GetService<Astra.Core.Devices.Management.IDeviceManager>();
                        if (deviceManager != null)
                        {
                            var deviceCount = deviceManager.GetDeviceCount();
                            System.Diagnostics.Debug.WriteLine($"[ConfigView] 延迟后检查设备数量: {deviceCount}");
                            System.Diagnostics.Debug.WriteLine($"[ConfigView] DeviceManager 实例哈希码: {deviceManager.GetHashCode()}");
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[ConfigView] 执行刷新配置树命令");
                    viewModel.RefreshConfigTreeCommand.Execute(null);
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ConfigView] 警告：DataContext 不是 ConfigViewModel 类型");
            }
        }

        private void ConfigContentRegion_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ConfigViewModel viewModel && sender is ContentControl contentControl)
            {
                viewModel.SetConfigContentRegion(contentControl);
            }
        }
    }
}

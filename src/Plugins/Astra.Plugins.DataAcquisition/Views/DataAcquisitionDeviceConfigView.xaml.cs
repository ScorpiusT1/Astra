using Astra.Plugins.DataAcquisition.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Astra.Plugins.DataAcquisition.Views
{
    /// <summary>
    /// DataAcquisitionDeviceConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class DataAcquisitionDeviceConfigView : UserControl
    {
        public DataAcquisitionDeviceConfigView()
        {
            InitializeComponent();

            // 监听 DataContext 变化
            this.DataContextChanged += DataAcquisitionDeviceConfigView_DataContextChanged;
            
            // 监听子控件的 Loaded 事件，确保在它们加载时设置 DataContext
            this.Loaded += DataAcquisitionDeviceConfigView_Loaded;
            
            // 在 InitializeComponent 之后，尝试立即设置（如果控件已经创建）
            // 使用 Dispatcher 确保在控件完全初始化后执行
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetupChildDataContexts();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void DataAcquisitionDeviceConfigView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 立即更新子控件的 DataContext
            SetupChildDataContexts();
        }

        private void DataAcquisitionDeviceConfigView_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保子控件的 DataContext 已设置
            SetupChildDataContexts();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 当 TabItem 被选中时，确保其内容的 DataContext 已设置
            // 这对于延迟加载的 TabItem 内容很重要
            SetupChildDataContexts();
        }

        private void SetupChildDataContexts()
        {
            if (this.DataContext is not DataAcquisitionDeviceConfigViewModel vm)
            {
                return;
            }

            if (vm.Config == null)
            {
                return;
            }

            // 设置子控件的 DataContext
            // 只有在还没有设置或者 Config 发生变化时才重新设置
            if (baseConfig != null)
            {
                if (baseConfig.DataContext is not DataAcquisitionBaseConfigViewModel baseVm || 
                    baseVm.Config != vm.Config)
                {
                    baseConfig.DataContext = new DataAcquisitionBaseConfigViewModel(vm.Config);
                }
            }

            if (channelConfig != null)
            {
                if (channelConfig.DataContext is not DataAcquisitionChannelConfigViewModel channelVm || 
                    channelVm.Config != vm.Config)
                {
                    channelConfig.DataContext = new DataAcquisitionChannelConfigViewModel(vm.Config);
                }
            }
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using Astra.ViewModels;

namespace Astra.Views
{
    /// <summary>
    /// SequenceView.xaml 的交互逻辑
    /// </summary>
    public partial class SequenceView : UserControl
    {
        public SequenceView()
        {
            InitializeComponent();
        }

        private void SequenceView_OnLoaded(object sender, RoutedEventArgs e)
        {
            // 与 IPluginHost 加载插件的时序对齐：VM 构造可能早于 LoadedPlugins 就绪，此处再刷新一次工具箱。
            if (DataContext is SequenceViewModel vm)
            {
                vm.MultiFlowEditor?.RefreshPluginToolBoxFromPlugins();
            }
        }
    }
}

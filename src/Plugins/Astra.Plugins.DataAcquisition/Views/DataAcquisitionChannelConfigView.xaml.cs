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
    /// DataAcquisitionChannelConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class DataAcquisitionChannelConfigView : UserControl
    {
        public DataAcquisitionChannelConfigView()
        {
            InitializeComponent();
            this.Loaded += DataAcquisitionChannelConfigView_Loaded;
            
            // 如果 DataContext 还没有设置，尝试从父级获取并设置
            // 使用 Dispatcher 确保在控件完全初始化后执行
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EnsureDataContext();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void DataAcquisitionChannelConfigView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 确保 DataContext 已设置
            EnsureDataContext();
        }

        private void EnsureDataContext()
        {
            // 如果 DataContext 还没有设置，尝试从父级获取
            if (this.DataContext == null)
            {
                var parent = this.Parent;
                while (parent != null)
                {
                    if (parent is DataAcquisitionDeviceConfigView parentView)
                    {
                        // 父级视图会在适当的时候设置我们的 DataContext
                        break;
                    }
                    parent = LogicalTreeHelper.GetParent(parent) ?? VisualTreeHelper.GetParent(parent);
                }
            }
        }

        /// <summary>
        /// 采样率 ComboBox 选择变化事件处理
        /// 当从下拉列表选择时，更新 Text 显示
        /// </summary>
        private void SampleRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is double selectedValue)
            {
                // 当从列表选择时，更新 Text 显示为 KHz 格式
                var converter = new Commons.HzToKHzConverter();
                comboBox.Text = converter.Convert(selectedValue, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture)?.ToString();
            }
        }
    }
}

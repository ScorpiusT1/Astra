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

            this.Loaded -= DataAcquisitionDeviceConfigView_Loaded;
            this.Loaded += DataAcquisitionDeviceConfigView_Loaded;
        }

        private void DataAcquisitionDeviceConfigView_Loaded(object sender, RoutedEventArgs e)
        {
            if(this.DataContext is not DataAcquisitionDeviceConfigViewModel vm)
            {
                return;
            }

            baseConfig.DataContext = new DataAcquisitionBaseConfigViewModel(vm.Config);
            channelConfig.DataContext = new DataAcquisitionChannelConfigViewModel(vm.Config);
        }
    }
}

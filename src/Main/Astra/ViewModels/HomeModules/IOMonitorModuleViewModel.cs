using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Astra.ViewModels.HomeModules
{
    public partial class IOMonitorModuleViewModel : ObservableObject
    {
        public ObservableCollection<IOPointItem> Points { get; } = new();

        public IOMonitorModuleViewModel()
        {
            InitializeDefaultPoints();
        }

        private void InitializeDefaultPoints()
        {
            // 模拟 8 个 IO 点位，统一在一个集合中展示
            Points.Add(new IOPointItem { Name = "DI_急停", Value = "False", IsOn = false });
            Points.Add(new IOPointItem { Name = "DI_安全门", Value = "True", IsOn = true });
            Points.Add(new IOPointItem { Name = "DO_蜂鸣器", Value = "False", IsOn = false });
            Points.Add(new IOPointItem { Name = "AO_风机频率", Value = "45 Hz", IsOn = true });
            Points.Add(new IOPointItem { Name = "DI_气压开关", Value = "True", IsOn = true });
            Points.Add(new IOPointItem { Name = "DI_治具到位", Value = "False", IsOn = false });
            Points.Add(new IOPointItem { Name = "DO_夹具气缸", Value = "True", IsOn = true });
            Points.Add(new IOPointItem { Name = "AO_加热温度", Value = "72 °C", IsOn = true });
        }
    }

    public partial class IOPointItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private bool _isOn;
    }
}

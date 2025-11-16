using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.ViewModels
{
    public partial class ConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "配置管理";

        public ConfigViewModel()
        {
            // 初始化配置管理相关的属性和命令
        }
    }
}

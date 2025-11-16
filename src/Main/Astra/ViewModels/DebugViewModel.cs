using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.ViewModels
{
    public partial class DebugViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "调试工具";

        public DebugViewModel()
        {
            // 初始化调试工具相关的属性和命令
        }
    }
}

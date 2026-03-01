using System;
using Astra.Core.Devices.Interfaces;

namespace Astra.Core.Plugins.UI
{
    /// <summary>
    /// 在正确的插件加载上下文中创建配置/调试 View 与 ViewModel，供宿主展示。
    /// 宿主应将 View 转为 <c>UserControl</c>/<c>FrameworkElement</c> 使用。
    /// </summary>
    public interface IPluginViewFactory
    {
        /// <summary>
        /// 根据 View/ViewModel 类型与配置或设备实例创建界面。
        /// </summary>
        (object View, object ViewModel) CreateView(Type viewType, Type viewModelType, object configOrDevice);

        /// <summary>
        /// 根据设备类型上的 DeviceConfigUIAttribute 创建配置界面；未标记则返回 (null, null)。
        /// </summary>
        (object View, object ViewModel) CreateConfigViewForDevice(IDevice device);

        /// <summary>
        /// 根据设备类型上的 DeviceDebugUIAttribute 创建调试界面；未标记则返回 (null, null)。
        /// </summary>
        (object View, object ViewModel) CreateDebugViewForDevice(IDevice device);
    }
}

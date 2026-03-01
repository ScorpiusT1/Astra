namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 由插件提供的配置/调试 View 实现，宿主在创建 View 后调用以注入当前设备实例。
    /// </summary>
    public interface IDeviceAwareView
    {
        void AttachDevice(IDevice device);
    }
}

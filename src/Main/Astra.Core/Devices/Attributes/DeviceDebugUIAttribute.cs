namespace Astra.Core.Devices.Attributes
{
    /// <summary>
    /// 设备调试界面特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DeviceDebugUIAttribute : Attribute
    {
        public DeviceDebugUIAttribute(Type viewType, Type? viewModelType = null)
        {
            ViewType = viewType;
            ViewModelType = viewModelType;
        }

        public Type ViewType { get; }
        public Type? ViewModelType { get; }
    }
}

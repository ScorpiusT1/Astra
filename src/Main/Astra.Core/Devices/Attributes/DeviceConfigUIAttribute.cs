namespace Astra.Core.Devices.Attributes
{
    /// <summary>
    /// 设备配置界面特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DeviceConfigUIAttribute : Attribute
    {
        public DeviceConfigUIAttribute(Type viewType, Type? viewModelType = null)
        {
            ViewType = viewType;
            ViewModelType = viewModelType;
        }

        /// <summary>
        /// 配置界面视图类型（必须继承自 UserControl）
        /// </summary>
        public Type ViewType { get; }

        /// <summary>
        /// 配置界面 ViewModel 类型（可选）
        /// </summary>
        public Type? ViewModelType { get; }
    }
}

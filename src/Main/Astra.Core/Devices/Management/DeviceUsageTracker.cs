namespace Astra.Core.Devices.Management
{
    /// <summary>
    /// 设备使用服务全局访问器
    /// </summary>
    public static class DeviceUsageTracker
    {
        private static IDeviceUsageService _current = DeviceUsageService.Null;

        public static IDeviceUsageService Current => _current;

        public static void SetService(IDeviceUsageService service)
        {
            _current = service ?? DeviceUsageService.Null;
        }
    }
}



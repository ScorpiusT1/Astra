using System;
using System.Collections.Generic;

namespace Astra.Core.Devices
{
    public class DataReceivedEventArgs : EventArgs
    {
        public DeviceMessage Message { get; set; }
        public string DeviceId { get; set; }
    }

    public class DeviceStatusChangedEventArgs : EventArgs
    {
        public string Id { get; set; }
        public DeviceStatus OldStatus { get; set; }
        public DeviceStatus NewStatus { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class DeviceErrorEventArgs : EventArgs
    {
        public string DeviceId { get; set; }
        public Exception Exception { get; set; }
        public string ErrorMessage { get; set; }
        public int ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 属性变更事件参数
    /// </summary>
    public class PropertyChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
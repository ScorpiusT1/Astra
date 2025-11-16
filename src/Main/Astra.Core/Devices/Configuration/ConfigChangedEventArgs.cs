using System;
using System.Collections.Generic;

namespace Astra.Core.Devices.Configuration
{
    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class ConfigChangedEventArgs<TConfig> : EventArgs where TConfig : DeviceConfig
    {
        public TConfig OldConfig { get; set; }
        public TConfig NewConfig { get; set; }
        public List<string> ChangedProperties { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ChangedBy { get; set; }
    }

    /// <summary>
    /// 配置变更记录
    /// </summary>
    public class ConfigChangeRecord
    {
        public DateTime Timestamp { get; set; }
        public string ChangedBy { get; set; }
        public List<string> ChangedProperties { get; set; }
        public Dictionary<string, object> OldValues { get; set; }
        public Dictionary<string, object> NewValues { get; set; }
    }
}
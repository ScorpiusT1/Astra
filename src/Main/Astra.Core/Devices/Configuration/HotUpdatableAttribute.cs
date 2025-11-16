using System;

namespace Astra.Core.Devices.Configuration
{
    [AttributeUsage(AttributeTargets.Property)]
    public class HotUpdatableAttribute : Attribute
    {
        public string Description { get; set; }
        public HotUpdatableAttribute(string description = null)
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RequireRestartAttribute : Attribute
    {
        public string Reason { get; set; }
        public RequireRestartAttribute(string reason = null)
        {
            Reason = reason ?? "此配置变更需要重启设备才能生效";
        }
    }
}
namespace Astra.Core.Devices.Configuration
{
    /// <summary>
    /// 标记属性可热更新
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class HotUpdatableAttribute : Attribute
    {
    }

    /// <summary>
    /// 标记属性需要重启设备生效
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RequireRestartAttribute : Attribute
    {
        public RequireRestartAttribute(string reason = null)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }
}


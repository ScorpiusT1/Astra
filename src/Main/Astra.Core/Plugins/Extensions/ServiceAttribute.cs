using Astra.Core.Plugins.Services;

namespace Astra.Core.Plugins.Extensions
{
    /// <summary>
    /// 服务标记特性 - 用于自动注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceAttribute : Attribute
    {
        public Type ServiceType { get; set; }
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

        public ServiceAttribute() { }

        public ServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            ServiceType = serviceType;
            Lifetime = lifetime;
        }
    }
}

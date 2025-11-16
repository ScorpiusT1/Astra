namespace Astra.Core.Plugins.Services
{
    /// <summary>
    /// 增强的服务描述符
    /// </summary>
    public partial class ServiceDescriptor
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public object Instance { get; set; }
        public ServiceLifetime Lifetime { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
}

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 默认配置容器实现
    /// </summary>
    public class ConfigContainer<T> : IConfigContainer<T> where T : class, IConfig
    {
        public List<T> Configs { get; set; } = new List<T>();
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string? Description { get; set; }
    }
}

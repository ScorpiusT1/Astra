namespace Addins.Core.Abstractions
{
    /// <summary>
    /// 插件基础接口 - 单一职责原则
    /// </summary>
    public interface IPlugin : IDisposable
    {
        string Id { get; }
        string Name { get; }
        Version Version { get; }

        Task InitializeAsync(IPluginContext context);
        Task StartAsync();
        Task StopAsync();
    }
}

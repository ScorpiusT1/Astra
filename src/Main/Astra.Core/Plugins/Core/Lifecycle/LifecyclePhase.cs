namespace Astra.Core.Plugins.Lifecycle
{
    /// <summary>
    /// 生命周期阶段
    /// </summary>
    public enum LifecyclePhase
    {
        Created,
        Initializing,
        Initialized,
        Starting,
        Running,
        Stopping,
        Stopped,
        Disposing,
        Disposed,
        Failed
    }
}

namespace Astra.Core.Plugins.Lifecycle
{
    /// <summary>
    /// 插件生命周期状态
    /// </summary>
    public class PluginLifecycleState
    {
        public string PluginId { get; set; }
        public LifecyclePhase Phase { get; set; }
        public DateTime LastTransition { get; set; }
        public Exception LastError { get; set; }
        public int StartCount { get; set; }
        public int StopCount { get; set; }
        public int ErrorCount { get; set; }
    }
}

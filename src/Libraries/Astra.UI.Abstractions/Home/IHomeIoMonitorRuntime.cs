using System.Collections.ObjectModel;

namespace Astra.UI.Abstractions.Home
{
    /// <summary>
    /// 首页 IO 监控运行时：根据 IO 配置中勾选的「首页监控」项绑定列表并轮询刷新。
    /// 由 PLC 插件在加载时注册到 <see cref="IoMonitorRuntimeRegistry"/>。
    /// </summary>
    public interface IHomeIoMonitorRuntime
    {
        /// <summary>
        /// 在 UI 线程调用：清空集合并按配置填充监控项，然后开始轮询。
        /// </summary>
        void Attach(ObservableCollection<IoMonitorPointItem> points);

        /// <summary>
        /// 停止轮询并解除绑定。
        /// </summary>
        void Detach();

        /// <summary>
        /// IO 配置（文件）变更并已反映到插件侧缓存后调用：若当前已 <see cref="Attach"/>，则按最新配置重建点位集合并重启轮询。
        /// 应在 UI 线程调用（会修改 <see cref="IoMonitorPointItem"/> 集合）。
        /// </summary>
        void ReloadFromConfiguration();
    }
}

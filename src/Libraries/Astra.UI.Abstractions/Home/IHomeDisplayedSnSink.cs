namespace Astra.UI.Abstractions.Home
{
    /// <summary>
    /// 由宿主 Home 实现：将流程运行中读到的 SN 同步到主页展示（切到 UI 线程）。
    /// 节点/插件通过 DI 注入本接口，避免直接依赖视图模型。
    /// </summary>
    public interface IHomeDisplayedSnSink
    {
        /// <summary>
        /// 更新主页当前展示的 SN；空或仅空白则显示占位 "-"。
        /// </summary>
        Task SetDisplayedSnAsync(string? sn, CancellationToken cancellationToken = default);
    }
}

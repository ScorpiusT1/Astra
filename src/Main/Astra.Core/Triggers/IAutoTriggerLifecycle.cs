namespace Astra.Core.Triggers
{
    /// <summary>
    /// 宿主侧「自动模式下监听触发器」的生命周期：任意插件将 <see cref="ITrigger"/> 注册到
    /// <see cref="Manager.TriggerManager"/> 后应通知宿主，由宿主按当前 Home 运行模式（自动/手动）统一启动或停止轮询。
    /// 适用于 PLC、串口、扫码等所有触发器实现，不绑定某一类设备。
    /// </summary>
    public interface IAutoTriggerLifecycle
    {
        /// <summary>
        /// 触发器集合已注册或已更新（例如配置保存、插件启用）后调用，使当前模式生效。
        /// </summary>
        Task NotifyTriggersRegisteredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 用户切换 Home 自动/手动扫码模式时调用。
        /// </summary>
        Task ApplyCurrentModeAsync(CancellationToken cancellationToken = default);
    }
}

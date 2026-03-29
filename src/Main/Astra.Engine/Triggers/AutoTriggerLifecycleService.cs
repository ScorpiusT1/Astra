using Astra.Core.Triggers;
using Astra.Core.Triggers.Manager;

namespace Astra.Engine.Triggers
{
    /// <summary>
    /// 与具体触发器类型无关：在自动模式下启动 <see cref="TriggerManager"/> 中已注册的全部触发器，手动模式下停止。
    /// </summary>
    public sealed class AutoTriggerLifecycleService : IAutoTriggerLifecycle
    {
        private readonly TriggerManager _triggerManager;
        private readonly IScanModeState _homeScanModeState;

        public AutoTriggerLifecycleService(TriggerManager triggerManager, IScanModeState homeScanModeState)
        {
            _triggerManager = triggerManager;
            _homeScanModeState = homeScanModeState;
        }

        public Task NotifyTriggersRegisteredAsync(CancellationToken cancellationToken = default)
        {
            return ApplyCurrentModeAsync(cancellationToken);
        }

        public async Task ApplyCurrentModeAsync(CancellationToken cancellationToken = default)
        {
            if (_homeScanModeState.IsAutoScanMode)
            {
                await _triggerManager.StartAutoListeningAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _triggerManager.StopAllAsync().ConfigureAwait(false);
            }
        }
    }
}

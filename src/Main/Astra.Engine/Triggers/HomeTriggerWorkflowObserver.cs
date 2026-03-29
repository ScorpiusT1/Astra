using Astra.Core.Triggers;
using Astra.Core.Triggers.Args;

namespace Astra.Engine.Triggers
{
    /// <summary>
    /// 触发器事件观察者：在自动模式下按软件配置将触发器映射到主流程并执行。
    /// </summary>
    public sealed class HomeTriggerWorkflowObserver : ITriggerObserver
    {
        private readonly IScanModeState _scanModeState;
        private readonly ISoftwareConfigAutoTriggerWorkflowResolver _workflowResolver;
        private readonly IAutoTriggerLogSink _log;
        private readonly IAutoTriggerWorkflowHandler _workflowHandler;

        public HomeTriggerWorkflowObserver(
            IScanModeState scanModeState,
            ISoftwareConfigAutoTriggerWorkflowResolver workflowResolver,
            IAutoTriggerLogSink log,
            IAutoTriggerWorkflowHandler workflowHandler)
        {
            _scanModeState = scanModeState;
            _workflowResolver = workflowResolver;
            _log = log;
            _workflowHandler = workflowHandler;
        }

        public async Task HandleTriggerAsync(TriggerEventArgs args)
        {
            if (!_scanModeState.IsAutoScanMode)
            {
                return;
            }

            var triggerId = args.GetTriggerId();
            if (string.IsNullOrWhiteSpace(triggerId))
            {
                return;
            }

            var resolved = await _workflowResolver.ResolveAsync(triggerId).ConfigureAwait(false);
            if (!resolved.ShouldExecute)
            {
                if (!string.IsNullOrWhiteSpace(resolved.SkipMessage))
                {
                    if (resolved.LogSkipAsError)
                    {
                        _log.Error(resolved.SkipMessage);
                    }
                    else
                    {
                        _log.Warn(resolved.SkipMessage);
                    }
                }

                return;
            }

            var path = resolved.MasterWorkflowFilePath?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                _log.Error("自动触发：主流程路径无效。");
                return;
            }

            var sn = args.GetSN();
            await _workflowHandler
                .ExecuteAutoTriggerWorkflowAsync(path, sn, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}

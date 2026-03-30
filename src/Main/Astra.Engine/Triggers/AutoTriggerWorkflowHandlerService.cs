using System;
using Astra.Core.Triggers;

namespace Astra.Engine.Triggers
{
    /// <summary>
    /// 自动触发命中后的主流程执行编排（与 UI 细节通过 <see cref="IAutoTriggerHomeRunContext"/> 分离）。
    /// </summary>
    public sealed class AutoTriggerWorkflowHandlerService : IAutoTriggerWorkflowHandler
    {
        private readonly IAutoTriggerHomeRunContext _homeRunContext;
        private readonly IConfiguredMasterWorkflowExecutor _workflowExecutor;
        private readonly IAutoTriggerLogSink _log;

        public AutoTriggerWorkflowHandlerService(
            IAutoTriggerHomeRunContext homeRunContext,
            IConfiguredMasterWorkflowExecutor workflowExecutor,
            IAutoTriggerLogSink log)
        {
            _homeRunContext = homeRunContext;
            _workflowExecutor = workflowExecutor;
            _log = log;
        }

        public async Task ExecuteAutoTriggerWorkflowAsync(string masterWorkflowFilePath, string? sn, CancellationToken cancellationToken)
        {
            if (_homeRunContext.IsSequenceLinkageEnabled)
            {
                _log.Warn("已启用 Home 与序列联动，自动触发不执行独立脚本。");
                return;
            }

            var prepare = await _homeRunContext.TryPrepareAutoTriggerRunAsync(cancellationToken, sn).ConfigureAwait(false);
            if (!prepare.Started || prepare.LinkedCancellation == null)
            {
                return;
            }

            try
            {
                await _workflowExecutor
                    .ExecuteConfiguredMasterAtPathAsync(masterWorkflowFilePath, prepare.LinkedCancellation.Token, sn)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _log.Warn("Home 独立执行已取消。");
            }
            catch (Exception ex)
            {
                _log.Error($"Home 独立执行异常：{ex.Message}");
            }
            finally
            {
                try
                {
                    await _homeRunContext.CompleteAutoTriggerRunAsync().ConfigureAwait(false);
                }
                finally
                {
                    prepare.LinkedCancellation.Dispose();
                }
            }
        }
    }
}

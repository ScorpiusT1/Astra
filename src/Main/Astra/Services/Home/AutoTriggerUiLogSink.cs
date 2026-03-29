using Astra.Core.Triggers;
using Astra.Services.Logging;

namespace Astra.Services.Home
{
    /// <summary>
    /// 将自动触发链路日志转发到 <see cref="IUiLogService"/>。
    /// </summary>
    public sealed class AutoTriggerUiLogSink : IAutoTriggerLogSink
    {
        private readonly IUiLogService _uiLog;

        public AutoTriggerUiLogSink(IUiLogService uiLog)
        {
            _uiLog = uiLog;
        }

        public void Warn(string message) => _uiLog.Warn(message);

        public void Error(string message) => _uiLog.Error(message);
    }
}

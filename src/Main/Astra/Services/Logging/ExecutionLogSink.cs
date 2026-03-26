using Astra.Core.Logs;

namespace Astra.Services.Logging
{
    public sealed class ExecutionLogSink : IExecutionLogSink
    {
        private readonly IUiLogService _uiLogService;

        public ExecutionLogSink(IUiLogService uiLogService)
        {
            _uiLogService = uiLogService;
        }

        public void Write(string level, string message)
        {
            _uiLogService.Write(level, message);
        }
    }
}

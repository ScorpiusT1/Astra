using Astra.Core.Foundation.Common;
using Astra.Core.Triggers.Interlock;
using Astra.UI.Services;

namespace Astra.Engine.Triggers.Interlock
{
    /// <summary>
    /// 将联锁动作映射到工作流会话的暂停/恢复/停止（多路并行）。
    /// </summary>
    public sealed class TestExecutionInterlockController : ITestExecutionInterlockController
    {
        private readonly IWorkflowExecutionSessionService _sessions;

        public TestExecutionInterlockController(IWorkflowExecutionSessionService sessions)
        {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        public OperationResult PauseAllActiveTests() => _sessions.PauseAllTrackedSessions();

        public OperationResult ResumeAllPausedTests() => _sessions.ResumeAllTrackedSessions();

        public OperationResult StopAllActiveTests() => _sessions.Stop();
    }
}

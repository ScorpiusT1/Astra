using Astra.Core.Foundation.Common;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 对当前由工作流会话服务跟踪的测试执行暂停/恢复/停止（支持多路并行）。
    /// </summary>
    public interface ITestExecutionInterlockController
    {
        OperationResult PauseAllActiveTests();

        OperationResult ResumeAllPausedTests();

        OperationResult StopAllActiveTests();
    }
}

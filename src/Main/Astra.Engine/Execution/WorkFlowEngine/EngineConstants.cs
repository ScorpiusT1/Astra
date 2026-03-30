using Astra.Core.Constants;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 引擎内跨模块复用的常量定义。
    /// </summary>
    internal static class EngineConstants
    {
        internal static class MetadataKeys
        {
            public const string WorkflowExecutionController = AstraSharedConstants.MetadataKeys.WorkflowExecutionController;
            public const string ExecutionId = AstraSharedConstants.MetadataKeys.ExecutionId;
            public const string WorkFlowKey = AstraSharedConstants.MetadataKeys.WorkFlowKey;
        }

        internal static class OutputKeys
        {
            public const string SkipReason = AstraSharedConstants.WorkflowOutputKeys.SkipReason;
            public const string ExecutionStrategy = AstraSharedConstants.WorkflowOutputKeys.ExecutionStrategy;
        }

        internal static class OutputValues
        {
            public const string Disabled = AstraSharedConstants.WorkflowOutputValues.Disabled;
            public const string BlockedByUpstream = AstraSharedConstants.WorkflowOutputValues.BlockedByUpstream;
        }
    }
}

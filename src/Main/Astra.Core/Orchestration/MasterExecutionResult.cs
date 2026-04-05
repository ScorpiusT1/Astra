namespace Astra.Core.Orchestration
{
    public sealed class MasterExecutionResult
    {
        public int SuccessCount { get; init; }
        public int FailedCount { get; init; }
        public int SkippedCount { get; init; }
        public bool OverallSuccess => FailedCount == 0;
    }
}

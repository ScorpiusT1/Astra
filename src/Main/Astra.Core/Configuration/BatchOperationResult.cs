namespace Astra.Core.Configuration
{
    // ==================== 批量操作结果 ====================

    public class BatchOperationResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public Dictionary<string, string> Failures { get; set; } = new Dictionary<string, string>();
        public bool IsFullySuccessful => FailureCount == 0;
        public bool IsPartiallySuccessful => SuccessCount > 0 && FailureCount > 0;
        public int TotalCount => SuccessCount + FailureCount;
    }
}

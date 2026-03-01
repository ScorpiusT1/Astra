namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// 批量操作结果
    /// </summary>
    public class BatchOperationResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public Dictionary<string, string> Failures { get; set; } = new();
        public bool IsFullySuccessful => FailureCount == 0;
        public bool IsPartiallySuccessful => SuccessCount > 0 && FailureCount > 0;
        public int TotalCount => SuccessCount + FailureCount;
    }
}

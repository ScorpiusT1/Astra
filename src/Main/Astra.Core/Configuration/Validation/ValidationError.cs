namespace Astra.Core.Configuration.Validation
{
    /// <summary>
    /// 验证错误
    /// </summary>
    public class ValidationError
    {
        public string PropertyName { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        public override string ToString()
            => $"{PropertyName}: {ErrorMessage}" + (string.IsNullOrEmpty(ErrorCode) ? "" : $" [{ErrorCode}]");
    }
}

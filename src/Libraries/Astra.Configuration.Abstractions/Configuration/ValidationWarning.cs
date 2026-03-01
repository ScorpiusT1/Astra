namespace Astra.Core.Configuration
{
    /// <summary>
    /// 验证警告
    /// </summary>
    public class ValidationWarning
    {
        public string PropertyName { get; set; }
        public string WarningMessage { get; set; }
        public string WarningCode { get; set; }
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;

        public override string ToString()
        {
            return $"{PropertyName}: {WarningMessage}" +
                   (string.IsNullOrEmpty(WarningCode) ? "" : $" [{WarningCode}]");
        }
    }
}


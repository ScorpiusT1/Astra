namespace Astra.Core.Configuration.Validation
{
    /// <summary>
    /// 验证警告
    /// </summary>
    public class ValidationWarning
    {
        public string PropertyName { get; set; }
        public string WarningMessage { get; set; }

        public override string ToString() => $"{PropertyName}: {WarningMessage}";
    }
}

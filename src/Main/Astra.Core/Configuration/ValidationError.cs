namespace Astra.Core.Configuration
{
    /// <summary>
    /// 验证错误
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 错误代码
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 严重程度
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        public override string ToString()
        {
            return $"{PropertyName}: {ErrorMessage}" +
                   (string.IsNullOrEmpty(ErrorCode) ? "" : $" [{ErrorCode}]");
        }
    }
}

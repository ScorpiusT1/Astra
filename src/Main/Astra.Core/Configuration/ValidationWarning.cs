namespace Astra.Core.Configuration
{
    /// <summary>
    /// 验证警告
    /// </summary>
    public class ValidationWarning
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 警告消息
        /// </summary>
        public string WarningMessage { get; set; }

        public override string ToString()
        {
            return $"{PropertyName}: {WarningMessage}";
        }
    }
}

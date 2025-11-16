namespace Astra.Core.Plugins.Validation
{
    /// <summary>
    /// 验证结果：包含是否通过与错误列表。
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否通过全部校验。
        /// </summary>
        public bool IsValid { get; set; }
        /// <summary>
        /// 失败时的错误信息集合。
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
}

namespace Astra.Core.Foundation.Common
{
    /// <summary>
    /// 通用验证结果，位于 Foundation 层供全局复用。
    ///
    /// 与 <see cref="Astra.Core.Configuration.ValidationResult"/> 的区别：
    ///   本类是轻量级的布尔+字符串错误列表，适用于节点/工作流验证；
    ///   Configuration.ValidationResult 是重量级的配置专用验证结果（含错误对象、错误码、链式 API 等）。
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failure(params string[] errors)
            => new() { IsValid = false, Errors = new List<string>(errors) };
    }
}

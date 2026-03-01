namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// 配置导出选项。
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// 是否以缩进格式输出 JSON（默认 true，便于阅读和版本控制对比）。
        /// </summary>
        public bool PrettyPrint { get; set; } = true;

        /// <summary>
        /// 仅导出指定类型的配置。
        /// 为 null 时导出所有已注册类型的配置。
        /// </summary>
        public IReadOnlyList<Type> TypeFilter { get; set; }
    }
}

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 报告分节标题：直接展示工况/流程名称，不带「工况」等前缀。
    /// </summary>
    public static class ReportConditionDisplay
    {
        /// <summary>
        /// 分节主标题用文本；空或空白时返回长横线占位。
        /// </summary>
        public static string FormatSectionTitle(string? condition) =>
            string.IsNullOrWhiteSpace(condition) ? "—" : condition.Trim();
    }
}

using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 配置导入/导出服务接口。
    ///
    /// 使用模式：
    ///   - 导出：调用 <see cref="ExportAsync(string, ExportOptions)"/> 将所有（或指定类型的）
    ///     配置序列化为 JSON 文件，供备份或迁移使用。
    ///   - 导入：调用 <see cref="ImportAsync(string, ImportOptions)"/> 从文件恢复配置，
    ///     通过 <see cref="ImportOptions.ConflictResolution"/> 控制冲突处理策略。
    ///
    /// 文件格式说明：
    ///   导出文件为 UTF-8 编码的 JSON，包含版本、导出时间及配置条目列表。
    ///   每个条目记录配置类型全名和原始数据，导入时按类型全名匹配已注册的 Provider。
    /// </summary>
    public interface IConfigurationImportExportService
    {
        // ── 导出 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 将所有已注册配置导出到指定文件。
        /// </summary>
        /// <param name="filePath">目标文件路径（不存在时自动创建）</param>
        /// <param name="options">导出选项（null 使用默认值）</param>
        Task<OperationResult> ExportAsync(string filePath, ExportOptions options = null);

        /// <summary>
        /// 仅导出指定类型 <typeparamref name="T"/> 的配置到指定文件。
        /// </summary>
        Task<OperationResult> ExportAsync<T>(string filePath, ExportOptions options = null)
            where T : class, IConfig;

        /// <summary>
        /// 将所有已注册配置导出为 JSON 字符串（适合预览、剪贴板等场景）。
        /// </summary>
        Task<OperationResult<string>> ExportToStringAsync(ExportOptions options = null);

        /// <summary>
        /// 将指定配置列表导出到文件（供 UI 按根节点导出当前类别使用）。
        /// </summary>
        Task<OperationResult> ExportConfigsAsync(IEnumerable<IConfig> configs, string filePath, ExportOptions options = null);

        // ── 导入 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 从指定文件导入配置。
        /// </summary>
        /// <param name="filePath">源文件路径</param>
        /// <param name="options">导入选项（null 使用默认值）</param>
        /// <returns>导入结果，包含成功/跳过/失败统计</returns>
        Task<ImportResult> ImportAsync(string filePath, ImportOptions options = null);

        /// <summary>
        /// 从流导入配置（适合 UI 不落地读取，如直接传入文件选择对话框的流）。
        /// </summary>
        Task<ImportResult> ImportAsync(Stream stream, ImportOptions options = null);
    }
}

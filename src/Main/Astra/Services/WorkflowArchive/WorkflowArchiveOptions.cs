namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 归档根目录等选项。主程序下通常由 <see cref="Astra.Configuration.SoftwareConfig.ReportOutputRootDirectory"/> 覆盖；
    /// 此处仅在无法注入配置管理器时作为回退根目录。
    /// </summary>
    public sealed class WorkflowArchiveOptions
    {
        /// <summary>
        /// 回退用归档根目录；为空时使用程序所在磁盘根目录。其下仍按「测试数据\年-月-日\SN\序号」组织。
        /// </summary>
        public string RootDirectory { get; set; } = string.Empty;
    }
}

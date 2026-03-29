namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 归档根目录等选项（可后续接入配置系统）。
    /// </summary>
    public sealed class WorkflowArchiveOptions
    {
        /// <summary>
        /// 测试归档根目录，默认在程序目录下 TestResults。
        /// </summary>
        public string RootDirectory { get; set; } =
            System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "TestResults");
    }
}

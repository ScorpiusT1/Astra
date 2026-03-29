namespace Astra.Core.Triggers
{
    /// <summary>
    /// 按给定主流程文件路径执行（与「当前保存的脚本」解耦），供自动触发等场景使用。
    /// </summary>
    public interface IConfiguredMasterWorkflowExecutor
    {
        Task ExecuteConfiguredMasterAtPathAsync(
            string masterWorkflowFilePath,
            CancellationToken cancellationToken,
            string? snForGlobalVariable = null);
    }
}

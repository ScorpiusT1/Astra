namespace Astra.Core.Logs
{
    /// <summary>
    /// 由宿主创建「单次执行」日志会话（含文件路径、UI 转发等）。
    /// </summary>
    public interface IExecutionRunLogSessionFactory
    {
        /// <param name="executionId">本轮执行 ID。</param>
        /// <param name="serialNumber">若启动时尚无 SN 可传 null，后续可 <see cref="IExecutionRunLogSession.TryRenameFileWithSerialNumber"/>。</param>
        /// <param name="preferredLogDirectory">非空时日志文件写入该目录（通常与测试数据归档目录一致）；为空时使用工厂默认目录。</param>
        /// <param name="workFlowKey">写入日志头/分段注释用（如合并批次内区分子流程）。</param>
        IExecutionRunLogSession Create(string executionId, string? serialNumber, string? preferredLogDirectory = null, string? workFlowKey = null);
    }
}

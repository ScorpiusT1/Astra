namespace Astra.Core.Logs
{
    /// <summary>文件型运行日志输出端可选实现：在获得 SN 后重命名日志文件。</summary>
    public interface IRunLogFileRenameSink
    {
        void TryRenameWithSerialNumber(string serialNumber);
    }
}

namespace Astra.Core.Logs
{
    /// <summary>
    /// 单次执行日志的落盘/UI 输出端：流程级立即行与节点成块内容。
    /// </summary>
    public interface IExecutionRunLogChunkSink
    {
        /// <summary>流程边界、文件头等立即写入（已含换行）。</summary>
        void WriteImmediate(string text);

        /// <summary>一个节点完整日志块（可含多行，建议末尾换行）。</summary>
        void WriteNodeBlock(string text);

        /// <summary>释放底层文件等资源。</summary>
        void Dispose();
    }
}

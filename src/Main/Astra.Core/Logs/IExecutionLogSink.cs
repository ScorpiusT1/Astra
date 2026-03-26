namespace Astra.Core.Logs
{
    /// <summary>
    /// 执行层日志桥接：把流程运行日志转发到上层日志系统。
    /// </summary>
    public interface IExecutionLogSink
    {
        void Write(string level, string message);
    }
}

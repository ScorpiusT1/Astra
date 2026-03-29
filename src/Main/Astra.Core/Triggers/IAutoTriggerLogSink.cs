namespace Astra.Core.Triggers
{
    /// <summary>
    /// 自动触发链路中的用户可见日志（Engine 使用；宿主可转发到 <c>IUiLogService</c> 等）。
    /// </summary>
    public interface IAutoTriggerLogSink
    {
        void Warn(string message);

        void Error(string message);
    }
}

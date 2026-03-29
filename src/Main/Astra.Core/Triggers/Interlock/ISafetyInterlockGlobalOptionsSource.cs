using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 从软件配置读取安全联锁总开关与轮询周期（宿主实现）。
    /// </summary>
    public interface ISafetyInterlockGlobalOptionsSource
    {
        ValueTask<(bool Enabled, int PollIntervalMs)> GetOptionsAsync(CancellationToken cancellationToken = default);
    }
}

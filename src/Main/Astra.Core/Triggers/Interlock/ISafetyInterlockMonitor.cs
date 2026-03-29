using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 安全联锁后台监控：轮询配置中的 IO 并调用 <see cref="ITestExecutionInterlockController"/>。
    /// </summary>
    public interface ISafetyInterlockMonitor
    {
        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}

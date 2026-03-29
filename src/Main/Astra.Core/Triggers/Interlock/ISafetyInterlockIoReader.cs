using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 读取联锁监控所需的 IO 值（由 PLC 等插件实现）。
    /// </summary>
    public interface ISafetyInterlockIoReader
    {
        /// <summary>
        /// 读取 BOOL 点位；成功时 <see cref="OperationResult{T}.Data"/> 为当前值，失败时含原因说明。
        /// </summary>
        Task<OperationResult<bool>> ReadBoolAsync(
            string plcDeviceName,
            string ioPointName,
            CancellationToken cancellationToken);
    }
}

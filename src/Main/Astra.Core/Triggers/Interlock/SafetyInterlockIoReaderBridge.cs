using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 安全联锁 IO 读取器的宿主桥接：在 BuildServiceProvider 之前注册到容器，
    /// 在插件 InitializeAsync 中再设置具体实现（启动顺序：注册服务 → 构建 ServiceProvider → 加载并初始化插件）。
    /// </summary>
    public sealed class SafetyInterlockIoReaderBridge : ISafetyInterlockIoReader
    {
        private readonly object _gate = new();
        private ISafetyInterlockIoReader? _inner;

        /// <summary>
        /// 由 PLC 等插件在初始化完成后设置；卸载时可置为 null。
        /// </summary>
        public void SetImplementation(ISafetyInterlockIoReader? implementation)
        {
            lock (_gate)
            {
                _inner = implementation;
            }
        }

        public Task<OperationResult<bool>> ReadBoolAsync(
            string plcDeviceName,
            string ioPointName,
            CancellationToken cancellationToken)
        {
            ISafetyInterlockIoReader? inner;
            lock (_gate)
            {
                inner = _inner;
            }

            if (inner == null)
            {
                return Task.FromResult(OperationResult<bool>.Failure("联锁 IO 读取器尚未由插件注册"));
            }

            return inner.ReadBoolAsync(plcDeviceName, ioPointName, cancellationToken);
        }
    }
}

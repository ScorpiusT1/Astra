using Astra.Communication.Abstractions;
using Astra.Core.Devices.Base;
using Astra.Core.Foundation.Common;
using Astra.Plugins.PLC.Configs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Devices
{
    public abstract class PlcDeviceBase : DeviceBase<PlcDeviceConfig>, IPLC
    {
        public PlcDeviceBase(DeviceConnectionBase connection, PlcDeviceConfig config) : base(connection, config)
        {

        }

        public abstract string Protocol { get; }

        public abstract OperationResult<T> Read<T>(string address);

        public abstract Task<OperationResult<T>> ReadAsync<T>(string address, CancellationToken cancellationToken = default);

        public abstract OperationResult Write<T>(string address, T value);

        public abstract Task<OperationResult> WriteAsync<T>(string address, T value, CancellationToken cancellationToken = default);

        public abstract OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, string> addressMap);

        public abstract Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(
            Dictionary<string, string> addressMap,
            CancellationToken cancellationToken = default);

        public abstract OperationResult BatchWrite(Dictionary<string, object> values);

        public abstract Task<OperationResult> BatchWriteAsync(
            Dictionary<string, object> values,
            CancellationToken cancellationToken = default);
    }
}

using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Triggers;
using Astra.Core.Triggers.Enums;
using Astra.Core.Triggers.Models;

namespace Astra.Plugins.PLC.Triggers
{
    /// <summary>
    /// PLC 监控触发器：轮询 IO 配置中的地址，检测 bool 上升沿后触发。
    /// </summary>
    public class PlcTrigger : TriggerBase
    {
        private readonly IPLC _plc;
        private readonly string _monitorAddress;
        private readonly string? _ioPointName;
        private bool _lastState;

        public override string TriggerName =>
            string.IsNullOrWhiteSpace(_ioPointName)
                ? $"PLC触发器-{_monitorAddress}"
                : $"PLC触发器-{_ioPointName}";

        protected override TriggerWorkType WorkType => TriggerWorkType.Polling;

        protected override int PollIntervalMs => 50;

        public PlcTrigger(IPLC plc, string monitorAddress, string? ioPointName = null)
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _monitorAddress = monitorAddress ?? throw new ArgumentNullException(nameof(monitorAddress));
            _ioPointName = ioPointName;
            _lastState = false;
        }

        protected override async Task<bool> OnBeforeStartAsync()
        {
            if (_plc is IDevice device && !device.IsOnline)
            {
                var r = await device.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
                return r.Success;
            }

            return true;
        }

        protected override Task OnBeforeStopAsync()
        {
            // 不主动断开 PLC，避免与其它节点/界面共用同一连接时互相干扰
            return Task.CompletedTask;
        }

        protected override async Task<TriggerResult> CheckTriggerAsync()
        {
            try
            {
                OperationResult<bool> read = await _plc
                    .ReadAsync<bool>(_monitorAddress, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!read.Success)
                {
                    return TriggerResult.NotTriggered();
                }

                bool currentState = read.Data;
                if (currentState && !_lastState)
                {
                    _lastState = currentState;
                    var data = new Dictionary<string, object>
                    {
                        { "PLCAddress", _monitorAddress },
                        { "IoPointName", _ioPointName ?? string.Empty },
                        { "TriggerEdge", "Rising" }
                    };
                    var sn = string.IsNullOrWhiteSpace(_ioPointName) ? _monitorAddress : _ioPointName!;
                    return TriggerResult.TriggeredWithSN("PLCMonitor", sn, data);
                }

                _lastState = currentState;
                return TriggerResult.NotTriggered();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{TriggerName}] PLC 读取异常: {ex.Message}");
                return TriggerResult.NotTriggered();
            }
        }
    }
}

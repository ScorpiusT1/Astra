using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Providers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Nodes
{
    public class WriteIoNode : Node
    {
        /// <summary>供属性编辑器绑定：列出当前所选 PLC 下已启用的配置 IO 名称。</summary>
        public IEnumerable<string> ConfiguredIoNameOptions => PlcIoProvider.GetIoNamesForPlcDevice(PlcDeviceName);

        [Display(Name = "PLC设备名称", GroupName = "PLC配置", Order = 1)]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public string PlcDeviceName { get; set; } = string.Empty;

        [Display(Name = "IO名称", GroupName = "写入配置", Order = 2, Description = "多选；须为 IO 配置中已启用且绑定当前 PLC 的点位；单选为单点写入，多选时对每个点位写入相同的「写入值」")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ConfiguredIoNameOptions), DisplayMemberPath = ".")]
        public List<string> IoNames { get; set; } = new();

        [Display(Name = "写入值", GroupName = "写入配置", Order = 3, Description = "支持 bool/int/double/string，自动解析；多 IO 时写入同一值到各地址")]
        public string ValueText { get; set; } = string.Empty;

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"PLC写入节点:{Name}");
            try
            {
                var selectedName = PlcDeviceName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(selectedName))
                {
                    return ExecutionResult.Failed("请选择一个 PLC 设备");
                }

                var plc = ResolvePlcByName(selectedName);
                if (plc == null)
                {
                    return ExecutionResult.Failed($"未找到已选择 PLC 设备: {selectedName}");
                }

                var nameList = NormalizeIoNameList(IoNames);
                if (nameList.Count == 0)
                {
                    return ExecutionResult.Failed("请至少选择一个 IO（须在 IO 配置中维护并启用）");
                }

                var value = ParseValue(ValueText);

                if (nameList.Count == 1)
                {
                    var ioName = nameList[0];
                    var io = PlcIoProvider.FindByName(ioName);
                    if (io == null)
                    {
                        return ExecutionResult.Failed("未找到该 IO 点位配置（请检查名称是否存在于 IO 配置且已启用）");
                    }

                    var plcMismatch = ValidateIoPlcBinding(io, selectedName, out _);
                    if (plcMismatch != null)
                    {
                        return plcMismatch;
                    }

                    var address = io.Address?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        return ExecutionResult.Failed("该 IO 在配置中地址为空");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var connectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                    if (!connectResult.Success)
                    {
                        return ExecutionResult.Failed(connectResult.Message ?? "PLC连接失败");
                    }

                    var writeResult = await plc.WriteAsync(address, value, cancellationToken).ConfigureAwait(false);
                    if (!writeResult.Success)
                    {
                        return ExecutionResult.Failed(writeResult.ErrorMessage ?? $"写入失败: {address}");
                    }

                    log.Info($"写入完成，PLC={selectedName}, 地址={address}");
                    return ExecutionResult.Successful("PLC 单点写入完成")
                        .WithOutput("PlcDeviceName", selectedName)
                        .WithOutput("PlcDeviceNames", new List<string> { selectedName })
                        .WithOutput("Address", address)
                        .WithOutput("WrittenValue", value);
                }

                if (!TryBuildMultiWriteMap(nameList, selectedName, value, out var writeMap, out var buildError))
                {
                    return ExecutionResult.Failed(buildError ?? "多 IO 写入配置无效");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var batchConnectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                if (!batchConnectResult.Success)
                {
                    return ExecutionResult.Failed(batchConnectResult.Message ?? "PLC连接失败");
                }

                var batchResult = await plc.BatchWriteAsync(writeMap, cancellationToken).ConfigureAwait(false);
                if (!batchResult.Success)
                {
                    return ExecutionResult.Failed(batchResult.ErrorMessage ?? "多地址写入失败");
                }

                log.Info($"多 IO 写入完成，PLC={selectedName}, 项数={writeMap.Count}");
                return ExecutionResult.Successful("PLC 多 IO 写入完成")
                    .WithOutput("PlcDeviceName", selectedName)
                    .WithOutput("PlcDeviceNames", new List<string> { selectedName })
                    .WithOutput("WrittenValues", writeMap);
            }
            catch (OperationCanceledException)
            {
                return ExecutionResult.Cancel("PLC 写入已取消");
            }
            catch (Exception ex)
            {
                log.Error($"写入异常: {ex.Message}");
                return ExecutionResult.Failed("PLC 写入异常", ex);
            }
        }

        private static List<string> NormalizeIoNameList(IEnumerable<string>? names)
        {
            if (names == null)
            {
                return new List<string>();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var raw in names)
            {
                var n = raw?.Trim() ?? string.Empty;
                if (n.Length == 0 || !seen.Add(n))
                {
                    continue;
                }

                list.Add(n);
            }

            return list;
        }

        private static bool TryBuildMultiWriteMap(
            IReadOnlyList<string> ioNames,
            string selectedPlcName,
            object value,
            out Dictionary<string, object> writeMap,
            out string? error)
        {
            writeMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            error = null;

            foreach (var ioName in ioNames)
            {
                var io = PlcIoProvider.FindByName(ioName);
                if (io == null)
                {
                    error = $"未找到 IO「{ioName}」（须在 IO 配置中存在且已启用）";
                    return false;
                }

                if (ValidateIoPlcBinding(io, selectedPlcName, out var plcMsg) != null)
                {
                    error = plcMsg;
                    return false;
                }

                var address = io.Address?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(address))
                {
                    error = $"IO「{ioName}」在配置中地址为空";
                    return false;
                }

                if (writeMap.ContainsKey(address))
                {
                    error = $"多条 IO 映射到同一 PLC 地址「{address}」，请检查配置";
                    return false;
                }

                writeMap[address] = value;
            }

            return true;
        }

        private static ExecutionResult? ValidateIoPlcBinding(IoPointModel io, string selectedPlcName, out string error)
        {
            if (!string.IsNullOrWhiteSpace(io.PlcDeviceName) &&
                !string.Equals(io.PlcDeviceName.Trim(), selectedPlcName, StringComparison.OrdinalIgnoreCase))
            {
                error = $"IO「{io.Name}」绑定的 PLC 为 {io.PlcDeviceName}，与当前选择 {selectedPlcName} 不一致";
                return ExecutionResult.Failed(error);
            }

            error = string.Empty;
            return null;
        }

        private static IPLC ResolvePlcByName(string plcDeviceName)
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null)
            {
                return null;
            }

            var all = plugin.GetAllPlcs();
            var name = plcDeviceName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return all.FirstOrDefault(p =>
            {
                if (p is not IDevice d)
                {
                    return false;
                }

                return string.Equals(d.DeviceName, name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static async Task<OperationResult> EnsureConnectedAsync(IPLC plc, CancellationToken cancellationToken)
        {
            if (plc is not IDevice device)
            {
                return OperationResult.Failure("PLC 实例不支持 IDevice 接口");
            }

            if (device.IsOnline)
            {
                return OperationResult.Succeed("PLC 已在线");
            }

            return await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        private static object ParseValue(string valueText)
        {
            var text = valueText?.Trim() ?? string.Empty;

            if (bool.TryParse(text, out var b))
            {
                return b;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }

            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
            {
                return d;
            }

            if ((text.StartsWith("\"") && text.EndsWith("\"")) && text.Length >= 2)
            {
                return text[1..^1];
            }

            return text;
        }
    }
}

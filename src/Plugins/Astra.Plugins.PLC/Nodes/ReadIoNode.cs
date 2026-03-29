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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Nodes
{
    public class ReadIoNode : Node
    {
        /// <summary>供属性编辑器绑定：列出当前所选 PLC 下已启用的配置 IO 名称。</summary>
        public IEnumerable<string> ConfiguredIoNameOptions => PlcIoProvider.GetIoNamesForPlcDevice(PlcDeviceName);

        [Display(Name = "PLC设备名称", GroupName = "PLC配置", Order = 1)]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public string PlcDeviceName { get; set; } = string.Empty;

        [Display(Name = "IO名称", GroupName = "读取配置", Order = 2, Description = "多选；须为 IO 配置中已启用且绑定当前 PLC 的点位；单选为单点读取，多选为批量读取；输出键与缩放取自各 IO 配置")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ConfiguredIoNameOptions), DisplayMemberPath = ".")]
        public List<string> IoNames { get; set; } = new();

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"PLC读取节点:{Name}");
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

                if (nameList.Count == 1)
                {
                    var ioName = nameList[0];
                    var io = PlcIoProvider.FindByName(ioName);
                    if (io == null)
                    {
                        return ExecutionResult.Failed("未找到该 IO 点位配置（请检查名称是否存在于 IO 配置且已启用）");
                    }

                    var plcMismatch = ValidateIoPlcBinding(io, selectedName);
                    if (plcMismatch != null)
                    {
                        return plcMismatch;
                    }

                    var address = io.Address?.Trim() ?? string.Empty;
                    var key = ResolveOutputKey(io);
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

                    var readResult = await plc.ReadAsync<object>(address, cancellationToken).ConfigureAwait(false);
                    if (!readResult.Success)
                    {
                        return ExecutionResult.Failed(readResult.ErrorMessage ?? $"读取失败: {address}");
                    }

                    var value = readResult.Data!;
                    if (io.TryApplyScaleOffset(value, out var scaled))
                    {
                        value = scaled!;
                    }

                    context.SetGlobalVariable(key, value);

                    log.Info($"读取完成，PLC={selectedName}, 地址={address}");
                    return ExecutionResult.Successful("PLC 单点读取完成")
                        .WithOutput("PlcDeviceName", selectedName)
                        .WithOutput("PlcDeviceNames", new List<string> { selectedName })
                        .WithOutput("Address", address)
                        .WithOutput("ReadValue", value)
                        .WithOutput("OutputKey", key);
                }

                if (!TryBuildMultiReadMaps(nameList, selectedName, out var addressMap, out var ioByKey, out var buildError))
                {
                    return ExecutionResult.Failed(buildError ?? "多 IO 读取配置无效");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var multiConnectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                if (!multiConnectResult.Success)
                {
                    return ExecutionResult.Failed(multiConnectResult.Message ?? "PLC连接失败");
                }

                var batchResult = await plc.BatchReadAsync(addressMap, cancellationToken).ConfigureAwait(false);
                if (!batchResult.Success)
                {
                    return ExecutionResult.Failed(batchResult.ErrorMessage ?? "多地址读取失败");
                }

                var values = batchResult.Data ?? new Dictionary<string, object>();
                foreach (var kv in values)
                {
                    var val = kv.Value;
                    if (ioByKey.TryGetValue(kv.Key, out var io) && io.TryApplyScaleOffset(val, out var scaled))
                    {
                        val = scaled!;
                    }

                    context.SetGlobalVariable(kv.Key, val);
                }

                log.Info($"多 IO 读取完成，PLC={selectedName}, 项数={values.Count}");
                return ExecutionResult.Successful("PLC 多 IO 读取完成")
                    .WithOutput("PlcDeviceName", selectedName)
                    .WithOutput("PlcDeviceNames", new List<string> { selectedName })
                    .WithOutput("ReadValues", values);
            }
            catch (OperationCanceledException)
            {
                return ExecutionResult.Cancel("PLC 读取已取消");
            }
            catch (Exception ex)
            {
                log.Error($"读取异常: {ex.Message}");
                return ExecutionResult.Failed("PLC 读取异常", ex);
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

        private static string ResolveOutputKey(IoPointModel io)
        {
            return string.IsNullOrWhiteSpace(io.OutputKey) ? (io.Name?.Trim() ?? "Value") : io.OutputKey.Trim();
        }

        private static ExecutionResult? ValidateIoPlcBinding(IoPointModel io, string selectedPlcName)
        {
            if (!string.IsNullOrWhiteSpace(io.PlcDeviceName) &&
                !string.Equals(io.PlcDeviceName.Trim(), selectedPlcName, StringComparison.OrdinalIgnoreCase))
            {
                return ExecutionResult.Failed($"IO 绑定的 PLC 为 {io.PlcDeviceName}，与当前选择 {selectedPlcName} 不一致");
            }

            return null;
        }

        private static bool TryBuildMultiReadMaps(
            IReadOnlyList<string> ioNames,
            string selectedPlcName,
            out Dictionary<string, string> addressMap,
            out Dictionary<string, IoPointModel> ioByKey,
            out string? error)
        {
            addressMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ioByKey = new Dictionary<string, IoPointModel>(StringComparer.OrdinalIgnoreCase);
            error = null;

            foreach (var rawName in ioNames)
            {
                var io = PlcIoProvider.FindByName(rawName);
                if (io == null)
                {
                    error = $"未找到 IO「{rawName}」（须在 IO 配置中存在且已启用）";
                    return false;
                }

                var plcErr = ValidateIoPlcBinding(io, selectedPlcName);
                if (plcErr != null)
                {
                    error = plcErr.Message ?? $"IO「{rawName}」与当前 PLC 不匹配";
                    return false;
                }

                var addr = io.Address?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(addr))
                {
                    error = $"IO「{rawName}」在配置中地址为空";
                    return false;
                }

                var key = ResolveOutputKey(io);
                if (addressMap.ContainsKey(key))
                {
                    error = $"多条 IO 映射到同一输出键「{key}」，请调整各 IO 的「输出键(读)」";
                    return false;
                }

                addressMap[key] = addr;
                ioByKey[key] = io;
            }

            return true;
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
    }
}

using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
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
    public class ReadNode : Node
    {
        [Display(Name = "PLC设备名称", GroupName = "PLC配置", Order = 1)]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public string PlcDeviceName { get; set; } = string.Empty;

        [Display(Name = "使用IO配置库", GroupName = "读取配置", Order = 2, Description = "true=按IO名称选择并自动取地址/输出键；false=手工填写地址")]
        public bool UseIoConfig { get; set; }

        [Display(Name = "IO名称", GroupName = "读取配置", Order = 3, Description = "从 IO 配置库选择 IO 名称")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcIoProvider), "GetIoNames", DisplayMemberPath = ".")]
        public string IoName { get; set; } = string.Empty;

        [Display(Name = "启用多地址读取", GroupName = "读取配置", Order = 2, Description = "false=单地址读取，true=多地址读取")]
        public bool UseMultiAddress { get; set; }

        [Display(Name = "单地址", GroupName = "读取配置", Order = 3, Description = "示例: DB1.DBW0")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "单地址输出键", GroupName = "读取配置", Order = 4, Description = "为空时默认使用 Value")]
        public string OutputKey { get; set; } = "Value";

        [Display(Name = "多地址映射", GroupName = "读取配置", Order = 5, Description = "每行一项: 键=地址 或 键:地址；仅地址时键与地址相同")]
        public string MultiAddressText { get; set; } = string.Empty;

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

                if (!UseMultiAddress)
                {
                    var address = Address?.Trim() ?? string.Empty;
                    var key = string.IsNullOrWhiteSpace(OutputKey) ? "Value" : OutputKey.Trim();

                    if (UseIoConfig)
                    {
                        var io = PlcIoProvider.FindByName(IoName);
                        if (io == null)
                        {
                            return ExecutionResult.Failed("未找到已选择 IO 点位配置（请检查 IO名称 或点位是否启用）");
                        }

                        if (!string.IsNullOrWhiteSpace(io.PlcDeviceName) &&
                            !string.Equals(io.PlcDeviceName.Trim(), selectedName, StringComparison.OrdinalIgnoreCase))
                        {
                            return ExecutionResult.Failed($"IO点位绑定的PLC设备为 {io.PlcDeviceName}，与当前选择 {selectedName} 不一致");
                        }

                        address = io.Address?.Trim() ?? string.Empty;
                        key = string.IsNullOrWhiteSpace(io.OutputKey) ? (io.Name?.Trim() ?? key) : io.OutputKey.Trim();
                    }

                    if (string.IsNullOrWhiteSpace(address))
                    {
                        return ExecutionResult.Failed("单地址读取模式下 Address 不能为空");
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
                    if (UseIoConfig)
                    {
                        var io = PlcIoProvider.FindByName(IoName);
                        if (io != null && io.TryApplyScaleOffset(value, out var scaled))
                        {
                            value = scaled!;
                        }
                    }
                    context.SetGlobalVariable(key, value);

                    log.Info($"读取完成，PLC={selectedName}, 地址={address}");
                    var result = ExecutionResult.Successful("PLC 单地址读取完成")
                        .WithOutput("PlcDeviceName", selectedName)
                        .WithOutput("PlcDeviceNames", new List<string> { selectedName })
                        .WithOutput("Address", address)
                        .WithOutput("ReadValue", value)
                        .WithOutput("OutputKey", key);
                    return result;
                }

                var addressMap = ParseAddressMap(MultiAddressText);
                if (addressMap.Count == 0)
                {
                    return ExecutionResult.Failed("多地址读取模式下 MultiAddressText 不能为空");
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
                    context.SetGlobalVariable(kv.Key, kv.Value);
                }

                log.Info($"多地址读取完成，PLC={selectedName}, 项数={values.Count}");
                return ExecutionResult.Successful("PLC 多地址读取完成")
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

        private static Dictionary<string, string> ParseAddressMap(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var lines = text.Replace(";", Environment.NewLine)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                var idx = line.IndexOf('=');
                if (idx < 0)
                {
                    idx = line.IndexOf(':');
                }

                if (idx <= 0 || idx >= line.Length - 1)
                {
                    result[line] = line;
                    continue;
                }

                var key = line[..idx].Trim();
                var address = line[(idx + 1)..].Trim();
                if (key.Length > 0 && address.Length > 0)
                {
                    result[key] = address;
                }
            }

            return result;
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

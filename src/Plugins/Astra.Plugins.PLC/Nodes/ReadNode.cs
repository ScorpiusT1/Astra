using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using Astra.Plugins.PLC.Providers;
using Astra.UI.Abstractions.Attributes;
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
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public string PlcDeviceName { get; set; } = string.Empty;

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
                var plc = ResolvePlcByName(PlcDeviceName);
                if (plc == null)
                {
                    return ExecutionResult.Failed($"未找到 PLC 设备: {PlcDeviceName}");
                }

                var connectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                if (!connectResult.Success)
                {
                    return ExecutionResult.Failed(connectResult.Message ?? "PLC 连接失败");
                }

                if (!UseMultiAddress)
                {
                    if (string.IsNullOrWhiteSpace(Address))
                    {
                        return ExecutionResult.Failed("单地址读取模式下 Address 不能为空");
                    }

                    var readResult = await plc.ReadAsync<object>(Address.Trim(), cancellationToken).ConfigureAwait(false);
                    if (!readResult.Success)
                    {
                        return ExecutionResult.Failed(readResult.ErrorMessage ?? $"读取失败: {Address}");
                    }

                    var key = string.IsNullOrWhiteSpace(OutputKey) ? "Value" : OutputKey.Trim();
                    var value = readResult.Data;
                    context.SetGlobalVariable(key, value);

                    log.Info($"读取成功，设备={PlcDeviceName}, 地址={Address}, 输出键={key}");
                    return ExecutionResult.Successful("PLC 单地址读取成功")
                        .WithOutput("PlcDeviceName", PlcDeviceName)
                        .WithOutput("Address", Address.Trim())
                        .WithOutput(key, value!);
                }

                var addressMap = ParseAddressMap(MultiAddressText);
                if (addressMap.Count == 0)
                {
                    return ExecutionResult.Failed("多地址读取模式下 MultiAddressText 不能为空");
                }

                var batchResult = await plc.BatchReadAsync(addressMap, cancellationToken).ConfigureAwait(false);
                if (!batchResult.Success)
                {
                    return ExecutionResult.Failed(batchResult.ErrorMessage ?? "PLC 多地址读取失败");
                }

                var values = batchResult.Data ?? new Dictionary<string, object>();
                foreach (var kv in values)
                {
                    context.SetGlobalVariable(kv.Key, kv.Value);
                }

                log.Info($"多地址读取完成，设备={PlcDeviceName}, 成功项={values.Count}");
                return ExecutionResult.Successful("PLC 多地址读取成功")
                    .WithOutput("PlcDeviceName", PlcDeviceName)
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

        private static IPLC? ResolvePlcByName(string plcDeviceName)
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null || string.IsNullOrWhiteSpace(plcDeviceName))
            {
                return null;
            }

            return plugin.GetAllPlcs()
                .FirstOrDefault(p =>
                {
                    if (p is not IDevice d)
                    {
                        return false;
                    }

                    return string.Equals(d.DeviceName, plcDeviceName, StringComparison.OrdinalIgnoreCase);
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

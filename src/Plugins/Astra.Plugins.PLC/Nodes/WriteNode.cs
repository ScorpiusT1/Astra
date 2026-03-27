using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using Astra.Plugins.PLC.Providers;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Nodes
{
    public class WriteNode : Node
    {
        [Display(Name = "PLC设备名称", GroupName = "PLC配置", Order = 1)]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public string PlcDeviceName { get; set; } = string.Empty;

        [Display(Name = "启用多地址写入", GroupName = "写入配置", Order = 2, Description = "false=单地址写入，true=多地址写入")]
        public bool UseMultiAddress { get; set; }

        [Display(Name = "单地址", GroupName = "写入配置", Order = 3, Description = "示例: DB1.DBW0")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "单地址值", GroupName = "写入配置", Order = 4, Description = "支持 bool/int/double/string，自动解析")]
        public string ValueText { get; set; } = string.Empty;

        [Display(Name = "多地址写入", GroupName = "写入配置", Order = 5, Description = "每行一项: 地址=值；支持 # 注释")]
        public string MultiWriteText { get; set; } = string.Empty;

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"PLC写入节点:{Name}");
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
                        return ExecutionResult.Failed("单地址写入模式下 Address 不能为空");
                    }

                    var value = ParseValue(ValueText);
                    var writeResult = await plc.WriteAsync(Address.Trim(), value, cancellationToken).ConfigureAwait(false);
                    if (!writeResult.Success)
                    {
                        return ExecutionResult.Failed(writeResult.ErrorMessage ?? $"写入失败: {Address}");
                    }

                    log.Info($"写入成功，设备={PlcDeviceName}, 地址={Address}");
                    return ExecutionResult.Successful("PLC 单地址写入成功")
                        .WithOutput("PlcDeviceName", PlcDeviceName)
                        .WithOutput("Address", Address.Trim())
                        .WithOutput("WrittenValue", value);
                }

                var writeMap = ParseWriteMap(MultiWriteText);
                if (writeMap.Count == 0)
                {
                    return ExecutionResult.Failed("多地址写入模式下 MultiWriteText 不能为空");
                }

                var batchResult = await plc.BatchWriteAsync(writeMap, cancellationToken).ConfigureAwait(false);
                if (!batchResult.Success)
                {
                    return ExecutionResult.Failed(batchResult.ErrorMessage ?? "PLC 多地址写入失败");
                }

                log.Info($"多地址写入完成，设备={PlcDeviceName}, 项数={writeMap.Count}");
                return ExecutionResult.Successful("PLC 多地址写入成功")
                    .WithOutput("PlcDeviceName", PlcDeviceName)
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

        private static Dictionary<string, object> ParseWriteMap(string text)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
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
                if (idx <= 0 || idx >= line.Length - 1)
                {
                    continue;
                }

                var address = line[..idx].Trim();
                var valueText = line[(idx + 1)..].Trim();
                if (address.Length == 0)
                {
                    continue;
                }

                result[address] = ParseValue(valueText);
            }

            return result;
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

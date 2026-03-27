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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Nodes
{
    public class WriteNode : Node
    {
        [Display(Name = "PLC设备名称", GroupName = "PLC配置", Order = 1)]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public string PlcDeviceName { get; set; } = string.Empty;

        [Display(Name = "使用IO配置库", GroupName = "写入配置", Order = 2, Description = "true=按IO名称选择并自动取地址；false=手工填写地址")]
        public bool UseIoConfig { get; set; }

        [Display(Name = "IO名称", GroupName = "写入配置", Order = 3, Description = "从 IO 配置库选择 IO 名称")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcIoProvider), "GetIoNames", DisplayMemberPath = ".")]
        public string IoName { get; set; } = string.Empty;

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
                    }

                    if (string.IsNullOrWhiteSpace(address))
                    {
                        return ExecutionResult.Failed("单地址写入模式下 Address 不能为空");
                    }

                    var value = ParseValue(ValueText);
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
                    return ExecutionResult.Successful("PLC 单地址写入完成")
                        .WithOutput("PlcDeviceName", selectedName)
                        .WithOutput("PlcDeviceNames", new List<string> { selectedName })
                        .WithOutput("Address", address)
                        .WithOutput("WrittenValue", value);
                }

                var writeMap = ParseWriteMap(MultiWriteText);
                if (writeMap.Count == 0)
                {
                    return ExecutionResult.Failed("多地址写入模式下 MultiWriteText 不能为空");
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

                log.Info($"多地址写入完成，PLC={selectedName}, 项数={writeMap.Count}");
                return ExecutionResult.Successful("PLC 多地址写入完成")
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

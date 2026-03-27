using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using Astra.Plugins.PLC.Providers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Nodes
{
    public class WriteNode : Node
    {
        [Display(Name = "PLC设备名称(可多选)", GroupName = "PLC配置", Order = 1)]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public ObservableCollection<string> PlcDeviceNames { get; set; } = new();

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
                var selectedNames = PlcDeviceNames?
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
                if (selectedNames.Count == 0)
                {
                    return ExecutionResult.Failed("请至少选择一个 PLC 设备");
                }

                var plcs = ResolvePlcsByNames(selectedNames);
                if (plcs.Count == 0)
                {
                    return ExecutionResult.Failed($"未找到已选择 PLC 设备: {string.Join(", ", selectedNames)}");
                }

                if (!UseMultiAddress)
                {
                    if (string.IsNullOrWhiteSpace(Address))
                    {
                        return ExecutionResult.Failed("单地址写入模式下 Address 不能为空");
                    }

                    var value = ParseValue(ValueText);
                    var errors = new List<string>();
                    var succeededPlcs = new List<string>();
                    foreach (var (plcName, plc) in plcs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var connectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                        if (!connectResult.Success)
                        {
                            errors.Add($"{plcName}: {connectResult.Message ?? "PLC连接失败"}");
                            continue;
                        }

                        var writeResult = await plc.WriteAsync(Address.Trim(), value, cancellationToken).ConfigureAwait(false);
                        if (!writeResult.Success)
                        {
                            errors.Add($"{plcName}: {writeResult.ErrorMessage ?? $"写入失败: {Address}"}");
                            continue;
                        }

                        succeededPlcs.Add(plcName);
                    }

                    if (succeededPlcs.Count == 0)
                    {
                        return ExecutionResult.Failed($"PLC 单地址写入失败: {string.Join(" | ", errors)}");
                    }

                    log.Info($"写入完成，地址={Address}, 成功PLC数={succeededPlcs.Count}");
                    var result = ExecutionResult.Successful("PLC 单地址写入完成")
                        .WithOutput("PlcDeviceNames", plcs.Select(x => x.Name).ToList())
                        .WithOutput("Address", Address.Trim())
                        .WithOutput("WrittenValue", value)
                        .WithOutput("SucceededPlcNames", succeededPlcs);
                    if (errors.Count > 0)
                    {
                        result = result.WithOutput("WriteErrors", errors);
                    }

                    return result;
                }

                var writeMap = ParseWriteMap(MultiWriteText);
                if (writeMap.Count == 0)
                {
                    return ExecutionResult.Failed("多地址写入模式下 MultiWriteText 不能为空");
                }

                var batchErrors = new List<string>();
                var batchSucceededPlcs = new List<string>();
                foreach (var (plcName, plc) in plcs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var connectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                    if (!connectResult.Success)
                    {
                        batchErrors.Add($"{plcName}: {connectResult.Message ?? "PLC连接失败"}");
                        continue;
                    }

                    var batchResult = await plc.BatchWriteAsync(writeMap, cancellationToken).ConfigureAwait(false);
                    if (!batchResult.Success)
                    {
                        batchErrors.Add($"{plcName}: {batchResult.ErrorMessage ?? "多地址写入失败"}");
                        continue;
                    }

                    batchSucceededPlcs.Add(plcName);
                }

                if (batchSucceededPlcs.Count == 0)
                {
                    return ExecutionResult.Failed($"PLC 多地址写入失败: {string.Join(" | ", batchErrors)}");
                }

                log.Info($"多地址写入完成，成功PLC数={batchSucceededPlcs.Count}, 项数={writeMap.Count}");
                var batchWriteResult = ExecutionResult.Successful("PLC 多地址写入完成")
                    .WithOutput("PlcDeviceNames", plcs.Select(x => x.Name).ToList())
                    .WithOutput("WrittenValues", writeMap)
                    .WithOutput("SucceededPlcNames", batchSucceededPlcs);
                if (batchErrors.Count > 0)
                {
                    batchWriteResult = batchWriteResult.WithOutput("WriteErrors", batchErrors);
                }

                return batchWriteResult;
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

        private static List<(string Name, IPLC Plc)> ResolvePlcsByNames(IEnumerable<string> plcDeviceNames)
        {
            var plugin = PlcPlugin.Current;
            var result = new List<(string Name, IPLC Plc)>();
            if (plugin == null)
            {
                return result;
            }

            var all = plugin.GetAllPlcs();
            foreach (var name in plcDeviceNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var plc = all.FirstOrDefault(p =>
                {
                    if (p is not IDevice d)
                    {
                        return false;
                    }

                    return string.Equals(d.DeviceName, name, StringComparison.OrdinalIgnoreCase);
                });
                if (plc != null)
                {
                    result.Add((name, plc));
                }
            }

            return result;
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

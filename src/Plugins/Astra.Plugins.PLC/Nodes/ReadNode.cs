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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Nodes
{
    public class ReadNode : Node
    {
        [Display(Name = "PLC设备名称(可多选)", GroupName = "PLC配置", Order = 1)]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(typeof(PlcDeviceProvider), "GetPlcDeviceNames", DisplayMemberPath = ".")]
        public ObservableCollection<string> PlcDeviceNames { get; set; } = new();

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
                        return ExecutionResult.Failed("单地址读取模式下 Address 不能为空");
                    }

                    var key = string.IsNullOrWhiteSpace(OutputKey) ? "Value" : OutputKey.Trim();
                    var perPlcValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    var errors = new List<string>();

                    foreach (var (plcName, plc) in plcs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var connectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                        if (!connectResult.Success)
                        {
                            errors.Add($"{plcName}: {connectResult.Message ?? "PLC连接失败"}");
                            continue;
                        }

                        var readResult = await plc.ReadAsync<object>(Address.Trim(), cancellationToken).ConfigureAwait(false);
                        if (!readResult.Success)
                        {
                            errors.Add($"{plcName}: {readResult.ErrorMessage ?? $"读取失败: {Address}"}");
                            continue;
                        }

                        var value = readResult.Data!;
                        perPlcValues[plcName] = value;

                        var globalKey = plcs.Count == 1 ? key : $"{plcName}.{key}";
                        context.SetGlobalVariable(globalKey, value);
                    }

                    if (errors.Count > 0 && perPlcValues.Count == 0)
                    {
                        return ExecutionResult.Failed($"PLC 单地址读取失败: {string.Join(" | ", errors)}");
                    }

                    log.Info($"读取完成，地址={Address}, 成功PLC数={perPlcValues.Count}");
                    var result = ExecutionResult.Successful("PLC 单地址读取完成")
                        .WithOutput("PlcDeviceNames", plcs.Select(x => x.Name).ToList())
                        .WithOutput("Address", Address.Trim())
                        .WithOutput("ReadValuesByPlc", perPlcValues);
                    if (errors.Count > 0)
                    {
                        result = result.WithOutput("ReadErrors", errors);
                    }

                    return result;
                }

                var addressMap = ParseAddressMap(MultiAddressText);
                if (addressMap.Count == 0)
                {
                    return ExecutionResult.Failed("多地址读取模式下 MultiAddressText 不能为空");
                }

                var perPlcMultiValues = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
                var multiErrors = new List<string>();
                foreach (var (plcName, plc) in plcs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var connectResult = await EnsureConnectedAsync(plc, cancellationToken).ConfigureAwait(false);
                    if (!connectResult.Success)
                    {
                        multiErrors.Add($"{plcName}: {connectResult.Message ?? "PLC连接失败"}");
                        continue;
                    }

                    var batchResult = await plc.BatchReadAsync(addressMap, cancellationToken).ConfigureAwait(false);
                    if (!batchResult.Success)
                    {
                        multiErrors.Add($"{plcName}: {batchResult.ErrorMessage ?? "多地址读取失败"}");
                        continue;
                    }

                    var values = batchResult.Data ?? new Dictionary<string, object>();
                    perPlcMultiValues[plcName] = values;

                    foreach (var kv in values)
                    {
                        var globalKey = plcs.Count == 1 ? kv.Key : $"{plcName}.{kv.Key}";
                        context.SetGlobalVariable(globalKey, kv.Value);
                    }
                }

                if (multiErrors.Count > 0 && perPlcMultiValues.Count == 0)
                {
                    return ExecutionResult.Failed($"PLC 多地址读取失败: {string.Join(" | ", multiErrors)}");
                }

                log.Info($"多地址读取完成，成功PLC数={perPlcMultiValues.Count}");
                var multiResult = ExecutionResult.Successful("PLC 多地址读取完成")
                    .WithOutput("PlcDeviceNames", plcs.Select(x => x.Name).ToList())
                    .WithOutput("ReadValuesByPlc", perPlcMultiValues);
                if (multiErrors.Count > 0)
                {
                    multiResult = multiResult.WithOutput("ReadErrors", multiErrors);
                }

                return multiResult;
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
    }
}

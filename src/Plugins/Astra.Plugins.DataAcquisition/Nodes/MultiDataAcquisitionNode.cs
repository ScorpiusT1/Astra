using Astra.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Nodes.Models;
using Astra.Plugins.DataAcquisition.Providers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataAcquisition.Nodes
{
    /// <summary>
    /// 多采集卡采集节点：在一个脚本节点中同时控制多个采集卡的启动。
    /// </summary>
    public class MultiDataAcquisitionNode : Node
    {
        [Display(Name = "采集时长(秒)", GroupName = "采集卡配置", Order = 1, Description = "为 0 表示只启动不自动停止")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// 选中的采集卡设备名称列表（仅保存 DeviceName，避免在脚本中序列化具体设备实例）。
        /// </summary>
        [Display(Name = "选择采集卡", GroupName = "采集卡配置", Order = 2)]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(typeof(DataAcquisitionCardProvider), "GetDataAcquisitionNames", DisplayMemberPath = ".")]
        public List<string> DataAcquisitionDeviceNames { get; set; } = new();

        protected override async Task<ExecutionResult> ExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken)
        {
            // 未选择设备时直接跳过，避免报错
            if (DataAcquisitionDeviceNames == null || DataAcquisitionDeviceNames.Count == 0)
            {
                return ExecutionResult.Skip("未选择任何采集卡");
            }

            // 通过当前插件中的设备列表，根据 DeviceName 解析出实际的设备实例
            var plugin = DataAcquisitionPlugin.Current;
            if (plugin == null)
            {
                return ExecutionResult.Skip("数据采集插件未初始化，无法获取采集卡列表");
            }

            var allDevices = plugin.GetAllDataAcquisitions() ?? new List<IDataAcquisition>();

            // 先按 IDevice 过滤名称，再还原为 IDataAcquisition 以便后续调用采集接口
            var deviceNameSet = new HashSet<string>(DataAcquisitionDeviceNames);

            var distinctDevices = allDevices
                .Where(d =>
                {
                    var info = d as IDevice;
                    return info != null && deviceNameSet.Contains(info.DeviceName);
                })
                .Distinct()
                .ToList();

            if (distinctDevices.Count == 0)
            {
                return ExecutionResult.Skip("采集卡列表为空或无效");
            }

            var startedDevices = new ConcurrentBag<string>();
            var startErrors = new ConcurrentBag<Exception>();

            try
            {
                // 并行启动所有未在运行状态的采集卡
                var startTasks = distinctDevices.Select(async device =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // 已在运行的设备不重复启动
                        var state = device.GetState();
                        if (state == AcquisitionState.Running)
                        {
                            return;
                        }

                        // 确保已初始化
                        await device.InitializeAsync().ConfigureAwait(false);

                        // 启动采集
                        await device.StartAcquisitionAsync(cancellationToken).ConfigureAwait(false);

                        startedDevices.Add(device.DeviceId);
                    }
                    catch (Exception ex)
                    {
                        startErrors.Add(ex);
                    }
                }).ToList();

                await Task.WhenAll(startTasks).ConfigureAwait(false);

                if (!startErrors.IsEmpty)
                {
                    // 有任意一块采集卡启动失败，则整体返回失败（但已经启动成功的卡仍保持运行）
                    var errorArray = startErrors.ToArray();
                    var aggregate = new AggregateException("部分采集卡启动失败", errorArray);

                    return ExecutionResult.Failed("部分采集卡启动失败，请检查日志获取详细信息", aggregate)
                        .WithOutput("StartErrors", errorArray.Select(e => e.Message).ToArray());
                }

                var startedList = startedDevices.ToList();

                // 如果配置了采集时长，则当前节点等待指定时长后自动停止刚刚启动的设备（并行停止）
                if (DurationSeconds > 0 && startedList.Count > 0)
                {
                    var delayMs = (int)(DurationSeconds * 1000);
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);

                    var startedSet = new HashSet<string>(startedList);

                    var stopTasks = distinctDevices
                        .Where(d => startedSet.Contains(d.DeviceId))
                        .Select(async device =>
                        {
                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                await device.StopAcquisitionAsync().ConfigureAwait(false);
                            }
                            catch
                            {
                                // 单个设备停止失败不影响其它设备
                            }
                        }).ToList();

                    await Task.WhenAll(stopTasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return ExecutionResult.Cancel("多采集卡采集被取消");
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed("多采集卡采集过程中发生异常", ex);
            }

            if (startedDevices.IsEmpty)
            {
                return ExecutionResult.Skip("所有采集卡均已在运行状态，无需重复启动");
            }

            return ExecutionResult
                .Successful("多采集卡采集已完成")
                .WithOutput("StartedDevices", startedDevices.ToList())
                .WithOutput("DurationSeconds", DurationSeconds);
        }
    }
}

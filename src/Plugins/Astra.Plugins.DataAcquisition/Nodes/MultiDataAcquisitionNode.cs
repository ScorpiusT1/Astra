using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Ui;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Plugins.DataAcquisition.Providers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;

namespace Astra.Plugins.DataAcquisition.Nodes
{
    /// <summary>
    /// 多采集卡采集节点：在一个脚本节点中同时控制多个采集卡的启动。
    /// </summary>
    public class MultiDataAcquisitionNode : Node
    {

        [Display(Name = "采集完成后自动停止", GroupName = "采集卡配置", Order = 1, Description = "为 true 时在采集时长结束后停止本次节点启动的采集卡；为 false 时保持运行")]
        public bool StopAcquisitionAfterCompletion { get; set; } = true;

        [Display(Name = "采集时长(秒)", GroupName = "采集卡配置", Order = 2, Description = "为 0 表示只启动不自动停止")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// 选中的采集卡设备名称列表（仅保存 DeviceName，避免在脚本中序列化具体设备实例）。
        /// </summary>
        [Display(Name = "选择采集卡", GroupName = "采集卡配置", Order = 3)]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(typeof(DataAcquisitionCardProvider), "GetDataAcquisitionNames", DisplayMemberPath = ".")]
        public List<string> DataAcquisitionDeviceNames { get; set; } = new();

        protected override async Task<ExecutionResult> ExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"数据采集节点:{Name}");

            var executionController = context?.GetMetadata<IWorkflowExecutionController>("WorkflowExecutionController");

            async Task WaitIfPausedAsync()
            {
                if (executionController == null)
                {
                    return;
                }

                await executionController.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            }

            // 未选择设备时直接跳过，避免报错
            if (DataAcquisitionDeviceNames == null || DataAcquisitionDeviceNames.Count == 0)
            {
                log.Warn("未选择任何采集卡，节点跳过。");
                return ExecutionResult.Skip("未选择任何采集卡");
            }

            // 通过当前插件中的设备列表，根据 DeviceName 解析出实际的设备实例
            var plugin = DataAcquisitionPlugin.Current;
            if (plugin == null)
            {
                log.Error("数据采集插件未初始化，无法获取采集卡列表。");
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
                log.Warn("采集卡列表为空或无效，节点跳过。");
                return ExecutionResult.Skip("采集卡列表为空或无效");
            }

            var startedDevices = new ConcurrentBag<string>();
            var runningDevices = new ConcurrentBag<string>();
            var startErrors = new ConcurrentBag<Exception>();

            var actualDurationSeconds = 0d;
            try
            {
                log.Info($"开始执行，目标采集卡数量: {distinctDevices.Count}。");
                await WaitIfPausedAsync().ConfigureAwait(false);

                var startCandidates = new ConcurrentBag<IDataAcquisition>();

                // 第一阶段：并行预热（初始化 + 建连），不做硬件启动
                var prepareTasks = distinctDevices.Select(async device =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await WaitIfPausedAsync().ConfigureAwait(false);

                        // 已在运行的设备不重复启动
                        var state = device.GetState();
                        if (state == AcquisitionState.Running)
                        {
                            runningDevices.Add(device.DeviceId);
                            return;
                        }

                        // 确保已初始化
                        var initResult = await device.InitializeAsync().ConfigureAwait(false);
                        if (!initResult.Success)
                        {
                            throw new InvalidOperationException(
                                $"采集卡初始化失败: {device.DeviceId}, {initResult.ErrorMessage ?? initResult.Message}");
                        }

                        // 建立连接（为第二阶段统一启动做准备）
                        if (device is IDevice connectableDevice)
                        {
                            var connectResult = await connectableDevice.ConnectAsync(cancellationToken).ConfigureAwait(false);
                            if (!connectResult.Success)
                            {
                                throw new InvalidOperationException(
                                    $"采集卡连接失败: {device.DeviceId}, {connectResult.ErrorMessage ?? connectResult.Message}");
                            }
                        }

                        startCandidates.Add(device);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        startErrors.Add(ex);
                    }
                }).ToList();

                await Task.WhenAll(prepareTasks).ConfigureAwait(false);
                await WaitIfPausedAsync().ConfigureAwait(false);

                if (!startErrors.IsEmpty)
                {
                    // 有任意一块采集卡启动失败，则整体返回失败（但已经启动成功的卡仍保持运行）
                    var errorArray = startErrors.ToArray();
                    var aggregate = new AggregateException("部分采集卡启动失败", errorArray);

                    log.Error($"并行预热阶段失败，失败数量: {errorArray.Length}。");
                    return ExecutionResult.Failed("部分采集卡启动失败，请检查日志获取详细信息", aggregate)
                        .WithOutput("StartErrors", errorArray.Select(e => e.Message).ToArray());
                }

                // 第二阶段：统一并行启动硬件，尽量压缩设备间起跑偏差
                var startTasks = startCandidates.Select(async device =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await WaitIfPausedAsync().ConfigureAwait(false);

                        var startResult = await device.StartAcquisitionAsync(cancellationToken).ConfigureAwait(false);
                        if (!startResult.Success)
                        {
                            throw new InvalidOperationException(
                                $"采集卡启动失败: {device.DeviceId}, {startResult.ErrorMessage ?? startResult.Message}");
                        }

                        startedDevices.Add(device.DeviceId);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        startErrors.Add(ex);
                    }
                }).ToList();

                await Task.WhenAll(startTasks).ConfigureAwait(false);
                await WaitIfPausedAsync().ConfigureAwait(false);

                if (!startErrors.IsEmpty)
                {
                    var errorArray = startErrors.ToArray();
                    var aggregate = new AggregateException("部分采集卡启动失败", errorArray);

                    log.Error($"统一启动阶段失败，失败数量: {errorArray.Length}。");
                    return ExecutionResult.Failed("部分采集卡启动失败，请检查日志获取详细信息", aggregate)
                        .WithOutput("StartErrors", errorArray.Select(e => e.Message).ToArray());
                }

                var startedList = startedDevices.ToList();

                var activeDevices = startedList
                    .Concat(runningDevices)
                    .Distinct()
                    .ToList();

                // 如果配置了采集时长，则当前节点等待指定时长
                if (DurationSeconds > 0 && activeDevices.Count > 0)
                {
                    log.Info($"进入采集等待阶段，配置时长: {DurationSeconds:F2}s。");
                    var delayMs = (int)(DurationSeconds * 1000);
                    const int delaySliceMs = 100;
                    var durationStopwatch = Stopwatch.StartNew();
                    while (durationStopwatch.ElapsedMilliseconds < delayMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await WaitIfPausedAsync().ConfigureAwait(false);
                        var remainingMs = delayMs - (int)durationStopwatch.ElapsedMilliseconds;
                        if (remainingMs <= 0)
                        {
                            break;
                        }

                        var nextDelay = Math.Min(delaySliceMs, remainingMs);
                        await Task.Delay(nextDelay, cancellationToken).ConfigureAwait(false);
                    }
                    durationStopwatch.Stop();
                    actualDurationSeconds = durationStopwatch.Elapsed.TotalSeconds;

                    if (StopAcquisitionAfterCompletion && startedList.Count > 0)
                    {
                        var startedSet = new HashSet<string>(startedList);

                        var stopTasks = distinctDevices
                            .Where(d => startedSet.Contains(d.DeviceId))
                            .Select(async device =>
                            {
                                try
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    await WaitIfPausedAsync().ConfigureAwait(false);
                                    var stopResult = await device.StopAcquisitionAsync().ConfigureAwait(false);
                                    if (!stopResult.Success)
                                    {
                                        throw new InvalidOperationException(
                                            $"采集卡停止失败: {device.DeviceId}, {stopResult.ErrorMessage ?? stopResult.Message}");
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch
                                {
                                    // 单个设备停止失败不影响其它设备
                                }
                            }).ToList();

                        await Task.WhenAll(stopTasks).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                log.Warn("执行被取消。");
                return ExecutionResult.Cancel("多采集卡采集被取消");
            }
            catch (Exception ex)
            {
                log.Error($"执行异常: {ex.Message}");
                return ExecutionResult.Failed("多采集卡采集过程中发生异常", ex);
            }

            if (startedDevices.IsEmpty && runningDevices.IsEmpty)
            {
                log.Info("所有采集卡均已在运行，未重复启动。");
                return ExecutionResult.Skip("所有采集卡均已在运行状态，无需重复启动");
            }

            var startedListForOutput = startedDevices.ToList();
            var runningListForOutput = runningDevices.Distinct().ToList();
            var activeDeviceListForOutput = startedListForOutput
                .Concat(runningListForOutput)
                .Distinct()
                .ToList();
            var result = ExecutionResult
                .Successful("多采集卡采集已完成")
                .WithOutput("StartedDevices", startedListForOutput)
                .WithOutput("RunningDevices", runningListForOutput)
                .WithOutput("ActiveDevices", activeDeviceListForOutput)
                .WithOutput("DurationSeconds", DurationSeconds)
                .WithOutput("ActualDurationSeconds", actualDurationSeconds)
                .WithOutput("StopAcquisitionAfterCompletion", StopAcquisitionAfterCompletion);
            log.Info($"执行完成，启动设备 {startedListForOutput.Count} 个，已运行设备 {runningListForOutput.Count} 个。");

            var rawDataStore = context.GetRawDataStore();
            if (rawDataStore != null)
            {
                var rawDataKeys = new List<string>();
                var activeSet = new HashSet<string>(activeDeviceListForOutput);

                foreach (var device in distinctDevices)
                {
                    if (!activeSet.Contains(device.DeviceId))
                    {
                        continue;
                    }

                    if (device is not DataAcquisitionDeviceBase daqDevice)
                    {
                        continue;
                    }

                    var dataFile = daqDevice.GetDataFile();
                    if (dataFile == null)
                    {
                        continue;
                    }

                    var artifactRef = context.StoreArtifact(
                        nodeId: Id,
                        category: DataArtifactCategory.Raw,
                        artifactName: $"{device.DeviceId}:raw",
                        data: dataFile,
                        displayName: $"{(device as IDevice)?.DeviceName ?? device.DeviceId}-RawData",
                        description: $"Device={device.DeviceId}, Node={Id}, Duration={DurationSeconds}s",
                        preview: new Dictionary<string, object>
                        {
                            ["DeviceId"] = device.DeviceId,
                            ["NodeId"] = Id,
                            ["DurationSeconds"] = DurationSeconds
                        });
                    if (artifactRef == null)
                    {
                        continue;
                    }

                    rawDataKeys.Add(artifactRef.Key);

                    var rawRef = new RawDataReference
                    {
                        Key = artifactRef.Key,
                        Category = artifactRef.Category,
                        DataType = dataFile.GetType().FullName,
                        DisplayName = artifactRef.DisplayName,
                        Description = artifactRef.Description,
                        Preview = artifactRef.Preview,
                        CreatedAt = artifactRef.CreatedAt
                    };

                    result = result
                        .WithOutput($"RawDataRef:{device.DeviceId}", rawRef)
                        .WithOutput($"ArtifactRef:{device.DeviceId}", rawRef);
                }

                if (rawDataKeys.Count > 0)
                {
                    result = result
                        .WithOutput("RawDataKeys", rawDataKeys)
                        .WithOutput(NodeUiOutputKeys.HasChartData, true)
                        .WithOutput(NodeUiOutputKeys.ChartArtifactKey, rawDataKeys[0]);
                }
            }

            return result;
        }
    }
}

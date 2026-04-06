using System;
using Astra.Contract.Communication.Abstractions;
using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Plugins.DataAcquisition.Providers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
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
    public class MultiDataAcquisitionNode : Node, IHomeTestItemChartNode, IPropertyVisibilityProvider, IMultiRawDataPipelineNode, IDesignTimeDataSourceInfo, IReportWhitelistChartProducerNode
    {
        private bool _usePropertyPanelForChartAxis = true;

        [Order(2)]
        [Display(Name = "显示图表按钮", GroupName = "基础配置", Order = 3, Description = "开启后在首页测试项操作栏显示「打开图表」；需同时开启「在主页测试项中显示」。关闭后若本次执行仍输出了曲线数据，按钮仍可出现。")]
        public bool ShowHomeChartButton { get; set; } = true;

        [Order(1)]
        [Display(Name = "在属性面板配置坐标轴", GroupName = "图表", Order = 0, Description = "开启后使用下方 X/Y 轴标签与单位（留空时横轴默认为「时间」、单位为秒）；关闭后隐藏下方四项，纵轴单位由灵敏度换算得到，横轴为时间(秒)。")]
        public bool UsePropertyPanelForChartAxis
        {
            get => _usePropertyPanelForChartAxis;
            set
            {
                if (_usePropertyPanelForChartAxis == value)
                {
                    return;
                }

                _usePropertyPanelForChartAxis = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "图表 X 轴标签", GroupName = "图表", Order = 1, Description = "主页测试项图表窗口底部轴标题。留空时默认为「时间」。")]
        public string ChartXAxisLabel { get; set; } = "";

        [Display(Name = "图表 X 轴单位", GroupName = "图表", Order = 2, Description = "显示在 X 轴标题后的单位。留空时默认为秒(s)。")]
        public string ChartXAxisUnit { get; set; } = string.Empty;

        [Display(Name = "图表 Y 轴标签", GroupName = "图表", Order = 3, Description = "主页测试项图表窗口左侧轴标题。")]
        public string ChartYAxisLabel { get; set; } = "";

        [Display(Name = "图表 Y 轴单位", GroupName = "图表", Order = 4, Description = "显示在 Y 轴标题后的单位，可留空。")]
        public string ChartYAxisUnit { get; set; } = string.Empty;

        [Display(Name = "多曲线显示模式", GroupName = "图表", Order = 5, Description = "单图叠加：所有系列绘制在一个 Plot；分子图：每个系列独立显示在一个子图。留空时按类型自动推断（同类型叠加，混合类型分子图）。")]
        public ChartLayoutMode ChartLayoutMode { get; set; } = ChartLayoutMode.SinglePlot;

        [Order(0)]
        [Display(Name = "采集完成后自动停止", GroupName = "采集卡配置", Order = 1, Description = "为 true 时在采集时长结束后停止本次节点启动的采集卡；为 false 时保持运行")]
        public bool StopAcquisitionAfterCompletion { get; set; } = true;

        [Display(Name = "采集时长(秒)", GroupName = "采集卡配置", Order = 2, Description = "为 0 表示只启动不自动停止。大于 0 时优先按「配置采样率 × 本时长」得到目标样本数，待各卡已启用通道的最小累积样本数达到后再停；若无法按样本判断则回退为按墙上时钟等待。")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// 选中的采集卡设备名称列表（仅保存 DeviceName，避免在脚本中序列化具体设备实例）。
        /// </summary>     
        [Display(Name = "选择采集卡", GroupName = "采集卡配置", Order = 3)]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(typeof(DataAcquisitionCardProvider), "GetHardwareDataAcquisitionNames", DisplayMemberPath = ".")]
        public List<string> DataAcquisitionDeviceNames
        {
            get => _dataAcquisitionDeviceNames;
            set
            {
                value ??= new();
                if (_dataAcquisitionDeviceNames.SequenceEqual(value, StringComparer.Ordinal))
                    return;
                _dataAcquisitionDeviceNames = value;
                OnPropertyChanged();
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(Id);
            }
        }

        private List<string> _dataAcquisitionDeviceNames = new();

        IEnumerable<string> IMultiRawDataPipelineNode.DataAcquisitionDeviceDisplayNames => DataAcquisitionDeviceNames;

        public IEnumerable<string> GetAvailableDeviceDisplayNames() =>
            DataAcquisitionDeviceNames?.Where(d => !string.IsNullOrWhiteSpace(d)) ?? Enumerable.Empty<string>();

        public IEnumerable<string> GetAvailableChannelNames(string deviceDisplayName) =>
            DataAcquisitionCardProvider.GetConfiguredChannelNamesForDeviceDisplayName(deviceDisplayName)
                .Where(ch => !string.IsNullOrEmpty(ch));

        /// <summary>未在属性面板填写横轴标签时，默认时间轴标题。</summary>
        private const string DefaultChartXAxisLabel = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartXAxisLabel;

        /// <summary>未在属性面板填写横轴单位时，默认秒。</summary>
        private const string DefaultChartXAxisUnit = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartXAxisUnit;

        /// <summary>关闭「在属性面板配置坐标轴」时，输出到主页图表的纵轴标签（节点代码侧默认）。</summary>
        private const string CodeDefinedChartYAxisLabel = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartYAxisLabel;

        /// <summary>关闭「在属性面板配置坐标轴」且无法从传感器解析单位时，纵轴单位回退值。</summary>
        private const string CodeDefinedChartYAxisUnitFallback = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartYAxisUnitFallback;

        public bool IsPropertyVisible(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return true;
            }

            if (propertyName is nameof(ChartXAxisLabel) or nameof(ChartXAxisUnit) or nameof(ChartYAxisLabel) or nameof(ChartYAxisUnit))
            {
                return UsePropertyPanelForChartAxis;
            }

            return true;
        }

        /// <param name="physicalYAxisUnitFromSensor">由灵敏度换算得到的纵轴物理单位（关闭属性面板坐标轴时使用）。</param>
        private (string XLabel, string XUnit, string YLabel, string YUnit) ResolveChartAxisOutputs(string? physicalYAxisUnitFromSensor = null)
        {
            if (UsePropertyPanelForChartAxis)
            {
                var xLabel = string.IsNullOrWhiteSpace(ChartXAxisLabel) ? DefaultChartXAxisLabel : ChartXAxisLabel.Trim();
                var xUnit = string.IsNullOrWhiteSpace(ChartXAxisUnit) ? DefaultChartXAxisUnit : ChartXAxisUnit.Trim();
                var yLabel = string.IsNullOrWhiteSpace(ChartYAxisLabel) ? CodeDefinedChartYAxisLabel : ChartYAxisLabel.Trim();
                var panelYAxisUnit = ChartYAxisUnit?.Trim() ?? string.Empty;
                return (xLabel, xUnit, yLabel, panelYAxisUnit);
            }

            var physicalyUnit = string.IsNullOrWhiteSpace(physicalYAxisUnitFromSensor)
                ? CodeDefinedChartYAxisUnitFallback
                : physicalYAxisUnitFromSensor.Trim();

            return (DefaultChartXAxisLabel, DefaultChartXAxisUnit, CodeDefinedChartYAxisLabel, physicalyUnit);
        }

        protected override async Task<ExecutionResult> ExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"数据采集节点:{Name}");

            var executionController = context?.GetMetadata<IWorkflowExecutionController>(ExecutionContextMetadataKeys.WorkflowExecutionController);

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

            var deviceLabelList = string.Join(", ",
                distinctDevices.Select(d =>
                {
                    var dev = d as IDevice;
                    return dev != null && !string.IsNullOrWhiteSpace(dev.DeviceName)
                        ? $"{dev.DeviceName}({d.DeviceId})"
                        : d.DeviceId;
                }));
            log.Info($"解析到采集设备 {distinctDevices.Count} 台: {deviceLabelList}。配置: 时长={DurationSeconds:F2}s, 完成后自动停止={StopAcquisitionAfterCompletion}。");

            var startedDevices = new ConcurrentBag<string>();
            var runningDevices = new ConcurrentBag<string>();
            var startErrors = new ConcurrentBag<Exception>();

            var actualDurationSeconds = 0d;
            var usedSampleBasedStop = false;
            long? actualMinSamplesAcrossDevices = null;
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

                var prepareCandidateCount = startCandidates.Count;
                log.Info($"并行预热(初始化/连接)完成，待统一启动设备数: {prepareCandidateCount}，此前已在运行跳过预热: {runningDevices.Count}。");

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

                log.Info($"统一启动硬件完成，本轮新启动: {startedDevices.Count}，此前已在运行: {runningDevices.Count}。");

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

                if (DurationSeconds <= 0)
                {
                    log.Info("采集时长≤0：仅完成启动，不进入按时长/样本等待；是否自动停采取决于「采集完成后自动停止」与后续流程。");
                }
                else if (activeDevices.Count == 0)
                {
                    log.Warn("无活动采集设备，跳过按时长等待。");
                }

                // 如果配置了采集时长：优先按「采样率 × 时长」得到目标样本数并轮询，否则回退为墙上时钟等待
                if (DurationSeconds > 0 && activeDevices.Count > 0)
                {
                    const int delaySliceMs = AstraSharedConstants.DataAcquisitionDefaults.DelaySliceMs;
                    var delayMs = (int)(DurationSeconds * 1000);

                    var activeRefs = distinctDevices
                        .Where(d => activeDevices.Contains(d.DeviceId))
                        .ToList();

                    var sampleTargets = new List<(DataAcquisitionDeviceBase Device, long Target)>();
                    var canUseSampleStop = true;
                    foreach (var d in activeRefs)
                    {
                        if (d is not DataAcquisitionDeviceBase daq)
                        {
                            canUseSampleStop = false;
                            break;
                        }

                        var sr = daq.CurrentConfig.SampleRate;
                        if (sr <= 0)
                        {
                            canUseSampleStop = false;
                            break;
                        }

                        var target = Math.Max(1L, (long)Math.Round(DurationSeconds * sr, MidpointRounding.AwayFromZero));
                        sampleTargets.Add((daq, target));
                    }

                    if (sampleTargets.Count == 0)
                    {
                        canUseSampleStop = false;
                    }

                    var durationStopwatch = Stopwatch.StartNew();

                    EventHandler? onPausing = null;
                    EventHandler? onResumed = null;
                    EventHandler? onCancelling = null;
                    if (executionController != null)
                    {
                        onPausing = (_, _) => durationStopwatch.Stop();
                        onResumed = (_, _) => durationStopwatch.Start();
                        onCancelling = (_, _) => durationStopwatch.Stop();
                        executionController.Pausing += onPausing;
                        executionController.Resumed += onResumed;
                        executionController.Cancelling += onCancelling;
                    }

                    try
                    {
                        if (canUseSampleStop)
                        {
                            usedSampleBasedStop = true;
                            log.Info(
                                $"进入按样本等待阶段，配置时长: {DurationSeconds:F2}s；目标样本数(每卡): " +
                                $"{string.Join(", ", sampleTargets.Select(t => $"{t.Device.DeviceId}={t.Target}"))}。");

                            var maxActiveWaitMs = (long)(DurationSeconds * 1000 * 2);

                            while (true)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                await WaitIfPausedAsync().ConfigureAwait(false);

                                if (sampleTargets.All(t =>
                                    {
                                        var n = t.Device.TryGetMinimumAcquiredSampleCountAcrossEnabledChannels();
                                        return n.HasValue && n.Value >= t.Target;
                                    }))
                                {
                                    break;
                                }

                                if (durationStopwatch.ElapsedMilliseconds > maxActiveWaitMs)
                                {
                                    log.Warn(
                                        $"按样本等待已超过有效时长上限 ({maxActiveWaitMs}ms)，将停止等待并进入停采。");
                                    break;
                                }

                                await Task.Delay(delaySliceMs, cancellationToken).ConfigureAwait(false);
                            }

                            durationStopwatch.Stop();
                            actualDurationSeconds = durationStopwatch.Elapsed.TotalSeconds;

                            long? crossMin = null;
                            foreach (var t in sampleTargets)
                            {
                                var n = t.Device.TryGetMinimumAcquiredSampleCountAcrossEnabledChannels();
                                if (!n.HasValue)
                                {
                                    continue;
                                }

                                crossMin = crossMin.HasValue ? Math.Min(crossMin.Value, n.Value) : n.Value;
                            }

                            actualMinSamplesAcrossDevices = crossMin;
                            log.Info(
                                $"按样本等待结束，有效耗时约 {actualDurationSeconds:F2}s，各启用通道最小样本数(跨卡): {(crossMin?.ToString() ?? "未知")}。");
                        }
                        else
                        {
                            log.Info($"进入按时间等待阶段（回退），配置时长: {DurationSeconds:F2}s。");

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
                            log.Info($"按时间等待结束，实际耗时约 {actualDurationSeconds:F2}s。");
                        }
                    }
                    finally
                    {
                        if (executionController != null)
                        {
                            if (onPausing != null)
                            {
                                executionController.Pausing -= onPausing;
                            }

                            if (onResumed != null)
                            {
                                executionController.Resumed -= onResumed;
                            }

                            if (onCancelling != null)
                            {
                                executionController.Cancelling -= onCancelling;
                            }
                        }
                    }

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
                        log.Info($"已对本节点本轮启动的 {startedList.Count} 台设备发起停采（StopAcquisitionAfterCompletion=true）。");
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
                .WithOutput("StopAcquisitionAfterCompletion", StopAcquisitionAfterCompletion)
                .WithOutput("UsedSampleBasedAcquisitionStop", usedSampleBasedStop);

            if (actualMinSamplesAcrossDevices.HasValue)
            {
                result = result.WithOutput("ActualMinSamplesAcrossDevices", actualMinSamplesAcrossDevices.Value);
            }
            log.Info($"执行完成，启动设备 {startedListForOutput.Count} 个，已运行设备 {runningListForOutput.Count} 个。");

            var dataBus = context.GetDataBus();
            if (dataBus != null)
            {
                var rawDataKeys = new List<string>();
                var activeSet = new HashSet<string>(activeDeviceListForOutput);
                string? codePathPhysicalYUnit = null;

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
                        log.Warn($"设备 {daqDevice.DeviceId} 无可用内存数据文件，跳过发布至总线。");
                        continue;
                    }

                    var dataForArtifact = dataFile;
                    if (NvhMemoryFileSensitivityConversion.TryCreatePhysicalChannelCopy(
                            dataFile,
                            daqDevice,
                            out var physicalFile,
                            out var yUnitFromSensor) &&
                        physicalFile != null)
                    {
                        dataForArtifact = physicalFile;
                        if (string.IsNullOrEmpty(codePathPhysicalYUnit))
                        {
                            codePathPhysicalYUnit = yUnitFromSensor;
                        }

                        log.Info($"设备 {daqDevice.DeviceId} 已按灵敏度换算为物理量副本发布（Y 单位: {yUnitFromSensor ?? "未解析"}）。");
                    }

                    var artifactRef = dataBus.PublishRawData(
                        producerNodeId: Id,
                        artifactName: $"{device.DeviceId}:raw",
                        rawData: dataForArtifact,
                        displayName: $"{(device as IDevice)?.DeviceName ?? device.DeviceId}-RawData",
                        deviceId: device.DeviceId,
                        includeInTestReport: IncludeInTestReport);

                    rawDataKeys.Add(artifactRef.Key);

                    result = result
                        .WithOutput($"RawDataRef:{device.DeviceId}", artifactRef)
                        .WithOutput($"ArtifactRef:{device.DeviceId}", artifactRef);
                }

                if (rawDataKeys.Count > 0)
                {
                    log.Info($"已向 TestDataBus 发布 Raw 工件 {rawDataKeys.Count} 个，首键: {rawDataKeys[0]}。");
                    var axis = ResolveChartAxisOutputs(codePathPhysicalYUnit);
                    result = result
                        .WithOutput("RawDataKeys", rawDataKeys)
                        .WithOutput(NodeUiOutputKeys.HasChartData, true)
                        .WithOutput(NodeUiOutputKeys.ChartArtifactKey, rawDataKeys[0])
                        .WithOutput(NodeUiOutputKeys.ChartArtifactKeys, rawDataKeys.ToArray())
                        .WithOutput(NodeUiOutputKeys.ChartUseSubPlots, ChartLayoutMode == ChartLayoutMode.SubPlots)
                        .WithOutput(NodeUiOutputKeys.ChartXAxisLabel, axis.XLabel)
                        .WithOutput(NodeUiOutputKeys.ChartXAxisUnit, axis.XUnit)
                        .WithOutput(NodeUiOutputKeys.ChartYAxisLabel, axis.YLabel)
                        .WithOutput(NodeUiOutputKeys.ChartYAxisUnit, axis.YUnit);
                }
                else if (activeDeviceListForOutput.Count > 0)
                {
                    log.Warn("存在活动设备但未发布任何 Raw 工件（可能无内存文件或类型不匹配），下游算法将无法从本节点取数。");
                }
            }
            else
            {
                log.Warn("NodeContext 未绑定 TestDataBus，跳过 Raw 数据发布。");
            }

            return result;
        }
    }
}

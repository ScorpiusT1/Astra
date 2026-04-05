using Astra.Configuration;
using Astra.Core.Archiving;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Data;
using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Microsoft.Extensions.Logging;
using System.IO;
using Newtonsoft.Json;
using NVHDataBridge.Converters;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;

namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 默认归档服务：所有路径统一使用合并报告流程。
    /// <list type="bullet">
    ///   <item>批次模式（Home 多子流程）：共享目录，数据收集到 <see cref="ICombinedReportCollector"/>，由 Home 编排层统一落盘；各子流程 Raw 在内存中合并，批次结束时写入单个 TDMS（组 <c>Signal</c>，通道名 <c>设备+通道名</c>）。</item>
    ///   <item>非批次模式（单流程 / 非 Home 入口）：与批次同一套采集逻辑，即时写合并报告（1 段）；总线上全部 Raw 合并为单个 TDMS。</item>
    /// </list>
    /// </summary>
    public sealed class DefaultWorkflowArchiveService : IWorkflowArchiveService
    {
        private const string TestDataFolderName = "测试数据";

        private readonly WorkflowArchiveOptions _options;
        private readonly ILogger<DefaultWorkflowArchiveService> _logger;
        private readonly ITestReportGenerator _testReportGenerator;
        private readonly ICombinedReportCollector? _combinedReportCollector;
        private readonly IConfigurationManager? _configurationManager;
        private readonly IReportStationLineSource? _reportStationLineSource;

        private readonly ConcurrentDictionary<string, ArchiveSessionNaming> _sessionNamingByExecution = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _rawExportDone = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _runRecordExportDone = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, object> _sequenceLocksBySnFolder = new(StringComparer.Ordinal);

        private readonly object _batchRawMergeGate = new();
        private NvhMemoryFile? _batchMergedRaw;
        private HashSet<string>? _batchRawUsedChannelNames;

        /// <summary>保证批次内首次分配共享输出目录时互斥，避免多子流程并行归档各建一个序号文件夹。</summary>
        private readonly object _batchSharedDirectoryGate = new();

        private sealed class ArchiveSessionNaming
        {
            public required string OutputDirectory { get; init; }
            public required string FileTimeStamp { get; init; }
        }

        public DefaultWorkflowArchiveService(
            WorkflowArchiveOptions options,
            ILogger<DefaultWorkflowArchiveService> logger,
            ITestReportGenerator testReportGenerator,
            ICombinedReportCollector? combinedReportCollector = null,
            IConfigurationManager? configurationManager = null,
            IReportStationLineSource? reportStationLineSource = null)
        {
            _options = options ?? new WorkflowArchiveOptions();
            _logger = logger;
            _testReportGenerator = testReportGenerator;
            _combinedReportCollector = combinedReportCollector;
            _configurationManager = configurationManager;
            _reportStationLineSource = reportStationLineSource;
        }

        public Task<WorkflowArchiveResult> ArchiveAsync(WorkflowArchiveRequest request, CancellationToken cancellationToken)
        {
            return Task.Run(() => ArchiveCore(request, cancellationToken), cancellationToken);
        }

        /// <inheritdoc />
        public void OnBatchArchiveStarted()
        {
            lock (_batchRawMergeGate)
            {
                _batchMergedRaw = null;
                _batchRawUsedChannelNames = null;
            }
        }

        /// <inheritdoc />
        public void FlushBatchCombinedRawTdms(string outputDirectory, string combinedFilePrefix)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(combinedFilePrefix))
                return;

            NvhMemoryFile? merged;
            lock (_batchRawMergeGate)
            {
                merged = _batchMergedRaw;
                _batchMergedRaw = null;
                _batchRawUsedChannelNames = null;
            }

            if (merged == null)
                return;

            try
            {
                Directory.CreateDirectory(outputDirectory);
                var tdmsPath = Path.Combine(outputDirectory, $"{combinedFilePrefix}_raw.tdms");
                NvhTdmsConverter.SaveToTdms(merged, tdmsPath);
                ExportWavPerChannel(merged, outputDirectory, $"{combinedFilePrefix}_raw");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批次合并 Raw TDMS/WAV 写入失败");
            }
        }

        private WorkflowArchiveResult ArchiveCore(WorkflowArchiveRequest request, CancellationToken cancellationToken)
        {
            if (request?.NodeContext == null)
                return new WorkflowArchiveResult { Success = false, Message = "NodeContext 为空", Skipped = true };

            if (string.IsNullOrWhiteSpace(request.ExecutionId))
                return new WorkflowArchiveResult { Success = false, Message = "ExecutionId 为空", Skipped = true };

            var batchMode = _combinedReportCollector is { IsActive: true };

            EnsureSnOrGenerate(request.NodeContext);

            var id = request.ExecutionId;
            var session = batchMode
                ? ResolveOrCreateBatchSession(request)
                : _sessionNamingByExecution.GetOrAdd(id, _ => CreateSessionNaming(request));

            var outputDir = session.OutputDirectory;
            var fileStamp = session.FileTimeStamp;

            var sn = GetGlobalString(request.NodeContext, "SN") ?? "AUTO";
            var condition = GetGlobalString(request.NodeContext, "工况")
                            ?? GetGlobalString(request.NodeContext, "Condition")
                            ?? "Default";
            var okNg = ResolveOkNg(request);
            var (stationName, lineName) = _reportStationLineSource?.GetStationAndLine() ?? (string.Empty, string.Empty);
            var filePrefix = ReportArchiveFileNaming.BuildFilePrefix(sn, stationName, lineName, okNg, fileStamp);

            var wroteRaw = false;
            var wroteRecord = false;

            // ── 1. Raw 数据（TDMS/WAV）──────────────────────────
            var dataBus = request.NodeContext?.GetDataBus();
            if (_rawExportDone.TryAdd(id, 0))
            {
                wroteRaw = ExportRawArtifacts(dataBus, outputDir, filePrefix, batchMode, cancellationToken);
            }

            // ── 2. RunRecord + 报告数据采集 ───────────────────
            if (request.RunRecord != null && _runRecordExportDone.TryAdd(id, 0))
            {
                try
                {
                    var reportData = CollectReportData(request, dataBus, outputDir, filePrefix, stationName, lineName, cancellationToken);

                    if (batchMode)
                    {
                        var seq = reportData?.SectionSequenceOrder ?? GetReportSectionSequence(request.NodeContext);
                        _combinedReportCollector!.AddRunRecord(request.RunRecord, condition, seq);
                        if (reportData != null)
                            _combinedReportCollector.AddSection(reportData);
                    }
                    else
                    {
                        ImmediateWriteAll(
                            request.RunRecord, condition, reportData,
                            request.NodeContext!, outputDir, sn, filePrefix, cancellationToken);
                    }

                    wroteRecord = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "归档报告/记录失败");
                }
            }

            return new WorkflowArchiveResult
            {
                Success = true,
                Message = wroteRaw || wroteRecord ? "归档完成" : "本轮无可写入内容（可能已由他处写入）",
                OutputDirectory = outputDir,
                Skipped = !(wroteRaw || wroteRecord),
                WroteRawArtifacts = wroteRaw,
                WroteRunRecordArtifacts = wroteRecord
            };
        }

        /// <summary>
        /// 采集报告数据（含图表渲染），不写文件。
        /// </summary>
        private TestReportData? CollectReportData(
            WorkflowArchiveRequest request, ITestDataBus? dataBus,
            string outputDir, string filePrefix,
            string reportStationFromConfig, string reportLineFromConfig,
            CancellationToken ct)
        {
            var result = _testReportGenerator.GenerateAsync(new TestReportRequest
            {
                ArchiveRequest = request,
                DataBus = dataBus,
                OutputDirectory = outputDir,
                FilePrefix = filePrefix,
                ExportChartFiles = false,
                Formats = ReportExportFormats.Html | ReportExportFormats.Pdf,
                IncludeRawDataCharts = request.ReportOptions?.IncludeRawDataCharts ?? true,
                ReportOptions = request.ReportOptions,
                ReportStationFromConfig = string.IsNullOrWhiteSpace(reportStationFromConfig) ? null : reportStationFromConfig,
                ReportLineFromConfig = string.IsNullOrWhiteSpace(reportLineFromConfig) ? null : reportLineFromConfig
            }, ct).GetAwaiter().GetResult();

            return result.ReportData;
        }

        /// <summary>
        /// 非批次（单次执行）：即时写合并报告（1 段）、合并 RunRecord。
        /// </summary>
        private void ImmediateWriteAll(
            WorkFlowRunRecord runRecord, string condition,
            TestReportData? reportData, NodeContext context,
            string outputDir, string sn, string filePrefix,
            CancellationToken ct)
        {
            WriteCombinedRunRecordJson(
                [(runRecord, condition)],
                sn, outputDir, filePrefix);

            if (reportData == null) return;

            _testReportGenerator.GenerateCombinedAsync(new CombinedTestReportRequest
            {
                Sections = [reportData],
                OutputDirectory = outputDir,
                FilePrefix = filePrefix,
                Formats = ReportExportFormats.Html | ReportExportFormats.Pdf
            }, ct).GetAwaiter().GetResult();
        }

        // ──────────────────────────────────────────────────────
        // 共享静态 helper（供 HomeWorkflowExecutionService 复用）
        // ──────────────────────────────────────────────────────

        internal static void WriteCombinedRunRecordJson(
            IReadOnlyList<(WorkFlowRunRecord Record, string Condition)> records,
            string sn, string outputDir, string filePrefix)
        {
            if (records.Count == 0) return;

            var combined = new
            {
                SN = sn,
                StartTime = records.Min(r => r.Record.StartTime),
                EndTime = records.Max(r => r.Record.EndTime),
                OverallResult = records.All(r => r.Record.FinalResult?.Success == true) ? "OK" : "NG",
                SubWorkflows = records.Select(r => new
                {
                    r.Condition,
                    r.Record.WorkFlowName,
                    r.Record.Strategy,
                    r.Record.StartTime,
                    r.Record.EndTime,
                    r.Record.Status,
                    FinalResult = r.Record.FinalResult?.Success == true ? "OK" : "NG",
                    r.Record.NodeRuns
                }).ToList()
            };

            var json = JsonConvert.SerializeObject(combined, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });
            var path = Path.Combine(outputDir, $"{filePrefix}_run_record.json");
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        // ──────────────────────────────────────────────────────
        // 内部方法
        // ──────────────────────────────────────────────────────

        private bool ExportRawArtifacts(ITestDataBus? dataBus, string outputDir, string filePrefix, bool batchMode, CancellationToken ct)
        {
            var rawRefs = dataBus?.Query(DataArtifactCategory.Raw)
                          ?? Array.Empty<DataArtifactReference>();

            var items = new List<(NvhMemoryFile File, DataArtifactReference Meta)>();
            foreach (var rawRef in rawRefs)
            {
                ct.ThrowIfCancellationRequested();
                if (dataBus == null || !dataBus.TryGet<NvhMemoryFile>(rawRef.Key, out var nvh) || nvh == null)
                    continue;
                items.Add((nvh, rawRef));
            }

            if (items.Count == 0)
                return false;

            try
            {
                if (batchMode)
                {
                    lock (_batchRawMergeGate)
                    {
                        _batchMergedRaw ??= new NvhMemoryFile();
                        _batchRawUsedChannelNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (nvh, meta) in items)
                        {
                            ct.ThrowIfCancellationRequested();
                            var dev = ResolveRawArchiveDeviceDisplayName(meta);
                            NvhMemoryFile.AppendAllChannelsRenamed(
                                _batchMergedRaw,
                                NvhArchiveSampleUtil.DefaultSignalGroupName,
                                nvh,
                                ch => SanitizeArchiveChannelName($"{dev}+{ch.Name}"),
                                _batchRawUsedChannelNames);
                        }
                    }

                    return true;
                }

                var merged = new NvhMemoryFile();
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (nvh, meta) in items)
                {
                    ct.ThrowIfCancellationRequested();
                    var dev = ResolveRawArchiveDeviceDisplayName(meta);
                    NvhMemoryFile.AppendAllChannelsRenamed(
                        merged,
                        NvhArchiveSampleUtil.DefaultSignalGroupName,
                        nvh,
                        ch => SanitizeArchiveChannelName($"{dev}+{ch.Name}"),
                        used);
                }

                Directory.CreateDirectory(outputDir);
                NvhTdmsConverter.SaveToTdms(merged, Path.Combine(outputDir, $"{filePrefix}_raw.tdms"));
                ExportWavPerChannel(merged, outputDir, $"{filePrefix}_raw");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "归档合并 Raw TDMS/WAV 失败");
                return false;
            }
        }

        /// <summary>从 Raw 产物引用解析设备显示名（与采集/导入 DisplayName、Preview 对齐）。</summary>
        private static string ResolveRawArchiveDeviceDisplayName(DataArtifactReference r)
        {
            var dn = r.DisplayName?.Trim() ?? string.Empty;
            foreach (var suf in new[] { "-RawData", "-Raw", "-Filtered" })
            {
                if (dn.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    var head = dn[..^suf.Length].Trim();
                    if (head.Length > 0)
                        return head;
                    break;
                }
            }

            if (r.Preview != null && r.Preview.TryGetValue("DeviceId", out var did) && did != null)
            {
                var id = Convert.ToString(did, CultureInfo.InvariantCulture)?.Trim();
                if (!string.IsNullOrEmpty(id))
                    return id;
            }

            return string.IsNullOrEmpty(dn) ? "Device" : dn;
        }

        private static string SanitizeArchiveChannelName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ch";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalid.Contains(chars[i]) || chars[i] == '\\')
                    chars[i] = '_';
            }

            var s = new string(chars).Trim();
            return s.Length > 200 ? s[..200] : s;
        }

        private ArchiveSessionNaming ResolveOrCreateBatchSession(WorkflowArchiveRequest request)
        {
            var existing = _combinedReportCollector!.SharedOutputDirectory;
            if (existing != null)
            {
                return new ArchiveSessionNaming
                {
                    OutputDirectory = existing,
                    FileTimeStamp = _combinedReportCollector.SharedFileTimestamp
                                    ?? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                };
            }

            lock (_batchSharedDirectoryGate)
            {
                existing = _combinedReportCollector.SharedOutputDirectory;
                if (existing != null)
                {
                    return new ArchiveSessionNaming
                    {
                        OutputDirectory = existing,
                        FileTimeStamp = _combinedReportCollector.SharedFileTimestamp
                                        ?? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    };
                }

                var session = CreateSessionNaming(request);
                _combinedReportCollector.SetSharedOutputDirectory(session.OutputDirectory, session.FileTimeStamp);
                return session;
            }
        }

        private ArchiveSessionNaming CreateSessionNaming(WorkflowArchiveRequest request)
        {
            var root = ResolveEffectiveReportRootDirectory();
            Directory.CreateDirectory(root);

            var dateFolder = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var dataRoot = Path.Combine(root, TestDataFolderName, dateFolder);
            Directory.CreateDirectory(dataRoot);

            var sn = GetGlobalString(request.NodeContext, "SN") ?? "AUTO";
            var snFolder = ReportArchiveFileNaming.SanitizeFileSegment(sn);
            var snBasePath = Path.Combine(dataRoot, snFolder);
            Directory.CreateDirectory(snBasePath);

            var lockKey = $"{dateFolder}|{snFolder}";
            var gate = _sequenceLocksBySnFolder.GetOrAdd(lockKey, _ => new object());
            string outputPath;
            lock (gate)
            {
                var nextIndex = GetNextRunIndex(snBasePath);
                outputPath = Path.Combine(snBasePath, nextIndex.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(outputPath);
            }

            return new ArchiveSessionNaming
            {
                OutputDirectory = outputPath,
                FileTimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            };
        }

        /// <summary>
        /// 与 <see cref="SoftwareConfig.ReportOutputRootDirectory"/> 对齐；留空则为程序所在磁盘（卷）根目录。
        /// 无配置管理器时使用 <see cref="WorkflowArchiveOptions.RootDirectory"/>，仍为空则同上默认根目录。
        /// </summary>
        private string ResolveEffectiveReportRootDirectory()
        {
            if (_configurationManager != null)
            {
                try
                {
                    var result = _configurationManager.GetAllAsync<SoftwareConfig>().GetAwaiter().GetResult();
                    if (result.Success && result.Data != null)
                    {
                        var sc = result.Data.FirstOrDefault();
                        var custom = sc?.ReportOutputRootDirectory?.Trim();
                        if (!string.IsNullOrWhiteSpace(custom))
                        {
                            try
                            {
                                return Path.GetFullPath(custom);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "报告根目录无效，将使用程序所在磁盘根目录: {Path}", custom);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取软件配置中的报告根目录失败，使用程序所在磁盘根目录。");
                }

                return PathHelper.GetReportDefaultRootDirectory();
            }

            var fallback = string.IsNullOrWhiteSpace(_options.RootDirectory)
                ? PathHelper.GetReportDefaultRootDirectory()
                : _options.RootDirectory.Trim();
            try
            {
                return Path.GetFullPath(fallback);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WorkflowArchiveOptions.RootDirectory 无效，使用程序所在磁盘根目录。");
                return PathHelper.GetReportDefaultRootDirectory();
            }
        }

        private static int GetNextRunIndex(string snRunRoot)
        {
            if (!Directory.Exists(snRunRoot)) return 1;

            var max = 0;
            foreach (var dir in Directory.GetDirectories(snRunRoot))
            {
                var name = Path.GetFileName(dir);
                if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > max)
                    max = n;
            }

            return max + 1;
        }

        private static string ResolveOkNg(WorkflowArchiveRequest request)
        {
            if (request.RunRecord?.FinalResult != null)
                return request.RunRecord.FinalResult.Success ? "OK" : "NG";

            return request.ExecutionStatus switch
            {
                WorkFlowExecutionStatus.Completed => "OK",
                WorkFlowExecutionStatus.Failed or WorkFlowExecutionStatus.Cancelled => "NG",
                _ => "UNK"
            };
        }

        private static string? GetGlobalString(NodeContext ctx, string key)
        {
            if (ctx.GlobalVariables.TryGetValue(key, out var v) && v != null)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            return null;
        }

        private static int GetReportSectionSequence(NodeContext? ctx)
        {
            if (ctx?.GlobalVariables == null) return 0;
            if (!ctx.GlobalVariables.TryGetValue(ReportContextKeys.SectionSequenceOrder, out var v) || v == null)
                return 0;
            try
            {
                return Convert.ToInt32(v, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private void EnsureSnOrGenerate(NodeContext ctx)
        {
            if (!string.IsNullOrWhiteSpace(GetGlobalString(ctx, "SN"))) return;

            var suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];
            var generated = $"AUTO_{DateTime.Now:yyyyMMdd_HHmmss}_{suffix}";
            ctx.SetGlobalVariable("SN", generated);
            _logger.LogInformation("未提供 SN，已自动生成: {SN}", generated);
        }

        private static void ExportWavPerChannel(NvhMemoryFile file, string outputDir, string baseName)
        {
            if (!file.TryGetGroup(NvhArchiveSampleUtil.DefaultSignalGroupName, out var group) || group == null)
                group = file.Groups.Values.FirstOrDefault();
            if (group == null) return;

            if (!NvhArchiveSampleUtil.TryGetFirstChannelSampleRateHz(file, NvhArchiveSampleUtil.DefaultSignalGroupName, out var rate) || rate <= 0)
                rate = 48000;

            foreach (var kv in group.Channels)
            {
                var chName = kv.Key;
                var sensorType = kv.Value.Properties.Get<string>("SensorType", string.Empty);
                if (string.IsNullOrWhiteSpace(sensorType))
                    continue;

                if (!NvhArchiveSampleUtil.TryExtractAsDoubleArray(file, NvhArchiveSampleUtil.DefaultSignalGroupName, chName, out var samples) || samples.Length == 0)
                    continue;

                var floats = new float[samples.Length];
                for (var i = 0; i < samples.Length; i++)
                    floats[i] = (float)samples[i];

                using var w = new WavWriter(Path.Combine(outputDir, $"{baseName}_{ReportArchiveFileNaming.SanitizeFileSegment(chName)}.wav"), rate, 1, 32);
                w.WriteSamples(floats);
            }
        }

    }
}

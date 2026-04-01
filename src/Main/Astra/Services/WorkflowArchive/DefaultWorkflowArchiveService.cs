using Astra.Core.Archiving;
using Astra.Core.Data;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.Services.Reporting;
using Microsoft.Extensions.Logging;
using System.IO;
using Newtonsoft.Json;
using NVHDataBridge.Converters;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Linq;

namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 默认归档：目录为 根目录/SN/序号(1,2,3…)/；Raw 快照写 TDMS/WAV，全局变量写 CSV，结果链写 JSON 与 HTML（便于浏览器打开或再转 PDF）。
    /// </summary>
    public sealed class DefaultWorkflowArchiveService : IWorkflowArchiveService
    {
        private readonly WorkflowArchiveOptions _options;
        private readonly ILogger<DefaultWorkflowArchiveService> _logger;

        private readonly ConcurrentDictionary<string, ArchiveSessionNaming> _sessionNamingByExecution = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _rawExportDone = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _runRecordExportDone = new(StringComparer.Ordinal);

        /// <summary>按 SN 目录名分配序号（1、2、3…）时的互斥，避免同一 SN 并行测试拿到相同序号。</summary>
        private readonly ConcurrentDictionary<string, object> _sequenceLocksBySnFolder = new(StringComparer.Ordinal);

        private sealed class ArchiveSessionNaming
        {
            public required string OutputDirectory { get; init; }

            /// <summary>yyyyMMdd_HHmmss，同一次执行内固定。</summary>
            public required string FileTimeStamp { get; init; }
        }

        public DefaultWorkflowArchiveService(
            WorkflowArchiveOptions options,
            ILogger<DefaultWorkflowArchiveService> logger)
        {
            _options = options ?? new WorkflowArchiveOptions();
            _logger = logger;
        }

        public Task<WorkflowArchiveResult> ArchiveAsync(WorkflowArchiveRequest request, CancellationToken cancellationToken)
        {
            return Task.Run(() => ArchiveCore(request, cancellationToken), cancellationToken);
        }

        private WorkflowArchiveResult ArchiveCore(WorkflowArchiveRequest request, CancellationToken cancellationToken)
        {
            if (request?.NodeContext == null)
            {
                return new WorkflowArchiveResult
                {
                    Success = false,
                    Message = "NodeContext 为空",
                    Skipped = true
                };
            }

            if (string.IsNullOrWhiteSpace(request.ExecutionId))
            {
                return new WorkflowArchiveResult { Success = false, Message = "ExecutionId 为空", Skipped = true };
            }

            EnsureSnOrGenerate(request.NodeContext);

            var id = request.ExecutionId;
            var session = _sessionNamingByExecution.GetOrAdd(id, _ => CreateSessionNaming(request));
            var outputDir = session.OutputDirectory;
            var fileStamp = session.FileTimeStamp;

            var sn = GetGlobalString(request.NodeContext, "SN") ?? "AUTO";
            var condition = GetGlobalString(request.NodeContext, "工况")
                            ?? GetGlobalString(request.NodeContext, "Condition")
                            ?? "Default";
            var okNg = ResolveOkNg(request);
            var filePrefix = BuildFileNamePrefix(sn, condition, okNg, fileStamp);

            var wroteRaw = false;
            var wroteRecord = false;

            var dataBus = request.NodeContext?.GetDataBus();
            if (_rawExportDone.TryAdd(id, 0))
            {
                var rawRefs = dataBus?.Query(Astra.Core.Nodes.Models.DataArtifactCategory.Raw)
                              ?? Array.Empty<Astra.Core.Nodes.Models.DataArtifactReference>();

                var index = 0;
                foreach (var rawRef in rawRefs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (dataBus == null || !dataBus.TryGet<NvhMemoryFile>(rawRef.Key, out var nvh) || nvh == null)
                    {
                        continue;
                    }

                    var artifactTag = $"{filePrefix}_raw{index}_{SanitizeFileSegment(rawRef.Key)}";
                    try
                    {
                        var tdmsPath = Path.Combine(outputDir, artifactTag + ".tdms");
                        NvhTdmsConverter.SaveToTdms(nvh, tdmsPath);
                        ExportWavPerChannel(nvh, outputDir, artifactTag);
                        wroteRaw = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "归档 TDMS/WAV 失败: {Key}", rawRef.Key);
                    }

                    index++;
                }

                try
                {
                    WriteGlobalVariablesCsv(request.NodeContext, Path.Combine(outputDir, $"{filePrefix}_global_variables.csv"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "写入 global_variables.csv 失败");
                }
            }

            if (request.RunRecord != null && _runRecordExportDone.TryAdd(id, 0))
            {
                try
                {
                    WriteRunRecordJson(request.RunRecord, Path.Combine(outputDir, $"{filePrefix}_run_record.json"));

                    var reportGen = new DefaultTestReportGenerator();
                    var reportResult = reportGen.GenerateAsync(new TestReportRequest
                    {
                        ArchiveRequest = request,
                        DataBus = request.NodeContext?.GetDataBus(),
                        OutputDirectory = outputDir,
                        FilePrefix = filePrefix,
                        ExportChartFiles = true
                    }, cancellationToken).GetAwaiter().GetResult();

                    if (!reportResult.Success)
                    {
                        WriteSummaryHtml(request, Path.Combine(outputDir, $"{filePrefix}_report.html"));
                    }

                    wroteRecord = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "写入结果链报告失败");
                }
            }

            var any = wroteRaw || wroteRecord;
            return new WorkflowArchiveResult
            {
                Success = true,
                Message = any ? "归档完成" : "本轮无可写入内容（可能已由他处写入）",
                OutputDirectory = outputDir,
                Skipped = !any,
                WroteRawArtifacts = wroteRaw,
                WroteRunRecordArtifacts = wroteRecord
            };
        }

        /// <summary>
        /// 目录：根目录 / SN / 序号（同 SN 第 1 次为 1，第 2 次为 2，依此类推）。同一次执行的时间戳在会话内固定。
        /// </summary>
        private ArchiveSessionNaming CreateSessionNaming(WorkflowArchiveRequest request)
        {
            var root = string.IsNullOrWhiteSpace(_options.RootDirectory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestResults")
                : _options.RootDirectory;

            Directory.CreateDirectory(root);

            var sn = GetGlobalString(request.NodeContext, "SN") ?? "AUTO";
            var snFolder = SanitizeFileSegment(sn);
            var gate = _sequenceLocksBySnFolder.GetOrAdd(snFolder, _ => new object());
            string outputPath;
            lock (gate)
            {
                var nextIndex = GetNextRunIndex(root, snFolder);
                outputPath = Path.Combine(root, snFolder, nextIndex.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(outputPath);
            }

            var fileStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return new ArchiveSessionNaming
            {
                OutputDirectory = outputPath,
                FileTimeStamp = fileStamp
            };
        }

        /// <summary>
        /// 在「根/SN/」下扫描纯数字子目录名，返回 max+1；无子目录或不存在 SN 目录时为 1。
        /// </summary>
        private static int GetNextRunIndex(string root, string snFolderName)
        {
            var snPath = Path.Combine(root, snFolderName);
            if (!Directory.Exists(snPath))
            {
                return 1;
            }

            var max = 0;
            foreach (var dir in Directory.GetDirectories(snPath))
            {
                var name = Path.GetFileName(dir);
                if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 && n > max)
                {
                    max = n;
                }
            }

            return max + 1;
        }

        /// <summary>
        /// 文件名前缀：SN + 工况 + OK/NG（尚无最终结果时为 UNK）+ 时间。
        /// </summary>
        private static string BuildFileNamePrefix(string sn, string condition, string okNg, string fileStamp)
        {
            return $"{SanitizeFileSegment(sn)}_{SanitizeFileSegment(condition)}_{SanitizeFileSegment(okNg)}_{SanitizeFileSegment(fileStamp)}";
        }

        /// <summary>
        /// 优先依据结果链 FinalResult；否则依据引擎传入的执行状态。流程内提前归档时尚无结果则为 UNK。
        /// </summary>
        private static string ResolveOkNg(WorkflowArchiveRequest request)
        {
            if (request.RunRecord?.FinalResult != null)
            {
                return request.RunRecord.FinalResult.Success ? "OK" : "NG";
            }

            if (request.ExecutionStatus.HasValue)
            {
                return request.ExecutionStatus.Value switch
                {
                    WorkFlowExecutionStatus.Completed => "OK",
                    WorkFlowExecutionStatus.Failed => "NG",
                    WorkFlowExecutionStatus.Cancelled => "NG",
                    _ => "UNK"
                };
            }

            return "UNK";
        }

        private static string? GetGlobalString(NodeContext ctx, string key)
        {
            if (ctx.GlobalVariables.TryGetValue(key, out var v) && v != null)
            {
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            }

            return null;
        }

        /// <summary>
        /// 未设置或仅为空白时生成 SN（AUTO_时间_短随机码），并写回全局变量，便于目录与后续节点一致。
        /// </summary>
        private void EnsureSnOrGenerate(NodeContext ctx)
        {
            var raw = GetGlobalString(ctx, "SN");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];
            var generated = $"AUTO_{DateTime.Now:yyyyMMdd_HHmmss}_{suffix}";
            ctx.SetGlobalVariable("SN", generated);
            _logger.LogInformation("未提供 SN，已自动生成: {SN}", generated);
        }

        private static void ExportWavPerChannel(NvhMemoryFile file, string outputDir, string baseName)
        {
            if (!file.TryGetGroup(NvhArchiveSampleUtil.DefaultSignalGroupName, out var group) || group == null)
            {
                group = file.Groups.Values.FirstOrDefault();
            }

            if (group == null)
            {
                return;
            }

            if (!NvhArchiveSampleUtil.TryGetFirstChannelSampleRateHz(file, NvhArchiveSampleUtil.DefaultSignalGroupName, out var rate) ||
                rate <= 0)
            {
                rate = 48000;
            }

            foreach (var chName in group.Channels.Keys)
            {
                if (!NvhArchiveSampleUtil.TryExtractAsDoubleArray(
                        file,
                        NvhArchiveSampleUtil.DefaultSignalGroupName,
                        chName,
                        out var samples) ||
                    samples.Length == 0)
                {
                    continue;
                }

                var wavName = $"{baseName}_{SanitizeFileSegment(chName)}.wav";
                var wavPath = Path.Combine(outputDir, wavName);
                var floats = new float[samples.Length];
                for (var i = 0; i < samples.Length; i++)
                {
                    floats[i] = (float)samples[i];
                }

                using var w = new WavWriter(wavPath, rate, 1, 32);
                w.WriteSamples(floats);
            }
        }

        private static void WriteGlobalVariablesCsv(NodeContext ctx, string path)
        {
            using var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            sw.WriteLine("Key,Value");
            foreach (var kv in ctx.GlobalVariables.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var v = kv.Value == null ? string.Empty : Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                sw.WriteLine($"{EscapeCsv(kv.Key)},{EscapeCsv(v)}");
            }
        }

        private static string EscapeCsv(string s)
        {
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }

            return s;
        }

        private static void WriteRunRecordJson(WorkFlowRunRecord record, string path)
        {
            var json = JsonConvert.SerializeObject(
                record,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                });
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void WriteSummaryHtml(WorkflowArchiveRequest request, string path)
        {
            var rr = request.RunRecord;
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>测试报告</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Microsoft YaHei,sans-serif;margin:24px;}table{border-collapse:collapse;}td,th{border:1px solid #ccc;padding:6px 10px;}th{background:#f0f0f0;}</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h1>{WebUtility.HtmlEncode(rr?.WorkFlowName ?? request.WorkFlowName)}</h1>");
            sb.AppendLine($"<p>执行ID: {WebUtility.HtmlEncode(request.ExecutionId)}</p>");
            sb.AppendLine($"<p>状态: {WebUtility.HtmlEncode(rr?.Status ?? request.ExecutionStatus?.ToString() ?? "")}</p>");
            sb.AppendLine($"<p>触发: {WebUtility.HtmlEncode(request.Trigger.ToString())}</p>");
            sb.AppendLine("<h2>节点输出摘要</h2><table><tr><th>节点</th><th>状态</th><th>消息</th></tr>");
            if (rr?.NodeRuns != null)
            {
                foreach (var n in rr.NodeRuns)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{WebUtility.HtmlEncode(n.NodeName ?? n.NodeId)}</td>");
                    sb.AppendLine($"<td>{WebUtility.HtmlEncode(n.State.ToString())}</td>");
                    sb.AppendLine($"<td>{WebUtility.HtmlEncode(n.Message ?? "")}</td>");
                    sb.AppendLine("</tr>");
                }
            }

            sb.AppendLine("</table>");
            sb.AppendLine("<p><small>单值明细见 run_record.json；曲线可后续由图表引擎导出为 PNG 嵌入。</small></p>");
            sb.AppendLine("</body></html>");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static string SanitizeFileSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "x";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalid.Contains(chars[i]) || chars[i] == ':' || chars[i] == '\\' || chars[i] == '/')
                {
                    chars[i] = '_';
                }
            }

            var s = new string(chars).Trim();
            return s.Length > 96 ? s[..96] : s;
        }
    }
}

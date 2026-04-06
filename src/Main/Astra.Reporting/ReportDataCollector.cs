using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;

namespace Astra.Reporting
{
    /// <summary>
    /// 报告数据采集器：从 <see cref="WorkFlowRunRecord"/>、<see cref="ITestDataBus"/> 与可选 <see cref="NodeContext"/>
    /// 汇总单次测试报告所需的元数据、单值判定、曲线判定，以及算法图/原始数据图条目（含子图展开为多节）。
    /// </summary>
    public static class ReportDataCollector
    {
        /// <summary>
        /// 根据运行记录与数据总线构建完整的 <see cref="TestReportData"/>。
        /// </summary>
        /// <param name="runRecord">工作流执行记录（节点运行、起止时间、总体结果等）。</param>
        /// <param name="dataBus">测试数据总线；为 null 时仅填充运行记录与上下文中的非图表字段。</param>
        /// <param name="context">节点上下文，用于读取全局变量（SN、工位、线体、工况、分节顺序等）。</param>
        /// <param name="options">
        /// 生成选项：为 null 时默认包含算法图与原始数据图，且不对节点/产物键做白名单过滤。
        /// 各筛选字段为逗号/分号/换行分隔的列表字符串。
        /// </param>
        /// <param name="reportStationFromConfig">软件配置工站名；非空时优先于上下文全局变量。</param>
        /// <param name="reportLineFromConfig">软件配置线体名；非空时优先于上下文全局变量。</param>
        /// <returns>可供 HTML/PDF 与图表渲染使用的报告数据模型。</returns>
        public static TestReportData Collect(
            WorkFlowRunRecord runRecord,
            ITestDataBus? dataBus,
            NodeContext? context,
            ReportGenerationOptions? options,
            string? reportStationFromConfig = null,
            string? reportLineFromConfig = null)
        {
            var includeAlg = options?.IncludeAlgorithmCharts ?? true;
            var includeRaw = options?.IncludeRawDataCharts ?? true;

            var scalarIds = ParseNodeIdSet(options?.ScalarNodeIdsFilter);
            var curveIds = ParseNodeIdSet(options?.CurveNodeIdsFilter);
            var chartProducerIds = ParseNodeIdSet(options?.ChartProducerNodeIdsFilter);
            var chartKeys = ParseArtifactKeySet(options?.ChartArtifactKeysFilter);

            var data = new TestReportData
            {
                ExecutionId = runRecord.ExecutionId ?? string.Empty,
                WorkFlowName = runRecord.WorkFlowName ?? string.Empty,
                SN = GetGlobalString(context, "SN") ?? "N/A",
                TestStation = PickStation(reportStationFromConfig, context),
                TestLine = PickLine(reportLineFromConfig, context),
                Condition = GetGlobalString(context, "工况")
                            ?? GetGlobalString(context, "Condition")
                            ?? "Default",
                StartTime = runRecord.StartTime,
                EndTime = runRecord.EndTime,
                OverallResult = runRecord.FinalResult?.Success == true ? "OK" : "NG",
                Strategy = runRecord.Strategy ?? string.Empty
            };

            if (runRecord.NodeRuns != null)
            {
                foreach (var nr in OrderNodeRunsByWorkflowTree(runRecord.NodeRuns, context))
                {
                    ExtractScalarJudgments(nr, data.ScalarJudgments, scalarIds);
                    ExtractCurveJudgments(nr, data, curveIds);
                }
            }

            if (dataBus != null)
            {
                if (includeAlg)
                    CollectAlgorithmCharts(dataBus, data, chartProducerIds, chartKeys);
                if (includeRaw)
                    CollectRawCharts(dataBus, data, chartProducerIds, chartKeys);
            }

            // 曲线判定附图已单独渲染；同一总线键不应再出现在「算法与数据图表」中，避免与用户「仅纳入报告」逻辑下的重复展示。
            ExcludeChartSectionsDuplicatedAsCurveAttachments(data);

            StableSortChartsByReportLayer(data.Charts);

            data.SectionSequenceOrder = GetReportSectionSequence(context);
            return data;
        }

        /// <summary>
        /// 报告输出顺序：原始数据图表 → 算法数据图表 → 曲线数据；同层内保持采集时的相对顺序。
        /// </summary>
        private static void StableSortChartsByReportLayer(List<ChartSection> charts)
        {
            if (charts.Count <= 1)
                return;

            static int Rank(ReportChartSourceKind k) =>
                k switch
                {
                    ReportChartSourceKind.Raw => 0,
                    ReportChartSourceKind.Algorithm => 1,
                    ReportChartSourceKind.CurveResult => 2,
                    _ => 1
                };

            var indexed = new List<(ChartSection Chart, int Index)>(charts.Count);
            for (var i = 0; i < charts.Count; i++)
                indexed.Add((charts[i], i));

            indexed.Sort((a, b) =>
            {
                var cmp = Rank(a.Chart.SourceKind).CompareTo(Rank(b.Chart.SourceKind));
                return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
            });

            charts.Clear();
            foreach (var (c, _) in indexed)
                charts.Add(c);
        }

        /// <summary>
        /// 从图库中移除与某条曲线判定行 <see cref="CurveJudgmentRow.PreferredChartArtifactKey"/> 相同的节（含子图拆条共用同一 ArtifactKey 的情况）。
        /// </summary>
        private static void ExcludeChartSectionsDuplicatedAsCurveAttachments(TestReportData data)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cj in data.CurveJudgments)
            {
                var k = cj.PreferredChartArtifactKey;
                if (!string.IsNullOrEmpty(k))
                    keys.Add(k);
            }

            if (keys.Count == 0)
                return;

            data.Charts.RemoveAll(c => !string.IsNullOrEmpty(c.ArtifactKey) && keys.Contains(c.ArtifactKey));
        }

        /// <summary>
        /// 从算法类产物预览 <see cref="ReportArtifactPreviewKeys.ChartReportSourceKind"/> 解析报告分层；缺省为 <see cref="ReportChartSourceKind.Algorithm"/>。
        /// </summary>
        private static ReportChartSourceKind ResolveAlgorithmChartSourceKind(DataArtifactReference r)
        {
            var s = GetPreviewString(r, ReportArtifactPreviewKeys.ChartReportSourceKind);
            if (string.Equals(s, nameof(ReportChartSourceKind.CurveResult), StringComparison.OrdinalIgnoreCase))
                return ReportChartSourceKind.CurveResult;
            if (string.Equals(s, nameof(ReportChartSourceKind.Raw), StringComparison.OrdinalIgnoreCase))
                return ReportChartSourceKind.Raw;
            return ReportChartSourceKind.Algorithm;
        }

        /// <summary>
        /// 从数据总线收集算法类图表：仅包含可解析为 <see cref="ChartDisplayPayload"/> 且通过包含键与白名单过滤的条目；子图布局时按系列拆成多个 <see cref="ChartSection"/>。
        /// 分层由 <see cref="ResolveAlgorithmChartSourceKind"/> 决定（原始数据图表 / 算法数据图表 / 曲线数据）。
        /// 预览标记为 <see cref="ReportChartSourceKind.Raw"/> 的算法产物（如文件导入预览）不在此收录，避免与 <see cref="CollectRawCharts"/> 中同类 Raw 数据重复。
        /// </summary>
        private static void CollectAlgorithmCharts(
            ITestDataBus dataBus,
            TestReportData data,
            HashSet<string>? producerWhitelist,
            HashSet<string>? artifactKeyWhitelist)
        {
            var algorithmRefs = dataBus.Query(DataArtifactCategory.Algorithm);
            foreach (var algRef in algorithmRefs)
            {
                if (!dataBus.TryGet<ChartDisplayPayload>(algRef.Key, out var payload) || payload == null)
                    continue;

                if (!ChartArtifactMatches(algRef, producerWhitelist, artifactKeyWhitelist))
                    continue;

                var algoLayer = ResolveAlgorithmChartSourceKind(algRef);
                if (algoLayer == ReportChartSourceKind.Raw)
                    continue;
                var baseTitle = algRef.DisplayName ?? "算法图表";

                // 多系列一律拆成多张报告图，不再保留「所有通道叠在一张图」的单节（含 SinglePlot 多 Series）。
                if (payload.Series is { Count: > 1 })
                {
                    for (var i = 0; i < payload.Series.Count; i++)
                    {
                        var ser = payload.Series[i];
                        var part = string.IsNullOrWhiteSpace(ser.Name) ? $"系列 {i + 1}" : ser.Name.Trim();
                        var algoLabel = $"{baseTitle} — {part}";
                        data.Charts.Add(new ChartSection
                        {
                            Title = algoLabel,
                            NodeName = GetProducerNodeId(algRef),
                            Description = algRef.Description,
                            ArtifactKey = algRef.Key,
                            SubPlotSeriesIndex = i,
                            SourceKind = algoLayer,
                            ReportHeading = BuildArtifactChartReportHeading(data, algRef, algoLabel)
                        });
                    }
                }
                else if (payload.Series is { Count: 1 })
                {
                    var ser = payload.Series[0];
                    var part = string.IsNullOrWhiteSpace(ser.Name) ? "系列 1" : ser.Name.Trim();
                    var algoLabel = $"{baseTitle} — {part}";
                    data.Charts.Add(new ChartSection
                    {
                        Title = algoLabel,
                        NodeName = GetProducerNodeId(algRef),
                        Description = algRef.Description,
                        ArtifactKey = algRef.Key,
                        SubPlotSeriesIndex = 0,
                        SourceKind = algoLayer,
                        ReportHeading = BuildArtifactChartReportHeading(data, algRef, algoLabel)
                    });
                }
                else
                {
                    data.Charts.Add(new ChartSection
                    {
                        Title = baseTitle,
                        NodeName = GetProducerNodeId(algRef),
                        Description = algRef.Description,
                        ArtifactKey = algRef.Key,
                        SourceKind = algoLayer,
                        ReportHeading = BuildArtifactChartReportHeading(data, algRef, baseTitle)
                    });
                }
            }
        }

        /// <summary>
        /// 从数据总线收集原始数据类图表：<see cref="ChartDisplayPayload"/> 与采集/导入常见的 <see cref="NvhMemoryFile"/> Raw；标题前缀为「原始数据图表」。
        /// </summary>
        private static void CollectRawCharts(
            ITestDataBus dataBus,
            TestReportData data,
            HashSet<string>? producerWhitelist,
            HashSet<string>? artifactKeyWhitelist)
        {
            var rawRefs = dataBus.Query(DataArtifactCategory.Raw);
            foreach (var rawRef in rawRefs)
            {
                if (!ChartArtifactMatches(rawRef, producerWhitelist, artifactKeyWhitelist))
                    continue;

                if (dataBus.TryGet<ChartDisplayPayload>(rawRef.Key, out var payload) && payload != null)
                {
                    var rawBase = rawRef.DisplayName ?? "Raw";

                    if (payload.Series is { Count: > 1 })
                    {
                        for (var i = 0; i < payload.Series.Count; i++)
                        {
                            var ser = payload.Series[i];
                            var part = string.IsNullOrWhiteSpace(ser.Name) ? $"系列 {i + 1}" : ser.Name.Trim();
                            var rawAlgo = $"原始数据图表: {rawBase} — {part}";
                            data.Charts.Add(new ChartSection
                            {
                                Title = rawAlgo,
                                NodeName = GetProducerNodeId(rawRef),
                                ArtifactKey = rawRef.Key,
                                SubPlotSeriesIndex = i,
                                SourceKind = ReportChartSourceKind.Raw,
                                ReportHeading = BuildArtifactChartReportHeading(data, rawRef, rawAlgo)
                            });
                        }
                    }
                    else if (payload.Series is { Count: 1 })
                    {
                        var ser = payload.Series[0];
                        var part = string.IsNullOrWhiteSpace(ser.Name) ? "系列 1" : ser.Name.Trim();
                        var rawAlgo = $"原始数据图表: {rawBase} — {part}";
                        data.Charts.Add(new ChartSection
                        {
                            Title = rawAlgo,
                            NodeName = GetProducerNodeId(rawRef),
                            ArtifactKey = rawRef.Key,
                            SubPlotSeriesIndex = 0,
                            SourceKind = ReportChartSourceKind.Raw,
                            ReportHeading = BuildArtifactChartReportHeading(data, rawRef, rawAlgo)
                        });
                    }
                    else
                    {
                        var rawSingle = $"原始数据图表: {rawBase}";
                        data.Charts.Add(new ChartSection
                        {
                            Title = rawSingle,
                            NodeName = GetProducerNodeId(rawRef),
                            ArtifactKey = rawRef.Key,
                            SourceKind = ReportChartSourceKind.Raw,
                            ReportHeading = BuildArtifactChartReportHeading(data, rawRef, rawSingle)
                        });
                    }

                    continue;
                }

                if (dataBus.TryGet<NvhMemoryFile>(rawRef.Key, out var nvh) && nvh != null)
                {
                    var channels = RawNvhMemoryFileChartHelper.ExtractChannelSeries(nvh);
                    if (channels.Count == 0)
                        continue;

                    var rawBase = rawRef.DisplayName ?? "Raw";
                    if (channels.Count > 1)
                    {
                        for (var i = 0; i < channels.Count; i++)
                        {
                            var ch = channels[i];
                            var part = string.IsNullOrWhiteSpace(ch.DisplayName)
                                ? $"通道 {i + 1}"
                                : ch.DisplayName.Trim();
                            var rawAlgo = $"原始数据图表: {rawBase} — {part}";
                            data.Charts.Add(new ChartSection
                            {
                                Title = rawAlgo,
                                NodeName = GetProducerNodeId(rawRef),
                                ArtifactKey = rawRef.Key,
                                SubPlotSeriesIndex = i,
                                SourceKind = ReportChartSourceKind.Raw,
                                ReportHeading = BuildArtifactChartReportHeading(data, rawRef, rawAlgo)
                            });
                        }
                    }
                    else
                    {
                        var rawSingle = $"原始数据图表: {rawBase}";
                        data.Charts.Add(new ChartSection
                        {
                            Title = rawSingle,
                            NodeName = GetProducerNodeId(rawRef),
                            ArtifactKey = rawRef.Key,
                            SourceKind = ReportChartSourceKind.Raw,
                            ReportHeading = BuildArtifactChartReportHeading(data, rawRef, rawSingle)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 判断某产物是否应纳入报告图表：须通过 <see cref="ReportIncludeKeys.PreviewIncludesInReport"/>；若配置了产物键或生产者白名单则进一步匹配。
        /// </summary>
        private static bool ChartArtifactMatches(
            DataArtifactReference r,
            HashSet<string>? producerWhitelist,
            HashSet<string>? artifactKeyWhitelist)
        {
            if (!ReportIncludeKeys.PreviewIncludesInReport(r.Preview))
                return false;

            if (artifactKeyWhitelist != null && artifactKeyWhitelist.Count > 0)
                return artifactKeyWhitelist.Contains(r.Key);

            if (producerWhitelist != null && producerWhitelist.Count > 0)
            {
                var pid = GetProducerNodeId(r);
                return producerWhitelist.Contains(pid);
            }

            return true;
        }

        /// <summary>
        /// 从单次节点运行输出中提取单值限值判定行（实际值、上下限、是否通过）。
        /// </summary>
        private static void ExtractScalarJudgments(
            NodeRunRecord nr,
            List<ScalarJudgmentRow> rows,
            HashSet<string>? nodeIdWhitelist)
        {
            if (!ReportIncludeKeys.NodeRunIncludesInReport(nr))
                return;

            if (nodeIdWhitelist != null && nodeIdWhitelist.Count > 0)
            {
                var id = nr.NodeId ?? string.Empty;
                if (!nodeIdWhitelist.Contains(id))
                    return;
            }

            var output = nr.OutputSnapshot;
            if (output == null) return;

            if (!output.TryGetValue(NodeUiOutputKeys.ActualValue, out var av) || av == null)
                return;

            // 值卡控用 ValueCheckPass；曲线范围卡控仅输出 CurveCheckPass，需一并纳入单值表以展示上下限与结果。
            bool pass;
            if (output.TryGetValue(NodeUiOutputKeys.ValueCheckPass, out var vp) && vp is bool vb)
                pass = vb;
            else if (output.TryGetValue(NodeUiOutputKeys.CurveCheckPass, out var cp) && cp is bool cb)
                pass = cb;
            else
                return;

            rows.Add(new ScalarJudgmentRow
            {
                NodeName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                ParameterName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                ActualValue = TryConvertDouble(av),
                LowerLimit = TryGetDouble(output, NodeUiOutputKeys.LowerLimit),
                UpperLimit = TryGetDouble(output, NodeUiOutputKeys.UpperLimit),
                Pass = pass
            });
        }

        /// <summary>
        /// 从单次节点运行输出中提取曲线判定行（通过与否、失败详情），并生成报告用标题段。
        /// </summary>
        private static void ExtractCurveJudgments(
            NodeRunRecord nr,
            TestReportData data,
            HashSet<string>? nodeIdWhitelist)
        {
            if (!ReportIncludeKeys.NodeRunIncludesInReport(nr))
                return;

            if (nodeIdWhitelist != null && nodeIdWhitelist.Count > 0)
            {
                var id = nr.NodeId ?? string.Empty;
                if (!nodeIdWhitelist.Contains(id))
                    return;
            }

            var output = nr.OutputSnapshot;
            if (output == null || !output.ContainsKey(NodeUiOutputKeys.CurveCheckPass))
                return;

            var nodeName = nr.NodeName ?? nr.NodeId ?? string.Empty;
            var curveName = nr.NodeName ?? nr.NodeId ?? string.Empty;
            string? preferredChartKey = null;
            if (output.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var ck) && ck != null)
            {
                preferredChartKey = Convert.ToString(ck, CultureInfo.InvariantCulture)?.Trim();
                if (preferredChartKey?.Length == 0)
                    preferredChartKey = null;
            }

            data.CurveJudgments.Add(new CurveJudgmentRow
            {
                NodeId = nr.NodeId ?? string.Empty,
                NodeName = nodeName,
                CurveName = curveName,
                ActualValue = output.TryGetValue(NodeUiOutputKeys.ActualValue, out var act)
                    ? TryConvertDouble(act)
                    : null,
                LowerLimit = TryGetDouble(output, NodeUiOutputKeys.LowerLimit),
                UpperLimit = TryGetDouble(output, NodeUiOutputKeys.UpperLimit),
                Pass = TryGetBool(output, NodeUiOutputKeys.CurveCheckPass),
                FailDetail = output.TryGetValue(NodeUiOutputKeys.CurveFailDetail, out var fd)
                    ? fd?.ToString() : null,
                ReportHeading = BuildCurveJudgmentReportHeading(data, nodeName, curveName),
                PreferredChartArtifactKey = preferredChartKey
            });
        }

        /// <summary>
        /// 组合算法/原始数据图表在报告中的多级标题（工作流、设备通道预览、算法标签）。
        /// </summary>
        private static string BuildArtifactChartReportHeading(
            TestReportData data,
            DataArtifactReference artifact,
            string algorithmLabel)
        {
            var devChPreview = GetPreviewString(artifact, ReportArtifactPreviewKeys.DeviceChannel);
            return ReportHeadingChannelMerge.ComposeArtifactChartHeading(
                data.WorkFlowName,
                devChPreview,
                algorithmLabel);
        }

        /// <summary>
        /// 组合曲线判定行的报告标题：工作流-节点-曲线，若节点名与曲线名相同则省略重复段。
        /// </summary>
        private static string BuildCurveJudgmentReportHeading(TestReportData data, string nodeName, string curveName)
        {
            var testItem = ReportHeadingSegment(data.WorkFlowName);
            var mid = ReportHeadingSegment(nodeName);
            var tail = ReportHeadingSegment(curveName);
            if (string.Equals(mid, tail, StringComparison.Ordinal))
                return $"{testItem}-{mid}";
            return $"{testItem}-{mid}-{tail}";
        }

        /// <summary>
        /// 将空或仅空白标题规范为占位符 “-”，否则返回去除首尾空白的字符串。
        /// </summary>
        private static string ReportHeadingSegment(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        /// <summary>
        /// 解析逗号、分号或换行分隔的节点 ID 列表为哈希集合；空字符串返回 null 表示不筛选。
        /// </summary>
        private static HashSet<string>? ParseNodeIdSet(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var parts = text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) set.Add(t);
            }
            return set.Count == 0 ? null : set;
        }

        /// <summary>
        /// 解析逗号或换行分隔的产物键列表；空则返回 null 表示不按键筛选。
        /// </summary>
        private static HashSet<string>? ParseArtifactKeySet(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var parts = text.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) set.Add(t);
            }
            return set.Count == 0 ? null : set;
        }

        /// <summary>
        /// 从产物预览中读取生产者节点 ID；缺失时返回 “Unknown”。
        /// </summary>
        private static string GetProducerNodeId(DataArtifactReference r)
        {
            return GetPreviewString(r, "__ProducerNodeId") ?? "Unknown";
        }

        /// <summary>
        /// 读取产物 <see cref="DataArtifactReference.Preview"/> 中指定键的字符串形式。
        /// </summary>
        private static string? GetPreviewString(DataArtifactReference r, string key)
        {
            if (r.Preview != null && r.Preview.TryGetValue(key, out var v))
                return v?.ToString();
            return null;
        }

        private static string PickStation(string? fromConfig, NodeContext? ctx)
        {
            if (!string.IsNullOrWhiteSpace(fromConfig))
                return fromConfig.Trim();
            return GetGlobalString(ctx, "测试工位")
                   ?? GetGlobalString(ctx, "TestStation")
                   ?? string.Empty;
        }

        private static string PickLine(string? fromConfig, NodeContext? ctx)
        {
            if (!string.IsNullOrWhiteSpace(fromConfig))
                return fromConfig.Trim();
            return GetGlobalString(ctx, "测试线体")
                   ?? GetGlobalString(ctx, "TestLine")
                   ?? string.Empty;
        }

        /// <summary>
        /// 从节点上下文全局变量读取字符串配置项。
        /// </summary>
        private static string? GetGlobalString(NodeContext? ctx, string key)
        {
            if (ctx?.GlobalVariables == null) return null;
            if (ctx.GlobalVariables.TryGetValue(key, out var v) && v != null)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            return null;
        }

        /// <summary>
        /// 读取合并报告分节的排序序号（<see cref="ReportContextKeys.SectionSequenceOrder"/>），解析失败时为 0。
        /// </summary>
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

        /// <summary>
        /// 从字典安全读取并转换为 double。
        /// </summary>
        private static double? TryGetDouble(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null)
                return TryConvertDouble(v);
            return null;
        }

        /// <summary>
        /// 将任意对象按不变区域格式转换为 double，失败返回 null。
        /// </summary>
        private static double? TryConvertDouble(object? v)
        {
            if (v == null) return null;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        /// <summary>
        /// 从字典读取布尔值；非 bool 或缺失时视为 false。
        /// </summary>
        private static bool TryGetBool(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v is bool b) return b;
            return false;
        }

        /// <summary>
        /// 将节点运行记录按主界面树项对应的工作流顺序重排（拓扑 + 声明顺序兜底），避免并行执行时按完成先后打乱报表行序。
        /// </summary>
        private static IReadOnlyList<NodeRunRecord> OrderNodeRunsByWorkflowTree(
            IReadOnlyList<NodeRunRecord> runs,
            NodeContext? context)
        {
            if (runs == null || runs.Count <= 1)
                return runs ?? Array.Empty<NodeRunRecord>();

            var orderMap = BuildWorkflowNodeOrderMap(context?.ParentWorkFlow);
            var indexed = new List<(NodeRunRecord Run, int Index)>(runs.Count);
            for (var i = 0; i < runs.Count; i++)
                indexed.Add((runs[i], i));

            indexed.Sort((a, b) =>
            {
                var aKey = a.Run?.NodeId ?? string.Empty;
                var bKey = b.Run?.NodeId ?? string.Empty;
                var aOrder = orderMap.TryGetValue(aKey, out var ao) ? ao : int.MaxValue;
                var bOrder = orderMap.TryGetValue(bKey, out var bo) ? bo : int.MaxValue;
                var cmp = aOrder.CompareTo(bOrder);
                if (cmp != 0)
                    return cmp;

                // 同层/未知顺序时按主页展示习惯做二级排序：曲线范围卡控在前，曲线统计卡控在后。
                cmp = CompareNodeDisplayPriority(a.Run?.NodeName, b.Run?.NodeName);
                return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
            });

            var ordered = new List<NodeRunRecord>(indexed.Count);
            foreach (var (run, _) in indexed)
                ordered.Add(run);
            return ordered;
        }

        /// <summary>
        /// 主页树项同层节点的兜底顺序：曲线范围卡控（逐点）优先于曲线统计卡控；其余保持稳定序。
        /// </summary>
        private static int CompareNodeDisplayPriority(string? leftName, string? rightName)
        {
            static int Priority(string? nodeName)
            {
                if (string.IsNullOrWhiteSpace(nodeName))
                    return int.MaxValue;

                var n = nodeName.Trim();
                if (n.Contains("曲线范围卡控", StringComparison.Ordinal))
                    return 10;
                if (n.Contains("曲线统计卡控", StringComparison.Ordinal))
                    return 20;
                return 100;
            }

            return Priority(leftName).CompareTo(Priority(rightName));
        }

        /// <summary>
        /// 构建工作流节点序映射：优先依赖拓扑，层内按节点声明顺序。
        /// </summary>
        private static Dictionary<string, int> BuildWorkflowNodeOrderMap(WorkFlowNode? workflow)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var nodes = (workflow?.Nodes ?? new List<Node>())
                .Where(n => n != null && !string.IsNullOrWhiteSpace(n.Id))
                .ToList();
            if (nodes.Count == 0)
                return map;

            var nodeById = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var originalIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < nodes.Count; i++)
                originalIndex[nodes[i].Id] = i;

            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                indegree[node.Id] = 0;
                adjacency[node.Id] = new List<string>();
            }

            foreach (var c in workflow?.Connections ?? new List<Connection>())
            {
                if (c == null ||
                    string.IsNullOrWhiteSpace(c.SourceNodeId) ||
                    string.IsNullOrWhiteSpace(c.TargetNodeId) ||
                    string.Equals(c.SourceNodeId, c.TargetNodeId, StringComparison.Ordinal) ||
                    !nodeById.ContainsKey(c.SourceNodeId) ||
                    !nodeById.ContainsKey(c.TargetNodeId))
                {
                    continue;
                }

                adjacency[c.SourceNodeId].Add(c.TargetNodeId);
                indegree[c.TargetNodeId]++;
            }

            var ordered = new List<string>(nodes.Count);
            var queue = nodes
                .Where(n => indegree[n.Id] == 0)
                .OrderBy(n => originalIndex[n.Id])
                .ToList();

            while (queue.Count > 0)
            {
                var current = queue[0];
                queue.RemoveAt(0);
                ordered.Add(current.Id);

                foreach (var targetId in adjacency[current.Id])
                {
                    indegree[targetId]--;
                    if (indegree[targetId] == 0)
                        queue.Add(nodeById[targetId]);
                }

                queue = queue.OrderBy(n => originalIndex[n.Id]).ToList();
            }

            if (ordered.Count < nodes.Count)
            {
                var exists = new HashSet<string>(ordered, StringComparer.Ordinal);
                foreach (var node in nodes)
                {
                    if (!exists.Contains(node.Id))
                        ordered.Add(node.Id);
                }
            }

            for (var i = 0; i < ordered.Count; i++)
                map[ordered[i]] = i;
            return map;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;

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
                foreach (var nr in runRecord.NodeRuns)
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

            data.SectionSequenceOrder = GetReportSectionSequence(context);
            return data;
        }

        /// <summary>
        /// 从数据总线收集算法类图表：仅包含可解析为 <see cref="ChartDisplayPayload"/> 且通过包含键与白名单过滤的条目；子图布局时按系列拆成多个 <see cref="ChartSection"/>。
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

                if (payload.Series is { Count: > 0 } && payload.LayoutMode == ChartLayoutMode.SubPlots)
                {
                    for (var i = 0; i < payload.Series.Count; i++)
                    {
                        var ser = payload.Series[i];
                        var part = string.IsNullOrWhiteSpace(ser.Name) ? $"子图 {i + 1}" : ser.Name.Trim();
                        var baseTitle = algRef.DisplayName ?? "算法图表";
                        var algoLabel = $"{baseTitle} — {part}";
                        data.Charts.Add(new ChartSection
                        {
                            Title = algoLabel,
                            NodeName = GetProducerNodeId(algRef),
                            Description = algRef.Description,
                            ArtifactKey = algRef.Key,
                            SubPlotSeriesIndex = i,
                            SourceKind = ReportChartSourceKind.Algorithm,
                            ReportHeading = BuildArtifactChartReportHeading(data, algRef, algoLabel)
                        });
                    }
                }
                else
                {
                    var algoSingle = algRef.DisplayName ?? "算法图表";
                    data.Charts.Add(new ChartSection
                    {
                        Title = algoSingle,
                        NodeName = GetProducerNodeId(algRef),
                        Description = algRef.Description,
                        ArtifactKey = algRef.Key,
                        SourceKind = ReportChartSourceKind.Algorithm,
                        ReportHeading = BuildArtifactChartReportHeading(data, algRef, algoSingle)
                    });
                }
            }
        }

        /// <summary>
        /// 从数据总线收集原始数据类图表：规则与算法图类似，标题前缀为「原始数据」。
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
                if (!dataBus.TryGet<ChartDisplayPayload>(rawRef.Key, out var payload) || payload == null)
                    continue;

                if (!ChartArtifactMatches(rawRef, producerWhitelist, artifactKeyWhitelist))
                    continue;

                var rawBase = rawRef.DisplayName ?? "Raw";
                if (payload.Series is { Count: > 0 } && payload.LayoutMode == ChartLayoutMode.SubPlots)
                {
                    for (var i = 0; i < payload.Series.Count; i++)
                    {
                        var ser = payload.Series[i];
                        var part = string.IsNullOrWhiteSpace(ser.Name) ? $"子图 {i + 1}" : ser.Name.Trim();
                        var rawAlgo = $"原始数据: {rawBase} — {part}";
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
                    var rawSingle = $"原始数据: {rawBase}";
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

            rows.Add(new ScalarJudgmentRow
            {
                NodeName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                ParameterName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                ActualValue = TryConvertDouble(av),
                LowerLimit = TryGetDouble(output, NodeUiOutputKeys.LowerLimit),
                UpperLimit = TryGetDouble(output, NodeUiOutputKeys.UpperLimit),
                Pass = TryGetBool(output, NodeUiOutputKeys.ValueCheckPass)
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
            data.CurveJudgments.Add(new CurveJudgmentRow
            {
                NodeId = nr.NodeId ?? string.Empty,
                NodeName = nodeName,
                CurveName = curveName,
                Pass = TryGetBool(output, NodeUiOutputKeys.CurveCheckPass),
                FailDetail = output.TryGetValue(NodeUiOutputKeys.CurveFailDetail, out var fd)
                    ? fd?.ToString() : null,
                ReportHeading = BuildCurveJudgmentReportHeading(data, nodeName, curveName)
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
    }
}

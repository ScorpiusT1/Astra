using System;
using System.Collections.Generic;
using System.Globalization;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;

namespace Astra.Plugins.WorkflowArchive.Reporting
{
    /// <summary>
    /// 从 <see cref="ITestDataBus"/> + <see cref="WorkFlowRunRecord"/> 采集报告所需的最终结果数据（单值、曲线判定、算法/可选原始数据图）。
    /// </summary>
    public static class ReportDataCollector
    {
        /// <param name="options">为 null 时：全部单值/曲线、全部算法图，原始数据图由调用方单独传入的旧参数控制（见生成器）。</param>
        public static TestReportData Collect(
            WorkFlowRunRecord runRecord,
            ITestDataBus? dataBus,
            NodeContext? context,
            ReportGenerationOptions? options)
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
                    ExtractCurveJudgments(nr, data.CurveJudgments, curveIds);
                }
            }

            if (dataBus != null)
            {
                if (includeAlg)
                    CollectAlgorithmCharts(dataBus, data, chartProducerIds, chartKeys);
                if (includeRaw)
                    CollectRawCharts(dataBus, data, chartProducerIds, chartKeys);
            }

            return data;
        }

        private static void CollectAlgorithmCharts(
            ITestDataBus dataBus,
            TestReportData data,
            HashSet<string>? producerWhitelist,
            HashSet<string>? artifactKeyWhitelist)
        {
            var algorithmRefs = dataBus.Query(DataArtifactCategory.Algorithm);
            foreach (var algRef in algorithmRefs)
            {
                if (!dataBus.TryGet<ChartDisplayPayload>(algRef.Key, out _))
                    continue;

                if (!ChartArtifactMatches(algRef, producerWhitelist, artifactKeyWhitelist))
                    continue;

                data.Charts.Add(new ChartSection
                {
                    Title = algRef.DisplayName ?? "算法图表",
                    NodeName = GetProducerNodeId(algRef),
                    Description = algRef.Description,
                    ArtifactKey = algRef.Key,
                    SourceKind = ReportChartSourceKind.Algorithm
                });
            }
        }

        private static void CollectRawCharts(
            ITestDataBus dataBus,
            TestReportData data,
            HashSet<string>? producerWhitelist,
            HashSet<string>? artifactKeyWhitelist)
        {
            var rawRefs = dataBus.Query(DataArtifactCategory.Raw);
            foreach (var rawRef in rawRefs)
            {
                if (!dataBus.TryGet<ChartDisplayPayload>(rawRef.Key, out _))
                    continue;

                if (!ChartArtifactMatches(rawRef, producerWhitelist, artifactKeyWhitelist))
                    continue;

                data.Charts.Add(new ChartSection
                {
                    Title = $"原始数据: {rawRef.DisplayName ?? "Raw"}",
                    NodeName = GetProducerNodeId(rawRef),
                    ArtifactKey = rawRef.Key,
                    SourceKind = ReportChartSourceKind.Raw
                });
            }
        }

        private static bool ChartArtifactMatches(
            DataArtifactReference r,
            HashSet<string>? producerWhitelist,
            HashSet<string>? artifactKeyWhitelist)
        {
            if (artifactKeyWhitelist != null && artifactKeyWhitelist.Count > 0)
                return artifactKeyWhitelist.Contains(r.Key);

            if (producerWhitelist != null && producerWhitelist.Count > 0)
            {
                var pid = GetProducerNodeId(r);
                return producerWhitelist.Contains(pid);
            }

            return true;
        }

        private static void ExtractScalarJudgments(
            NodeRunRecord nr,
            List<ScalarJudgmentRow> rows,
            HashSet<string>? nodeIdWhitelist)
        {
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

        private static void ExtractCurveJudgments(
            NodeRunRecord nr,
            List<CurveJudgmentRow> rows,
            HashSet<string>? nodeIdWhitelist)
        {
            if (nodeIdWhitelist != null && nodeIdWhitelist.Count > 0)
            {
                var id = nr.NodeId ?? string.Empty;
                if (!nodeIdWhitelist.Contains(id))
                    return;
            }

            var output = nr.OutputSnapshot;
            if (output == null || !output.ContainsKey(NodeUiOutputKeys.CurveCheckPass))
                return;

            rows.Add(new CurveJudgmentRow
            {
                NodeName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                CurveName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                Pass = TryGetBool(output, NodeUiOutputKeys.CurveCheckPass),
                FailDetail = output.TryGetValue(NodeUiOutputKeys.CurveFailDetail, out var fd)
                    ? fd?.ToString() : null
            });
        }

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

        private static string GetProducerNodeId(DataArtifactReference r)
        {
            return GetPreviewString(r, "__ProducerNodeId") ?? "Unknown";
        }

        private static string? GetPreviewString(DataArtifactReference r, string key)
        {
            if (r.Preview != null && r.Preview.TryGetValue(key, out var v))
                return v?.ToString();
            return null;
        }

        private static string? GetGlobalString(NodeContext? ctx, string key)
        {
            if (ctx?.GlobalVariables == null) return null;
            if (ctx.GlobalVariables.TryGetValue(key, out var v) && v != null)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            return null;
        }

        private static double? TryGetDouble(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null)
                return TryConvertDouble(v);
            return null;
        }

        private static double? TryConvertDouble(object? v)
        {
            if (v == null) return null;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        private static bool TryGetBool(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v is bool b) return b;
            return false;
        }
    }
}

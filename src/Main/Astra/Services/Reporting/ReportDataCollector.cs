using System;
using System.Collections.Generic;
using System.Globalization;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;

namespace Astra.Services.Reporting
{
    /// <summary>
    /// 从 <see cref="ITestDataBus"/> + <see cref="WorkFlowRunRecord"/> 采集报告所需的全部结构化数据。
    /// </summary>
    public static class ReportDataCollector
    {
        public static TestReportData Collect(
            WorkFlowRunRecord runRecord,
            ITestDataBus? dataBus,
            NodeContext? context)
        {
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
                    data.NodeSummaries.Add(new NodeRunSummary
                    {
                        NodeName = nr.NodeName ?? nr.NodeId ?? string.Empty,
                        State = nr.State.ToString(),
                        Message = nr.Message,
                        Duration = nr.Duration
                    });

                    ExtractScalarJudgments(nr, data.ScalarJudgments);
                    ExtractCurveJudgments(nr, data.CurveJudgments);
                }
            }

            if (dataBus != null)
            {
                CollectAlgorithmCharts(dataBus, data);
                CollectRawCharts(dataBus, data);
                CollectTextArtifacts(dataBus, data);
            }

            return data;
        }

        private static void CollectAlgorithmCharts(ITestDataBus dataBus, TestReportData data)
        {
            var algorithmRefs = dataBus.Query(DataArtifactCategory.Algorithm);
            foreach (var algRef in algorithmRefs)
            {
                if (!dataBus.TryGet<ChartDisplayPayload>(algRef.Key, out _))
                    continue;

                data.Charts.Add(new ChartSection
                {
                    Title = algRef.DisplayName ?? "算法图表",
                    NodeName = GetProducerNodeId(algRef),
                    Description = algRef.Description,
                    ArtifactKey = algRef.Key
                });
            }
        }

        private static void CollectRawCharts(ITestDataBus dataBus, TestReportData data)
        {
            var rawRefs = dataBus.Query(DataArtifactCategory.Raw);
            foreach (var rawRef in rawRefs)
            {
                data.Charts.Add(new ChartSection
                {
                    Title = $"原始数据: {rawRef.DisplayName ?? "Raw"}",
                    NodeName = GetProducerNodeId(rawRef),
                    ArtifactKey = rawRef.Key
                });
            }
        }

        private static void CollectTextArtifacts(ITestDataBus dataBus, TestReportData data)
        {
            var textRefs = dataBus.Query(DataArtifactCategory.Text);
            foreach (var tr in textRefs)
            {
                if (!dataBus.TryGet<string>(tr.Key, out var text))
                    continue;

                data.TextArtifacts.Add(new TextArtifactSection
                {
                    Title = tr.DisplayName ?? "文本",
                    ContentType = GetPreviewString(tr, "ContentType") ?? "text/plain",
                    Content = text ?? string.Empty
                });
            }
        }

        private static void ExtractScalarJudgments(NodeRunRecord nr, List<ScalarJudgmentRow> rows)
        {
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

        private static void ExtractCurveJudgments(NodeRunRecord nr, List<CurveJudgmentRow> rows)
        {
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

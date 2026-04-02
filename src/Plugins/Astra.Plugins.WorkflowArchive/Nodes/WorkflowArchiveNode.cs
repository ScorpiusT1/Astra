using Astra.Core.Archiving;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Astra.Plugins.WorkflowArchive.Nodes
{
    /// <summary>
    /// 在流程末尾显式触发与引擎相同的归档逻辑（成功路径下可先落盘 Raw；完整报告由引擎在释放 Raw 前补写）。
    /// </summary>
    public class WorkflowArchiveNode : Node
    {
        [JsonIgnore]
        private List<ReportNodePickEntry>? _scalarOptionsCache;

        [JsonIgnore]
        private List<ReportNodePickEntry>? _curveOptionsCache;

        [JsonIgnore]
        private List<ReportNodePickEntry>? _chartProducerOptionsCache;

        [JsonIgnore]
        private string? _peerFingerprint;

        [JsonProperty("ReportScalarNodeIdsFilter", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyReportScalarNodeIdsFilter;

        [JsonProperty("ReportCurveNodeIdsFilter", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyReportCurveNodeIdsFilter;

        [JsonProperty("ReportChartProducerNodeIdsFilter", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyReportChartProducerNodeIdsFilter;

        [Display(Name = "说明", Order = 0, Description = "与引擎在 Raw 清理前调用同一 IWorkflowArchiveService；首轮通常无结果链，第二轮由引擎写入 report.html / run_record.json。报告白名单在多流程编辑器中会汇总全部子流程内节点（子流程名/节点名）。")]
        public string? Note { get; set; }

        [Display(
            Name = "单值判定节点",
            GroupName = "报告内容",
            Order = 1,
            Description = "可多选，仅列出可产生单值判定行的节点类型。列表优先为全部子流程（子流程名/节点名）；无全局列表时仅为当前子流程。未选任何项表示包含全部单值判定。")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ReportScalarPeerOptions), DisplayMemberPath = nameof(ReportNodePickEntry.DisplayName))]
        public List<ReportNodePickEntry> ReportScalarNodePicks { get; set; } = new();

        [Display(
            Name = "曲线判定节点",
            GroupName = "报告内容",
            Order = 2,
            Description = "可多选，仅列出可产生曲线判定输出的节点类型。未选任何项表示包含全部曲线判定。")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ReportCurvePeerOptions), DisplayMemberPath = nameof(ReportNodePickEntry.DisplayName))]
        public List<ReportNodePickEntry> ReportCurveNodePicks { get; set; } = new();

        [Display(
            Name = "图表产生节点",
            GroupName = "报告内容",
            Order = 3,
            Description = "可多选，仅列出可向数据总线发布图表类产物的节点类型。未选任何项表示不按产生者过滤。")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ReportChartProducerPeerOptions), DisplayMemberPath = nameof(ReportNodePickEntry.DisplayName))]
        public List<ReportNodePickEntry> ReportChartProducerNodePicks { get; set; } = new();

        [Display(Name = "报告包含算法图", GroupName = "报告内容", Order = 4)]
        public bool ReportIncludeAlgorithmCharts { get; set; } = true;

        [Display(Name = "报告包含原始数据图", GroupName = "报告内容", Order = 5)]
        public bool ReportIncludeRawDataCharts { get; set; } = true;

        /// <summary>单值判定白名单候选（按节点类型过滤）。</summary>
        [JsonIgnore]
        public IEnumerable<ReportNodePickEntry> ReportScalarPeerOptions
        {
            get
            {
                EnsurePeerOptionCaches();
                return _scalarOptionsCache ?? Enumerable.Empty<ReportNodePickEntry>();
            }
        }

        /// <summary>曲线判定白名单候选。</summary>
        [JsonIgnore]
        public IEnumerable<ReportNodePickEntry> ReportCurvePeerOptions
        {
            get
            {
                EnsurePeerOptionCaches();
                return _curveOptionsCache ?? Enumerable.Empty<ReportNodePickEntry>();
            }
        }

        /// <summary>图表产生者白名单候选。</summary>
        [JsonIgnore]
        public IEnumerable<ReportNodePickEntry> ReportChartProducerPeerOptions
        {
            get
            {
                EnsurePeerOptionCaches();
                return _chartProducerOptionsCache ?? Enumerable.Empty<ReportNodePickEntry>();
            }
        }

        private void EnsurePeerOptionCaches()
        {
            var fp = ComputePeerFingerprint();
            if (_peerFingerprint == fp && _scalarOptionsCache != null)
                return;

            var prevFp = _peerFingerprint;
            _peerFingerprint = fp;
            BuildPeerOptionLists(out _scalarOptionsCache, out _curveOptionsCache, out _chartProducerOptionsCache);
            RemapPicksToCanonical(ReportScalarNodePicks, _scalarOptionsCache);
            RemapPicksToCanonical(ReportCurveNodePicks, _curveOptionsCache);
            RemapPicksToCanonical(ReportChartProducerNodePicks, _chartProducerOptionsCache);
            if (prevFp != fp)
            {
                OnPropertyChanged(nameof(ReportScalarPeerOptions));
                OnPropertyChanged(nameof(ReportCurvePeerOptions));
                OnPropertyChanged(nameof(ReportChartProducerPeerOptions));
            }
        }

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext context)
        {
            MigrateLegacyStringFilters();
            InvalidatePeerCache();
        }

        private void MigrateLegacyStringFilters()
        {
            if (ReportScalarNodePicks.Count == 0 && !string.IsNullOrWhiteSpace(_legacyReportScalarNodeIdsFilter))
            {
                foreach (var id in SplitLegacyIds(_legacyReportScalarNodeIdsFilter))
                    ReportScalarNodePicks.Add(new ReportNodePickEntry(id, id));
            }

            if (ReportCurveNodePicks.Count == 0 && !string.IsNullOrWhiteSpace(_legacyReportCurveNodeIdsFilter))
            {
                foreach (var id in SplitLegacyIds(_legacyReportCurveNodeIdsFilter))
                    ReportCurveNodePicks.Add(new ReportNodePickEntry(id, id));
            }

            if (ReportChartProducerNodePicks.Count == 0 && !string.IsNullOrWhiteSpace(_legacyReportChartProducerNodeIdsFilter))
            {
                foreach (var id in SplitLegacyIds(_legacyReportChartProducerNodeIdsFilter))
                    ReportChartProducerNodePicks.Add(new ReportNodePickEntry(id, id));
            }

            _legacyReportScalarNodeIdsFilter = null;
            _legacyReportCurveNodeIdsFilter = null;
            _legacyReportChartProducerNodeIdsFilter = null;
        }

        private void InvalidatePeerCache()
        {
            _scalarOptionsCache = null;
            _curveOptionsCache = null;
            _chartProducerOptionsCache = null;
            _peerFingerprint = null;
        }

        private string ComputePeerFingerprint()
        {
            var cat = WorkflowArchivePeerCatalog.GetFingerprint();
            if (!string.IsNullOrEmpty(cat))
                return cat + "\u001e" + Id;

            var wf = ContainingWorkflow;
            if (wf?.Nodes == null) return string.Empty;
            var peers = wf.Nodes
                .Where(n => n != null && n.Id != Id && n is not WorkFlowNode)
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .ToList();
            return string.Join("|", peers.Select(p =>
                $"{p.Id}\u001f{p.Name}\u001f{p.NodeType}\u001f{(byte)ReportWhitelistNodeCategories.FromNode(p)}"));
        }

        private void BuildPeerOptionLists(
            out List<ReportNodePickEntry> scalar,
            out List<ReportNodePickEntry> curve,
            out List<ReportNodePickEntry> chartProducer)
        {
            var fromCatalog = WorkflowArchivePeerCatalog.GetEntries();
            if (fromCatalog.Count > 0)
            {
                scalar = fromCatalog
                    .Where(e => e.NodeId != Id && e.WhitelistCategories.HasFlag(ReportWhitelistCategories.Scalar))
                    .ToList();
                curve = fromCatalog
                    .Where(e => e.NodeId != Id && e.WhitelistCategories.HasFlag(ReportWhitelistCategories.Curve))
                    .ToList();
                chartProducer = fromCatalog
                    .Where(e => e.NodeId != Id && e.WhitelistCategories.HasFlag(ReportWhitelistCategories.ChartProducer))
                    .ToList();
                return;
            }

            scalar = new List<ReportNodePickEntry>();
            curve = new List<ReportNodePickEntry>();
            chartProducer = new List<ReportNodePickEntry>();
            var wf = ContainingWorkflow;
            if (wf?.Nodes == null)
                return;

            foreach (var n in wf.Nodes
                         .Where(x => x != null && x.Id != Id && x is not WorkFlowNode)
                         .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(x => x.Id, StringComparer.Ordinal))
            {
                var cat = ReportWhitelistNodeCategories.FromNode(n);
                if (cat == ReportWhitelistCategories.None)
                    continue;
                var label = ReportNodePickDisplay.FormatNodeLabel(n);
                var entry = new ReportNodePickEntry(n.Id, label, cat);
                if (cat.HasFlag(ReportWhitelistCategories.Scalar))
                    scalar.Add(entry);
                if (cat.HasFlag(ReportWhitelistCategories.Curve))
                    curve.Add(entry);
                if (cat.HasFlag(ReportWhitelistCategories.ChartProducer))
                    chartProducer.Add(entry);
            }
        }

        private static void RemapPicksToCanonical(List<ReportNodePickEntry> picks, List<ReportNodePickEntry> canonical)
        {
            if (picks.Count == 0 || canonical.Count == 0) return;
            var dict = canonical.ToDictionary(x => x.NodeId, StringComparer.Ordinal);
            for (var i = 0; i < picks.Count; i++)
            {
                if (dict.TryGetValue(picks[i].NodeId, out var c))
                    picks[i] = c;
            }
        }

        private static IEnumerable<string> SplitLegacyIds(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            foreach (var p in text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = p.Trim();
                if (t.Length > 0) yield return t;
            }
        }

        private static string? JoinNodeIds(IReadOnlyList<ReportNodePickEntry>? picks)
        {
            if (picks == null || picks.Count == 0) return null;
            var ids = picks.Select(p => p.NodeId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList();
            return ids.Count == 0 ? null : string.Join(",", ids);
        }

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"结果归档:{Name}");
            if (context.ServiceProvider == null)
            {
                return ExecutionResult.Failed("ServiceProvider 为空，无法解析 IWorkflowArchiveService");
            }

            var svc = context.ServiceProvider.GetService(typeof(IWorkflowArchiveService)) as IWorkflowArchiveService;
            if (svc == null)
            {
                return ExecutionResult.Failed("未注册 IWorkflowArchiveService");
            }

            WorkFlowRunRecord? runRecord = null;
            if (context.ServiceProvider.GetService(typeof(IWorkFlowManager)) is IWorkFlowManager mgr &&
                !string.IsNullOrWhiteSpace(context.ExecutionId))
            {
                var rr = mgr.GetWorkFlowRunRecord(context.ExecutionId);
                if (rr.Success && rr.Data != null)
                {
                    runRecord = rr.Data;
                }
            }

            var wfKey = context.GetMetadata<string>("WorkFlowKey", string.Empty);
            var request = new WorkflowArchiveRequest
            {
                Trigger = WorkflowArchiveTrigger.WorkflowNode,
                ExecutionId = context.ExecutionId ?? string.Empty,
                WorkFlowKey = wfKey,
                WorkFlowName = context.ParentWorkFlow?.Name ?? string.Empty,
                NodeContext = context,
                ExecutionStatus = null,
                RunRecord = runRecord,
                ReportOptions = BuildReportOptions()
            };

            try
            {
                var ar = await svc.ArchiveAsync(request, cancellationToken).ConfigureAwait(false);
                var msg = ar.Message ?? (ar.Success ? "OK" : "失败");
                log.Info($"归档: {msg}，目录={ar.OutputDirectory}");
                return ExecutionResult.Successful(msg);
            }
            catch (Exception ex)
            {
                log.Error($"归档异常: {ex.Message}");
                return ExecutionResult.Failed($"归档异常: {ex.Message}", ex);
            }
        }

        private ReportGenerationOptions BuildReportOptions()
        {
            return new ReportGenerationOptions
            {
                ScalarNodeIdsFilter = JoinNodeIds(ReportScalarNodePicks),
                CurveNodeIdsFilter = JoinNodeIds(ReportCurveNodePicks),
                ChartProducerNodeIdsFilter = JoinNodeIds(ReportChartProducerNodePicks),
                IncludeAlgorithmCharts = ReportIncludeAlgorithmCharts,
                IncludeRawDataCharts = ReportIncludeRawDataCharts
            };
        }
    }
}

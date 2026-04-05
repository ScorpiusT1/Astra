using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Serialization;
using Astra.Core.Orchestration;
using Astra.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Services.Home
{
    public sealed class HomeWorkflowExecutionService : IHomeWorkflowExecutionService
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly IUiLogService _uiLogService;
        private readonly IMultiWorkflowSerializer _multiWorkflowSerializer = new MultiWorkflowSerializer();
        private readonly IMasterWorkflowOrchestrator _orchestrator;

        public HomeWorkflowExecutionService(
            IConfigurationManager configurationManager,
            IUiLogService uiLogService,
            IMasterWorkflowOrchestrator orchestrator)
        {
            _configurationManager = configurationManager;
            _uiLogService = uiLogService;
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }

        public async Task ExecuteCurrentConfiguredMasterAsync(CancellationToken cancellationToken, string? manualBarcode = null)
        {
            var scriptPath = await ResolveCurrentWorkflowPathAsync().ConfigureAwait(false);
            await ExecuteConfiguredMasterAtPathAsync(scriptPath, cancellationToken, manualBarcode).ConfigureAwait(false);
        }

        public async Task ExecuteConfiguredMasterAtPathAsync(string? masterWorkflowFilePath, CancellationToken cancellationToken, string? snForGlobalVariable = null)
        {
            var scriptPath = masterWorkflowFilePath?.Trim();
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            {
                _uiLogService.Error("Home 独立执行失败：未找到主流程脚本文件或路径无效。");
                return;
            }

            var loadResult = _multiWorkflowSerializer.LoadFromFile(scriptPath);
            if (!loadResult.Success || loadResult.Data == null)
            {
                _uiLogService.Error($"Home 独立执行失败：脚本加载失败，{loadResult.ErrorMessage}");
                return;
            }

            var plan = BuildExecutionPlan(loadResult.Data);
            if (plan.Entries.Count == 0)
            {
                _uiLogService.Warn("Home 独立执行：当前脚本没有可执行子流程。");
                return;
            }

            var options = new MasterExecutionOptions { Sn = snForGlobalVariable };

            try
            {
                var result = await _orchestrator.ExecuteAsync(plan, options, cancellationToken).ConfigureAwait(false);

                if (result.OverallSuccess)
                    _uiLogService.Info($"Home 独立执行完成：成功 {result.SuccessCount}，跳过 {result.SkippedCount}");
                else
                    _uiLogService.Warn($"Home 独立执行完成：成功 {result.SuccessCount}，失败 {result.FailedCount}，跳过 {result.SkippedCount}");
            }
            catch (OperationCanceledException)
            {
                _uiLogService.Info("Home 独立执行已取消。");
            }
            catch (Exception ex)
            {
                _uiLogService.Error($"Home 独立执行异常: {ex.Message}");
            }
        }

        private async Task<string?> ResolveCurrentWorkflowPathAsync()
        {
            var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
            if (all?.Success != true || all.Data == null)
                return null;

            var latest = all.Data
                .OfType<SoftwareConfig>()
                .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (latest == null)
                return null;

            if (!string.IsNullOrWhiteSpace(latest.CurrentWorkflowId))
                return latest.CurrentWorkflowId;

            return (latest.Duts ?? [])
                .Select(d => d?.WorkflowId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        internal static MasterExecutionPlan BuildExecutionPlan(MultiWorkflowData data)
        {
            var entries = new Dictionary<string, SubWorkflowEntry>(StringComparer.Ordinal);
            var ordered = new List<SubWorkflowEntry>();
            var successors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            var refs = data.MasterWorkflow?.SubWorkflowReferences ?? new List<WorkflowReference>();
            foreach (var reference in refs)
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.Id) || string.IsNullOrWhiteSpace(reference.SubWorkflowId))
                    continue;

                if (!data.SubWorkflows.TryGetValue(reference.SubWorkflowId, out var workflow) || workflow == null)
                    continue;

                var entry = new SubWorkflowEntry
                {
                    RefId = reference.Id,
                    DisplayName = string.IsNullOrWhiteSpace(reference.DisplayName)
                        ? (workflow.Name ?? "未命名子流程")
                        : reference.DisplayName,
                    Workflow = workflow,
                    IsEnabled = reference.IsEnabled,
                    ContinueOnFailure = reference.ContinueOnFailure,
                    ExecuteLast = reference.ExecuteLast
                };

                entries[reference.Id] = entry;
                ordered.Add(entry);
            }

            if (entries.Count == 0)
            {
                foreach (var workflow in data.SubWorkflows?.Values?.Where(w => w != null) ?? Enumerable.Empty<WorkFlowNode>())
                {
                    var entry = new SubWorkflowEntry
                    {
                        RefId = workflow.Id,
                        DisplayName = workflow.Name ?? "未命名子流程",
                        Workflow = workflow,
                        IsEnabled = true,
                        ContinueOnFailure = true,
                        ExecuteLast = false
                    };
                    entries[workflow.Id] = entry;
                    ordered.Add(entry);
                }

                return new MasterExecutionPlan
                {
                    Entries = entries,
                    OrderedEntries = ordered,
                    Successors = entries.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal),
                    HasDependencies = false
                };
            }

            foreach (var id in entries.Keys)
                successors[id] = new List<string>();

            var depCount = 0;
            foreach (var edge in data.MasterWorkflow?.Edges ?? new List<Edge>())
            {
                if (edge == null || !entries.ContainsKey(edge.SourceNodeId) || !entries.ContainsKey(edge.TargetNodeId))
                    continue;

                successors[edge.SourceNodeId].Add(edge.TargetNodeId);
                entries[edge.TargetNodeId].PredecessorIds.Add(edge.SourceNodeId);
                depCount++;
            }

            return new MasterExecutionPlan
            {
                Entries = entries,
                OrderedEntries = ordered,
                Successors = successors,
                HasDependencies = depCount > 0
            };
        }
    }
}

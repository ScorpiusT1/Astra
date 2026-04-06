using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Interfaces;
using Size2D = Astra.Core.Nodes.Geometry.Size2D;
using PortDirection = Astra.Core.Nodes.Models.PortDirection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.Logic.Nodes
{
    /// <summary>
    /// 根据与本节点相连的直连上游节点的最近一次执行结果做聚合判断（如全部通过、全部失败、任一失败等）。
    /// 不传递业务数据，仅依赖工作流图中入边所指向的上游节点及其 <see cref="Node.LastExecutionResult"/>。
    /// </summary>
    public sealed class UpstreamTestAggregationNode : Node, IPropertyVisibilityProvider
    {
        public UpstreamTestAggregationNode()
        {
            NodeType = nameof(UpstreamTestAggregationNode);
            Name = "上游测试结果聚合";
            Icon = "CodeBranch";
            Size = new Size2D(200, 120);
            InitializePorts();
        }

        [Display(Name = "聚合模式", GroupName = "判定", Order = 1, Description = "对直连上游最近一次执行结果的统计方式。")]
        [JsonConverter(typeof(StringEnumConverter))]
        public UpstreamTestAggregationMode AggregationMode { get; set; } = UpstreamTestAggregationMode.AllStrictPass;

        [Display(Name = "跳过项处理", GroupName = "判定", Order = 2, Description = "上游节点结果为「跳过」时如何参与统计。")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SkippedUpstreamTreatment SkippedTreatment { get; set; } = SkippedUpstreamTreatment.ExcludeFromAggregation;

        [Display(Name = "要求上游均已执行", GroupName = "判定", Order = 3, Description = "若勾选，任一上游缺少 LastExecutionResult 时本节点直接失败。")]
        public bool RequireEachUpstreamExecuted { get; set; } = true;

        /// <inheritdoc />
        public bool IsPropertyVisible(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return true;
            if (string.Equals(propertyName, nameof(IncludeInTestReport), StringComparison.Ordinal))
                return false;
            return true;
        }

        private void InitializePorts()
        {
            AddInputPort(new Port
            {
                Name = "In",
                DisplayName = "输入",
                Type = PortType.Data,
                Direction = PortDirection.Input,
                AllowMultipleConnections = true,
                Description = "连接需参与统计的上游节点（可多条入边）"
            });

            AddInputPort(new Port
            {
                Name = "FlowIn",
                DisplayName = "流程输入",
                Type = PortType.Flow,
                Direction = PortDirection.Input,
                AllowMultipleConnections = true,
                Description = "流程入边（可多条）"
            });

            AddOutputPort(new Port
            {
                Name = "Out",
                DisplayName = "输出",
                Type = PortType.Data,
                Direction = PortDirection.Output,
                AllowMultipleConnections = true,
                Description = "判定结果输出"
            });

            AddOutputPort(new Port
            {
                Name = "FlowOut",
                DisplayName = "流程输出",
                Type = PortType.Flow,
                Direction = PortDirection.Output,
                AllowMultipleConnections = true,
                Description = "流程继续"
            });
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"上游结果聚合:{Name}");
            var workflow = context.ParentWorkFlow;
            if (workflow == null)
            {
                return Task.FromResult(ExecutionResult.Failed("未绑定父工作流，无法解析上游节点。"));
            }

            var inbound = workflow.GetInputConnections(Id);
            var upstreamIds = inbound
                .Select(c => c.SourceNodeId)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (upstreamIds.Count == 0)
            {
                return Task.FromResult(ExecutionResult.Failed("未连接任何上游节点；请将需判定的节点用连线连至本节点。"));
            }

            var snapshots = new List<(string Id, string? Name, ExecutionResult? Result)>(upstreamIds.Count);
            foreach (var uid in upstreamIds)
            {
                var n = workflow.GetNode(uid);
                snapshots.Add((uid, n?.Name, n?.LastExecutionResult));
            }

            if (RequireEachUpstreamExecuted && snapshots.Any(s => UpstreamTestResultSemantics.IsMissingResult(s.Result)))
            {
                var missing = snapshots.Where(s => UpstreamTestResultSemantics.IsMissingResult(s.Result))
                    .Select(s => string.IsNullOrWhiteSpace(s.Name) ? s.Id : $"{s.Name}({s.Id})");
                return Task.FromResult(ExecutionResult.Failed(
                    $"下列上游尚无执行结果（可能未运行或被失败策略阻断）：{string.Join("，", missing)}"));
            }

            var effective = new List<(string Id, string? Name, TriState PassFailSkip)>();
            if (AggregationMode != UpstreamTestAggregationMode.AllNoHardFailure)
            {
                foreach (var s in snapshots)
                {
                    var slot = Classify(s.Result, SkippedTreatment);
                    if (slot == TriState.Excluded)
                    {
                        continue;
                    }

                    effective.Add((s.Id, s.Name, slot));
                }

                if (effective.Count == 0)
                {
                    return Task.FromResult(ExecutionResult.Failed(
                        "没有可参与判定的上游样本（可能全部为跳过且配置为不参与，或全部被要求已执行但无结果）。"));
                }
            }

            bool ok;
            int passCount;
            int failCount;
            int sampleCount;

            if (AggregationMode == UpstreamTestAggregationMode.AllNoHardFailure)
            {
                sampleCount = snapshots.Count;
                ok = snapshots.All(s => s.Result != null && s.Result.Success);
                passCount = snapshots.Count(s => s.Result != null && UpstreamTestResultSemantics.IsStrictPass(s.Result));
                failCount = snapshots.Count(s => s.Result != null && UpstreamTestResultSemantics.IsExecutedFailure(s.Result));
            }
            else
            {
                sampleCount = effective.Count;
                passCount = effective.Count(x => x.PassFailSkip == TriState.Pass);
                failCount = effective.Count(x => x.PassFailSkip == TriState.Fail);
                ok = Evaluate(AggregationMode, effective);
            }

            var detail = string.Join("；", snapshots.Select(FormatSnapshot));
            var msg = ok
                ? $"聚合条件满足 [{AggregationMode}]，样本数={sampleCount}（严格通过={passCount}，硬失败={failCount}）。{detail}"
                : $"聚合条件不满足 [{AggregationMode}]，样本数={sampleCount}（严格通过={passCount}，硬失败={failCount}）。{detail}";

            log.Info(msg);

            var outputs = new Dictionary<string, object>
            {
                ["Logic.ConditionMet"] = ok,
                ["Logic.AggregationMode"] = AggregationMode.ToString(),
                ["Logic.UpstreamTotal"] = upstreamIds.Count,
                ["Logic.SampleCount"] = sampleCount,
                ["Logic.PassCount"] = passCount,
                ["Logic.FailCount"] = failCount
            };

            return Task.FromResult(ok
                ? ExecutionResult.Successful(msg, outputs)
                : ExecutionResult.Failed(msg).WithOutputs(outputs));
        }

        private static string FormatSnapshot((string Id, string? Name, ExecutionResult? Result) s)
        {
            var label = string.IsNullOrWhiteSpace(s.Name) ? s.Id : $"{s.Name}";
            if (s.Result == null)
            {
                return $"{label}:无结果";
            }

            if (s.Result.IsSkipped)
            {
                return $"{label}:跳过";
            }

            return s.Result.Success ? $"{label}:成功" : $"{label}:失败({s.Result.ResultType})";
        }

        private enum TriState
        {
            Excluded,
            Pass,
            Fail
        }

        private static TriState Classify(ExecutionResult? r, SkippedUpstreamTreatment skipMode)
        {
            if (UpstreamTestResultSemantics.IsMissingResult(r))
            {
                // RequireEachUpstreamExecuted 已前置处理；此处保守视为失败样本
                return TriState.Fail;
            }

            if (UpstreamTestResultSemantics.IsSkippedResult(r))
            {
                return skipMode switch
                {
                    SkippedUpstreamTreatment.ExcludeFromAggregation => TriState.Excluded,
                    SkippedUpstreamTreatment.TreatAsPass => TriState.Pass,
                    SkippedUpstreamTreatment.TreatAsFailure => TriState.Fail,
                    _ => TriState.Excluded
                };
            }

            return UpstreamTestResultSemantics.IsStrictPass(r) ? TriState.Pass : TriState.Fail;
        }

        private static bool Evaluate(UpstreamTestAggregationMode mode, List<(string Id, string? Name, TriState Slot)> effective)
        {
            var anyPass = effective.Any(x => x.Slot == TriState.Pass);
            var anyFail = effective.Any(x => x.Slot == TriState.Fail);
            var allPass = effective.All(x => x.Slot == TriState.Pass);
            var allFail = effective.All(x => x.Slot == TriState.Fail);

            return mode switch
            {
                UpstreamTestAggregationMode.AllStrictPass => allPass,
                UpstreamTestAggregationMode.AllExecutedFailure => allFail,
                UpstreamTestAggregationMode.AnyStrictPass => anyPass,
                UpstreamTestAggregationMode.AnyExecutedFailure => anyFail,
                UpstreamTestAggregationMode.AllNoHardFailure => false, // 在 ExecuteCoreAsync 中单独处理
                _ => false
            };
        }
    }
}

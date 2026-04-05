using System.Collections.Generic;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Geometry;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PortDirection = Astra.Core.Nodes.Models.PortDirection;

namespace Astra.Plugins.Logic.Nodes
{
    /// <summary>
    /// 精确延时（基于 <see cref="Task.Delay"/>，受系统定时器分辨率影响；记录配置值与 Stopwatch 实测）。
    /// 用于主流程/子流程编排；主流程画布上使用与流程引用块相同的外框样式。
    /// </summary>
    public sealed class MasterWorkflowDelayNode : Node
    {
        public const int MaxDelayMilliseconds = 86_400_000; // 24h

        public MasterWorkflowDelayNode()
        {
            NodeType = nameof(MasterWorkflowDelayNode);
            Name = "精确延时";
            Icon = "Clock";
            Size = new Size2D(200, 120);
            InitializePorts();
        }

        [Display(Name = "延时 (毫秒)", GroupName = "时间", Order = 1,
            Description = "等待时长，0 表示不等待。最大 86400000（24 小时）。实际唤醒精度受操作系统定时器影响（常见约 15ms 量级）。")]
        [Range(0, MaxDelayMilliseconds)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int DelayMilliseconds { get; set; } = 1000;

        private void InitializePorts()
        {
            AddInputPort(new Port
            {
                Name = "FlowIn",
                DisplayName = "流程输入",
                Type = PortType.Flow,
                Direction = PortDirection.Input,
                AllowMultipleConnections = true,
                Description = "流程入边"
            });

            AddOutputPort(new Port
            {
                Name = "FlowOut",
                DisplayName = "流程输出",
                Type = PortType.Flow,
                Direction = PortDirection.Output,
                AllowMultipleConnections = true,
                Description = "延时结束后流程继续"
            });

            AddInputPort(new Port
            {
                Name = "In",
                DisplayName = "输入",
                Type = PortType.Flow,
                Direction = PortDirection.Input,
                AllowMultipleConnections = true,
                Description = "入边"
            });

            AddOutputPort(new Port
            {
                Name = "Out",
                DisplayName = "输出",
                Type = PortType.Flow,
                Direction = PortDirection.Output,
                AllowMultipleConnections = true,
                Description = "延时结束后继续"
            });
        }

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var ms = DelayMilliseconds;
            if (ms < 0)
                ms = 0;
            if (ms > MaxDelayMilliseconds)
                ms = MaxDelayMilliseconds;

            if (ms == 0)
                return ExecutionResult.Successful("延时为 0ms，未等待。");

            var log = context.CreateExecutionLogger($"延时:{Name}");
            log.Info($"开始等待 {ms} ms");

            var executionController = context?.GetMetadata<IWorkflowExecutionController>(
                ExecutionContextMetadataKeys.WorkflowExecutionController);

            var sw = Stopwatch.StartNew();
            try
            {
                var remaining = ms;
                const int sliceMs = 100;
                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (executionController != null)
                    {
                        await executionController.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                    }

                    var step = remaining < sliceMs ? remaining : sliceMs;
                    await Task.Delay(step, cancellationToken).ConfigureAwait(false);
                    remaining -= step;
                }
            }
            catch (OperationCanceledException)
            {
                return ExecutionResult.Cancel("延时已取消。");
            }
            finally
            {
                sw.Stop();
            }

            var actualMs = sw.Elapsed.TotalMilliseconds;
            var msg = $"延时完成：配置 {ms} ms，实测约 {actualMs:F1} ms";
            log.Info(msg);

            var outputs = new Dictionary<string, object>
            {
                ["Logic.DelayConfiguredMs"] = ms,
                ["Logic.DelayElapsedMs"] = actualMs
            };

            return ExecutionResult.Successful(msg, outputs);
        }
    }
}

using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Triggers;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Services.Home
{
    /// <summary>
    /// 基于当前软件配置，将触发器配置 Id 解析为 DUT 绑定的主流程脚本路径。
    /// </summary>
    public sealed class SoftwareConfigAutoTriggerWorkflowResolver : ISoftwareConfigAutoTriggerWorkflowResolver
    {
        private readonly IConfigurationManager _configurationManager;

        public SoftwareConfigAutoTriggerWorkflowResolver(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager;
        }

        public async Task<AutoTriggerWorkflowResolveResult> ResolveAsync(string triggerConfigId, CancellationToken cancellationToken = default)
        {
            var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
            if (all?.Success != true || all.Data == null)
            {
                return new AutoTriggerWorkflowResolveResult
                {
                    ShouldExecute = false,
                    SkipMessage = "自动触发：无法读取软件配置，已跳过。"
                };
            }

            var latest = all.Data
                .OfType<SoftwareConfig>()
                .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (latest == null)
            {
                return new AutoTriggerWorkflowResolveResult
                {
                    ShouldExecute = false,
                    SkipMessage = "自动触发：未找到软件配置，已跳过。"
                };
            }

            if (latest.EnableHomeSequenceLinkage)
            {
                return new AutoTriggerWorkflowResolveResult
                {
                    ShouldExecute = false,
                    SkipMessage = "已启用 Home 与序列联动，自动触发不执行独立脚本。请关闭联动或使用序列界面运行。"
                };
            }

            var dut = latest.Duts?
                .FirstOrDefault(d => d != null && string.Equals(d.TriggerConfigId, triggerConfigId, StringComparison.Ordinal));

            if (dut == null)
            {
                return new AutoTriggerWorkflowResolveResult
                {
                    ShouldExecute = false,
                    SkipMessage = $"自动触发：未找到绑定触发器 ({triggerConfigId}) 的 DUT，已跳过。"
                };
            }

            var path = dut.WorkflowId?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new AutoTriggerWorkflowResolveResult
                {
                    ShouldExecute = false,
                    SkipMessage = $"自动触发：{dut.Name} 未配置主流程脚本路径。",
                    LogSkipAsError = true
                };
            }

            return new AutoTriggerWorkflowResolveResult
            {
                ShouldExecute = true,
                MasterWorkflowFilePath = path
            };
        }
    }
}

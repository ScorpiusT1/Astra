using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Logic
{
    /// <summary>对多个直连上游节点最近一次 <see cref="Astra.Core.Nodes.Models.ExecutionResult"/> 的聚合方式。</summary>
    public enum UpstreamTestAggregationMode
    {
        [Display(Name = "全部严格通过", Description = "每个参与统计的上游均为「测试通过」（Success 且非跳过）。")]
        AllStrictPass = 0,

        [Display(Name = "全部已执行失败", Description = "每个参与统计的上游均为「已执行且失败」（非 Success 且非跳过）。")]
        AllExecutedFailure = 1,

        [Display(Name = "任一严格通过", Description = "至少一个上游为「测试通过」。")]
        AnyStrictPass = 2,

        [Display(Name = "任一已执行失败", Description = "至少一个上游为「已执行且失败」。")]
        AnyExecutedFailure = 3,

        [Display(Name = "全部无硬失败", Description = "每个上游均有结果且 Success（跳过视为 Success，仍计入；缺少结果或未成功则不满足）。")]
        AllNoHardFailure = 4
    }

    /// <summary>上游为「跳过」时在聚合中的角色。</summary>
    public enum SkippedUpstreamTreatment
    {
        [Display(Name = "不参与判定", Description = "跳过的上游从 All/Any 统计中剔除；若剔除后无样本则判定失败。")]
        ExcludeFromAggregation = 0,

        [Display(Name = "视为通过", Description = "跳过当作严格通过参与 AllStrictPass / AnyStrictPass；在 AllExecutedFailure 中不满足「失败」。")]
        TreatAsPass = 1,

        [Display(Name = "视为失败", Description = "跳过当作已执行失败参与 AllExecutedFailure / AnyExecutedFailure。")]
        TreatAsFailure = 2
    }
}

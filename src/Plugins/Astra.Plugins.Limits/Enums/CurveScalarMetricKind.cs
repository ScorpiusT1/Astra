using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Limits.Enums
{
    /// <summary>曲线标量统计指标（单选）。</summary>
    public enum CurveScalarMetricKind
    {
        [Display(Name = "均值", Description = "算术平均值 mean(Y)")]
        Mean = 0,

        [Display(Name = "最大值", Description = "max(Y)")]
        Max = 1,

        [Display(Name = "最小值", Description = "min(Y)")]
        Min = 2,

        [Display(Name = "峰值（绝对值）", Description = "max(|Y|)")]
        PeakAbsolute = 3,

        [Display(Name = "峰峰值", Description = "max(Y) − min(Y)")]
        PeakToPeak = 4,

        [Display(Name = "RMS", Description = "sqrt(mean(Y²))")]
        Rms = 5,

        [Display(Name = "峭度（超额）", Description = "样本超额峭度，正态分布为 0；样本数需 ≥4")]
        KurtosisExcess = 6
    }
}

using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Limits.Enums
{
    /// <summary>
    /// 组合卡控节点：要执行哪些检查。
    /// </summary>
    public enum LimitCheckMode
    {
        [Display(Name = "只检查数值")]
        ValueOnly = 0,

        [Display(Name = "只检查曲线")]
        CurveOnly = 1,

        [Display(Name = "数值与曲线都检查")]
        Both = 2,
    }
}

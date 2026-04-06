using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>曲线逐点卡控时样本来源：自动按上游是否有可用图表工件切换，或强制 Raw / 强制图表。</summary>
    public enum CurveLimitSampleSource
    {
        [Display(Name = "自动",
            Description = "若上游节点输出有效图表工件（如算法频谱）且能解析出曲线，则卡控该曲线；否则从采集/导入等 Raw 链路读取时域数据。")]
        Auto = 0,

        [Display(Name = "仅原始数据", Description = "仅从采集卡、文件导入等 Raw 链路读取 NVH 曲线，忽略上游图表。")]
        RawOnly = 1,

        [Display(Name = "仅上游图表", Description = "仅从上游节点输出的图表工件读取曲线（如自功率谱的 Y、时域图的 SignalY），不读 Raw。")]
        ChartOnly = 2
    }
}

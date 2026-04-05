using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>椭圆（Cauer）高通（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class EllipticHighPassFilterNode : IirFilterNodeBase
    {
        public EllipticHighPassFilterNode() : base(nameof(EllipticHighPassFilterNode), "椭圆 高通")
        {
        }

        [Display(Name = "通带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double PassbandRippleDb { get; set; } = 1;

        [Display(Name = "阻带衰减 (dB)", GroupName = "滤波", Order = 2)]
        public double StopbandAttenuationDb { get; set; } = 20;

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 3)]
        public double CutoffHz { get; set; } = 100;

        protected override string ChartArtifactName => "EllipticHighPassFiltered";

        protected override string ResultTag => "filter-elliptic-highpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwEllipticFilter.Apply(samples, samplingRate, ButterworthFilterKind.HighPass, order, CutoffHz, null, PassbandRippleDb, StopbandAttenuationDb);
    }
}

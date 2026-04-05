using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>椭圆（Cauer）低通（NWaves，最窄过渡带）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class EllipticLowPassFilterNode : IirFilterNodeBase
    {
        public EllipticLowPassFilterNode() : base(nameof(EllipticLowPassFilterNode), "椭圆 低通")
        {
        }

        [Display(Name = "通带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double PassbandRippleDb { get; set; } = 1;

        [Display(Name = "阻带衰减 (dB)", GroupName = "滤波", Order = 2)]
        public double StopbandAttenuationDb { get; set; } = 20;

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 3)]
        public double CutoffHz { get; set; } = 1000;

        protected override string ChartArtifactName => "EllipticLowPassFiltered";

        protected override string ResultTag => "filter-elliptic-lowpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwEllipticFilter.Apply(samples, samplingRate, ButterworthFilterKind.LowPass, order, CutoffHz, null, PassbandRippleDb, StopbandAttenuationDb);
    }
}

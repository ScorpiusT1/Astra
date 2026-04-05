using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>椭圆（Cauer）带通（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class EllipticBandPassFilterNode : IirFilterNodeBase
    {
        public EllipticBandPassFilterNode() : base(nameof(EllipticBandPassFilterNode), "椭圆 带通")
        {
        }

        [Display(Name = "通带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double PassbandRippleDb { get; set; } = 1;

        [Display(Name = "阻带衰减 (dB)", GroupName = "滤波", Order = 2)]
        public double StopbandAttenuationDb { get; set; } = 20;

        [Display(Name = "低频截止 (Hz)", GroupName = "滤波", Order = 3)]
        public double LowHz { get; set; } = 500;

        [Display(Name = "高频截止 (Hz)", GroupName = "滤波", Order = 4)]
        public double HighHz { get; set; } = 3000;

        protected override string ChartArtifactName => "EllipticBandPassFiltered";

        protected override string ResultTag => "filter-elliptic-bandpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwEllipticFilter.Apply(samples, samplingRate, ButterworthFilterKind.BandPass, order, LowHz, HighHz, PassbandRippleDb, StopbandAttenuationDb);
    }
}

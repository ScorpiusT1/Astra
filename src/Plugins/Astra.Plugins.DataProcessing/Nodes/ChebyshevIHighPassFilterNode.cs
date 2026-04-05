using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Chebyshev I 高通（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class ChebyshevIHighPassFilterNode : IirFilterNodeBase
    {
        public ChebyshevIHighPassFilterNode() : base(nameof(ChebyshevIHighPassFilterNode), "Chebyshev I 高通")
        {
        }

        [Display(Name = "通带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double PassbandRippleDb { get; set; } = 0.1;

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 2)]
        public double CutoffHz { get; set; } = 100;

        protected override string ChartArtifactName => "ChebyshevIHighPassFiltered";

        protected override string ResultTag => "filter-cheby1-highpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwChebyshevIFilter.Apply(samples, samplingRate, ButterworthFilterKind.HighPass, order, CutoffHz, null, PassbandRippleDb);
    }
}

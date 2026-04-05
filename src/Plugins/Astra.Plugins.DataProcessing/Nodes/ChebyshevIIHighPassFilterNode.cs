using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Chebyshev II 高通（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class ChebyshevIIHighPassFilterNode : IirFilterNodeBase
    {
        public ChebyshevIIHighPassFilterNode() : base(nameof(ChebyshevIIHighPassFilterNode), "Chebyshev II 高通")
        {
        }

        [Display(Name = "阻带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double StopbandRippleDb { get; set; } = 0.1;

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 2)]
        public double CutoffHz { get; set; } = 100;

        protected override string ChartArtifactName => "ChebyshevIIHighPassFiltered";

        protected override string ResultTag => "filter-cheby2-highpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwChebyshevIIFilter.Apply(samples, samplingRate, ButterworthFilterKind.HighPass, order, CutoffHz, null, StopbandRippleDb);
    }
}

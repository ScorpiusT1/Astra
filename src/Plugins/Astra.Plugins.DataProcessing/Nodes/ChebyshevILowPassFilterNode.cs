using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Chebyshev I 低通（NWaves，通带等纹波）。</summary>
    public sealed class ChebyshevILowPassFilterNode : IirFilterNodeBase
    {
        public ChebyshevILowPassFilterNode() : base(nameof(ChebyshevILowPassFilterNode), "Chebyshev I 低通")
        {
        }

        [Display(Name = "通带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double PassbandRippleDb { get; set; } = 0.1;

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 2)]
        public double CutoffHz { get; set; } = 1000;

        protected override string ChartArtifactName => "ChebyshevILowPassFiltered";

        protected override string ResultTag => "filter-cheby1-lowpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwChebyshevIFilter.Apply(samples, samplingRate, ButterworthFilterKind.LowPass, order, CutoffHz, null, PassbandRippleDb);
    }
}

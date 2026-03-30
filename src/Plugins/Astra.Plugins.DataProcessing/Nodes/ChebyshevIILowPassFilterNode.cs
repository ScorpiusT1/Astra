using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Chebyshev II 低通（NWaves，阻带等纹波）。</summary>
    public sealed class ChebyshevIILowPassFilterNode : IirFilterNodeBase
    {
        public ChebyshevIILowPassFilterNode() : base(nameof(ChebyshevIILowPassFilterNode), "Chebyshev II 低通")
        {
        }

        [Display(Name = "阻带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double StopbandRippleDb { get; set; } = 0.1;

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 2)]
        public double CutoffHz { get; set; } = 1000;

        protected override string ChartArtifactName => "ChebyshevIILowPassFiltered";

        protected override string ResultTag => "filter-cheby2-lowpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwChebyshevIIFilter.Apply(samples, samplingRate, ButterworthFilterKind.LowPass, order, CutoffHz, null, StopbandRippleDb);
    }
}

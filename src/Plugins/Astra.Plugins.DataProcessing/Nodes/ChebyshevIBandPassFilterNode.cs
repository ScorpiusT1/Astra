using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Chebyshev I 带通（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class ChebyshevIBandPassFilterNode : IirFilterNodeBase
    {
        public ChebyshevIBandPassFilterNode() : base(nameof(ChebyshevIBandPassFilterNode), "Chebyshev I 带通")
        {
        }

        [Display(Name = "通带纹波 (dB)", GroupName = "滤波", Order = 1)]
        public double PassbandRippleDb { get; set; } = 0.1;

        [Display(Name = "低频截止 (Hz)", GroupName = "滤波", Order = 2)]
        public double LowHz { get; set; } = 500;

        [Display(Name = "高频截止 (Hz)", GroupName = "滤波", Order = 3)]
        public double HighHz { get; set; } = 3000;

        protected override string ChartArtifactName => "ChebyshevIBandPassFiltered";

        protected override string ResultTag => "filter-cheby1-bandpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwChebyshevIFilter.Apply(samples, samplingRate, ButterworthFilterKind.BandPass, order, LowHz, HighHz, PassbandRippleDb);
    }
}

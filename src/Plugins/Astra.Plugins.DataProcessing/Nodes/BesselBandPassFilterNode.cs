using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Bessel 带通（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class BesselBandPassFilterNode : IirFilterNodeBase
    {
        public BesselBandPassFilterNode() : base(nameof(BesselBandPassFilterNode), "Bessel 带通")
        {
        }

        [Display(Name = "低频截止 (Hz)", GroupName = "滤波", Order = 1)]
        public double LowHz { get; set; } = 500;

        [Display(Name = "高频截止 (Hz)", GroupName = "滤波", Order = 2)]
        public double HighHz { get; set; } = 3000;

        protected override string ChartArtifactName => "BesselBandPassFiltered";

        protected override string ResultTag => "filter-bessel-bandpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwBesselFilter.Apply(samples, samplingRate, ButterworthFilterKind.BandPass, order, LowHz, HighHz);
    }
}

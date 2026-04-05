using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Bessel 低通（NWaves，最平坦群时延）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class BesselLowPassFilterNode : IirFilterNodeBase
    {
        public BesselLowPassFilterNode() : base(nameof(BesselLowPassFilterNode), "Bessel 低通")
        {
        }

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 1)]
        public double CutoffHz { get; set; } = 1000;

        protected override string ChartArtifactName => "BesselLowPassFiltered";

        protected override string ResultTag => "filter-bessel-lowpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwBesselFilter.Apply(samples, samplingRate, ButterworthFilterKind.LowPass, order, CutoffHz, null);
    }
}

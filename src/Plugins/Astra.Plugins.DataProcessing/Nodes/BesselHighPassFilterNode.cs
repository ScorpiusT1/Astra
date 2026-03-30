using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Bessel 高通（NWaves）。</summary>
    public sealed class BesselHighPassFilterNode : IirFilterNodeBase
    {
        public BesselHighPassFilterNode() : base(nameof(BesselHighPassFilterNode), "Bessel 高通")
        {
        }

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 1)]
        public double CutoffHz { get; set; } = 100;

        protected override string ChartArtifactName => "BesselHighPassFiltered";

        protected override string ResultTag => "filter-bessel-highpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwBesselFilter.Apply(samples, samplingRate, ButterworthFilterKind.HighPass, order, CutoffHz, null);
    }
}

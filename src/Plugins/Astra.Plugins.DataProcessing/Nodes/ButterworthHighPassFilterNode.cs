using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Butterworth 高通滤波（NWaves）。</summary>
    public sealed class ButterworthHighPassFilterNode : IirFilterNodeBase
    {
        public ButterworthHighPassFilterNode() : base(nameof(ButterworthHighPassFilterNode), "Butterworth 高通")
        {
        }

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 1)]
        public double CutoffHz { get; set; } = 100;

        protected override string ChartArtifactName => "ButterworthHighPassFiltered";

        protected override string ResultTag => "filter-highpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwButterworthFilter.Apply(samples, samplingRate, ButterworthFilterKind.HighPass, order, CutoffHz, null);
    }
}

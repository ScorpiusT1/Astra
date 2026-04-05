using Astra.Plugins.DataProcessing.Enums;
using Astra.Plugins.DataProcessing.Filters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>Butterworth 低通滤波（NWaves）；与同基类共享多设备、多通道 Raw 管道。</summary>
    public sealed class ButterworthLowPassFilterNode : IirFilterNodeBase
    {
        public ButterworthLowPassFilterNode() : base(nameof(ButterworthLowPassFilterNode), "Butterworth 低通")
        {
        }

        [Display(Name = "截止频率 (Hz)", GroupName = "滤波", Order = 1)]
        public double CutoffHz { get; set; } = 1000;

        protected override string ChartArtifactName => "ButterworthLowPassFiltered";

        protected override string ResultTag => "filter-lowpass";

        protected override double[] ApplyFilter(double[] samples, int samplingRate, int order) =>
            NwButterworthFilter.Apply(samples, samplingRate, ButterworthFilterKind.LowPass, order, CutoffHz, null);
    }
}

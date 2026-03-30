using Astra.Plugins.DataProcessing.Enums;
using NWaves.Filters.Bessel;

namespace Astra.Plugins.DataProcessing.Filters
{
    /// <summary>NWaves Bessel IIR（最平坦群时延，归一化 f/fs）。</summary>
    internal static class NwBesselFilter
    {
        public static double[] Apply(
            double[] samples,
            int samplingRate,
            ButterworthFilterKind kind,
            int order,
            double cutoffOrLowHz,
            double? highHz)
        {
            if (samples.Length == 0)
                return samples;
            if (samplingRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(samplingRate));
            if (order < 1 || order > 16)
                throw new ArgumentOutOfRangeException(nameof(order));

            var ny = samplingRate * 0.5;
            var input = NwSignalFilterUtil.ToDiscreteSignal(samples, samplingRate);

            switch (kind)
            {
                case ButterworthFilterKind.LowPass:
                    NwIirFilterValidation.ValidateHz(cutoffOrLowHz, ny);
                    {
                        var f = cutoffOrLowHz / samplingRate;
                        var filter = new LowPassFilter(f, order);
                        return NwSignalFilterUtil.ToDoubleArray(filter.ApplyTo(input));
                    }
                case ButterworthFilterKind.HighPass:
                    NwIirFilterValidation.ValidateHz(cutoffOrLowHz, ny);
                    {
                        var f = cutoffOrLowHz / samplingRate;
                        var filter = new HighPassFilter(f, order);
                        return NwSignalFilterUtil.ToDoubleArray(filter.ApplyTo(input));
                    }
                case ButterworthFilterKind.BandPass:
                    if (highHz == null)
                        throw new ArgumentException("带通需要高频截止。");
                    NwIirFilterValidation.ValidateHz(cutoffOrLowHz, ny);
                    NwIirFilterValidation.ValidateHz(highHz.Value, ny);
                    if (cutoffOrLowHz >= highHz.Value)
                        throw new ArgumentException("带通低频须小于高频。");
                    {
                        var f1 = cutoffOrLowHz / samplingRate;
                        var f2 = highHz.Value / samplingRate;
                        var filter = new BandPassFilter(f1, f2, order);
                        return NwSignalFilterUtil.ToDoubleArray(filter.ApplyTo(input));
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}

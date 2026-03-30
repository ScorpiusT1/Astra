using NWaves.Signals;

namespace Astra.Plugins.DataProcessing.Filters
{
    internal static class NwSignalFilterUtil
    {
        public static DiscreteSignal ToDiscreteSignal(double[] samples, int samplingRate)
        {
            var floatSamples = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
                floatSamples[i] = (float)samples[i];
            return new DiscreteSignal(samplingRate, floatSamples);
        }

        public static double[] ToDoubleArray(DiscreteSignal signal)
        {
            var s = signal.Samples;
            if (s == null || s.Length == 0)
                return Array.Empty<double>();
            var o = new double[s.Length];
            for (var i = 0; i < s.Length; i++)
                o[i] = s[i];
            return o;
        }
    }
}

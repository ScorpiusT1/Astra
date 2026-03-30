namespace Astra.Plugins.DataProcessing.Filters
{
    internal static class NwIirFilterValidation
    {
        public static void ValidateHz(double hz, double nyquist)
        {
            if (hz <= 0 || hz >= nyquist)
                throw new ArgumentOutOfRangeException(nameof(hz), $"截止频率须在 (0, {nyquist:0.###}) Hz 内。");
        }
    }
}

using Astra.Workflow.AlgorithmChannel.APIs;

namespace Astra.Workflow.AlgorithmChannel.Helpers
{
    /// <summary>
    /// 与 NVH 算法刻度配套的默认参考值：切换 dB / Linear 时应同步调整参考值。
    /// </summary>
    public static class AlgorithmScaleReferenceDefaults
    {
        /// <summary>dB 刻度下的参考值（与工程约定 20/1e6 一致）。</summary>
        public const double ForDb = 20.0 / 1_000_000;

        /// <summary>线性刻度下的参考值。</summary>
        public const double ForLinear = 1.0;

        public static double ForScale(Scale scale) => scale == Scale.dB ? ForDb : ForLinear;
    }
}

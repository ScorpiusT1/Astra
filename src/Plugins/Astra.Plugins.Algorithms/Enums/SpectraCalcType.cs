using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    /// <summary>
    /// 指定用于频谱计算的参数类型。
    /// </summary>
    /// <remarks>
    /// 此枚举用于描述计算频谱时所使用的主导参数类型：
    /// <list type="bullet">
    /// <item><description><see cref="Resolution"/> — 以目标频率分辨率来确定窗长/FFT 点数。</description></item>
    /// <item><description><see cref="FrameLength"/> — 以帧长度来确定分析窗的长度（通常以秒或样本为单位）。</description></item>
    /// <item><description><see cref="SpectrumLines"/> — 直接指定谱线（FFT 点）数量。</description></item>
    /// </list>
    /// 不同的计算选项将被转换为实际的谱线数或时间长度以供底层本机库使用。
    /// </remarks>
    public enum SpectraCalcType
    {
        /// <summary>
        /// 以目标频率分辨率来确定计算参数（例如：Hz 为单位的分辨率）。
        /// </summary>
        Resolution,

        /// <summary>
        /// 以帧长度来确定计算参数（例如：指定帧长度以秒或样本为单位）。
        /// </summary>
        FrameLength,

        /// <summary>
        /// 直接指定谱线数（FFT 点数或频谱线数量）。
        /// </summary>
        SpectrumLines,
    }
}

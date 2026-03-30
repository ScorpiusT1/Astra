using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    public class EnvelopeExOptions
    {
        public BandType Type { get; } = BandType.Fixed;

        // 固定带参数
        // 1. 中心频率
        public double CenterFrequency { get; }
        // 2. 带宽
        public double BandwidthFixed { get; }

        // 跟踪带参数
        // 1. 中心阶次
        // fc = (rpm / 60) * order
        public double CenterOrder { get; }
        // 2. 滑动窗口长度
        public int WindowLength { get; }
        // 3. 跟踪带全局最小频率
        // 当频率低于该值时，置零
        public double MinFrequency { get; }
        // 4. 跟踪带全局最大频率
        // 当频率高于该值时，置零
        public double MaxFrequency { get; }
        // 5. 参考转速
        public double[] Rpm { get; } 

        public EnvelopeExOptions(double bandWidth, double centerFrequency)
        {
            if (bandWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(bandWidth), "带宽必须大于零。");
            if (centerFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(centerFrequency), "中心频率必须大于零。");

            Type = BandType.Fixed;
            BandwidthFixed = bandWidth;
            CenterFrequency = centerFrequency;
        }

        public EnvelopeExOptions(double centerOrder, double bandwidth, int windowLength, double minFreq, double maxFreq, double[] trackingRpm)
        {
            if (bandwidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(bandwidth), "带宽必须大于零。");
            if (minFreq <= 0 || maxFreq <= 0)
                throw new ArgumentOutOfRangeException("频率必须大于零。");
            if (minFreq >= maxFreq)
                throw new ArgumentException("最小频率必须小于最大频率。");
            if (centerOrder <= 0)
                throw new ArgumentOutOfRangeException(nameof(centerOrder), "中心阶次必须大于零。");
            if (windowLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowLength), "滑动窗口长度必须大于零。");

            Type = BandType.Tracked;
            BandwidthFixed = bandwidth;
            CenterOrder = centerOrder;
            WindowLength = windowLength;
            MinFrequency = minFreq;
            MaxFrequency = maxFreq;
            Rpm = trackingRpm;
        }

        public enum BandType
        {
            Fixed,
            Tracked
        }
    }
}

using Astra.Plugins.Algorithms.APIs;
using Astra.Workflow.AlgorithmChannel.APIs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.Enums
{
    /// <summary>
    /// 缩放选项配置类，用于定义数据输出的缩放方式和参考值
    /// </summary>
    public class ScaleOptions
    {
        public ScaleOptions(Scale scaleType, double referenceValue)
        {
            Scale = scaleType;
            ReferenceValue = referenceValue;
        }
        /// <summary>
        /// 缩放类型，用于转换输出格式为 dB 或 Lin
        /// </summary>
        public Scale Scale { get; }

        /// <summary>
        /// 参考值，默认为 1.0。
        /// 仅在 Scale 为 dB 时生效
        /// </summary>
        public double ReferenceValue { get; }
    }
}

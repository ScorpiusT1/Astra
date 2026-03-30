using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    /// <summary>
    /// 表示谱（Spectra）步长的选项。包含步长类型与对应的数值，用于指定在谱分析或扫描中步进的方式与大小。
    /// </summary>
    /// <param name="stepType">步长的类型，参见 <see cref="SpectraStepType"/>。例如固定步长或相对步长等。</param>
    /// <param name="stepValue">步长的数值，含义依赖于 <paramref name="stepType"/>（例如频率增量、百分比等）。使用时需根据 <see cref="SpectraStepType"/> 解释该值的单位。</param>
    public class SpectraStepOptions
    {
        public SpectraStepOptions(SpectraStepType stepType, double stepValue)
        {
            StepType = stepType; 
            StepValue = stepValue;
        }
        /// <summary>
        /// 获取步长类型，指示步长的计算或应用方式（例如固定步长、相对步长等）。
        /// </summary>
        public SpectraStepType StepType { get; } 

        /// <summary>
        /// 获取步长的数值。具体含义和单位依赖于 <see cref="StepType"/>。
        /// </summary>
        public double StepValue { get; }
    }
}

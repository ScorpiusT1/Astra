using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    /// <summary>
    /// 表示用于谱线计算的选项集合。
    /// </summary>
    /// <remarks>
    /// 该类使用主构造函数接收必要的计算类型和计算值，并将其暴露为只读属性。
    /// 设计为不可变对象，方便在多线程或函数式风格中安全传递。
    /// </remarks>
    /// <param name="calcType">指定谱线计算的类型。</param>
    /// <param name="calcValue">与 <paramref name="calcType"/> 一起使用的数值参数（例如阈值或因子）。</param>
    public class SpectraCalcOptions
    {
        public SpectraCalcOptions(SpectraCalcType calcType, double calcValue)
        {
            CalcType = calcType;
            CalcValue = calcValue;
        }
        /// <summary>
        /// 获取用于确定谱线计算方式的枚举值。
        /// </summary>
        /// <value>参见 <see cref="NvhLibCSharp.Enums.SpectraCalcType"/>。</value>
        public SpectraCalcType CalcType { get; } 

        /// <summary>
        /// 获取用于谱线计算的数值参数。
        /// </summary>
        /// <value>含义取决于 <see cref="CalcType"/>，例如阈值或缩放因子。</value>
        public double CalcValue { get; }
    }
}

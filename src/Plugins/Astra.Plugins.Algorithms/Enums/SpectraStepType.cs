using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    /// <summary>
    /// 表示在频谱处理或扫描过程中步进（step）方式的枚举。
    /// </summary>
    /// <remarks>
    /// 该枚举用于指定在生成或处理谱图时如何前进频率/波长范围：
    /// - <see cref="Overlap"/>: 使用重叠窗口进行步进，以保证邻近段之间有交叠区域用于拼接或平滑。
    /// - <see cref="Increment"/>: 以固定的增量进行步进，用于非重叠分段采样或测量。
    /// </remarks>
    public enum SpectraStepType
    {
        /// <summary>
        /// 使用重叠率（overlap）进行步进。
        /// </summary>
        /// <remarks>
        /// 当选择此选项时，相邻的步进区间将存在重叠区域
        /// </remarks>
        Overlap,

        /// <summary>
        /// 使用固定增量（increment）进行步进。
        /// </summary>
        /// <remarks>
        /// 当选择此选项时，每一步按照固定增量前进，可能存在重叠区域。
        /// </remarks>
        Increment,
    }
}

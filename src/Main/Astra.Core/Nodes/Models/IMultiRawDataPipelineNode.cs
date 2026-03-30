using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 同时管理多个采集卡 Raw 数据的节点（如多采集节点），与 <see cref="IRawDataPipelineNode"/> 互补。
    /// </summary>
    public interface IMultiRawDataPipelineNode
    {
        /// <summary>本节点所管理的全部采集卡显示名。</summary>
        IEnumerable<string> DataAcquisitionDeviceDisplayNames { get; }
    }
}

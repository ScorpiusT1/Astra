namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 作为某采集卡 Raw 数据在流程中的直接上游（例如滤波后再次发布与多采集相同命名的 Raw 工件）。
    /// </summary>
    public interface IRawDataPipelineNode
    {
        /// <summary>与多采集节点一致的采集卡显示名，用于按设备解析 Raw 工件。</summary>
        string DataAcquisitionDeviceDisplayName { get; }
    }
}

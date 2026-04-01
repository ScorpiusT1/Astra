using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 设计时数据源信息契约。
    /// 数据采集、文件导入、数字滤波等节点实现此接口后，
    /// 下游节点（算法、卡控等）可在连线时自动获取可选的设备名与通道名，
    /// 而无需依赖全局注册表。
    /// </summary>
    public interface IDesignTimeDataSourceInfo
    {
        /// <summary>本节点对外提供的设备显示名列表。</summary>
        IEnumerable<string> GetAvailableDeviceDisplayNames();

        /// <summary>指定设备下可用的通道名列表（不含「未选择」占位项）。</summary>
        IEnumerable<string> GetAvailableChannelNames(string deviceDisplayName);
    }
}

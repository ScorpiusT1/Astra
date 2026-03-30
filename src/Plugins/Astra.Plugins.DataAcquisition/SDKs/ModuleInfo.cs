using System.Collections.Generic;
using Astra.Plugins.DataAcquisition.Configs;

namespace Astra.Plugins.DataAcquisition.SDKs
{
    /// <summary>
    /// 采集卡模块信息（公共模型，供不同SDK实现复用）
    /// </summary>
    public class ModuleInfo
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 产品名称
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 通道数
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// 增益选项
        /// </summary>
        public List<double> GainOptions { get; set; } = new List<double>();

        /// <summary>
        /// 采样率选项
        /// </summary>
        public List<double> SampleRateOptions { get; set; } = new List<double>();

        /// <summary>
        /// 电流选项
        /// </summary>
        public List<double> CurrentOptions { get; set; } = new List<double>();

        /// <summary>
        /// 耦合模式选项
        /// </summary>
        public List<CouplingMode> CouplingOptions { get; set; } = new List<CouplingMode>();
    }
}

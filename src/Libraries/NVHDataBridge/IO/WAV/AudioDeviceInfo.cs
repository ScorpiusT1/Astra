using NAudio.Wave;
using System;

namespace NVHDataBridge.IO.WAV
{
    /// <summary>
    /// 音频设备信息
    /// </summary>
    public class AudioDeviceInfo
    {
        /// <summary>
        /// 设备编号（用于选择设备）
        /// </summary>
        public int DeviceNumber { get; set; }

        /// <summary>
        /// 设备产品名称
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 设备制造商名称
        /// </summary>
        public string ManufacturerName { get; set; }

        /// <summary>
        /// 产品GUID
        /// </summary>
        public Guid ProductGuid { get; set; }

        /// <summary>
        /// 制造商GUID
        /// </summary>
        public Guid ManufacturerGuid { get; set; }

        /// <summary>
        /// 通道数
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// 设备功能
        /// </summary>
        public WaveOutCapabilities Capabilities { get; set; }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"{DeviceNumber}: {ProductName} ({ManufacturerName})";
        }
    }
}


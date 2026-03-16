using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Core.Devices.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.DataAcquisition.Providers
{
    /// <summary>
    /// 采集卡提供者：为属性编辑器提供可选的 IDataAcquisition 设备列表。
    /// 
    /// 通过 DataAcquisitionPlugin 内部维护的 _devices 列表获取当前所有已注册的采集卡实例，
    /// 确保属性编辑器中看到的是实时的设备列表，而不是重复创建新实例。
    /// </summary>
    internal static class DataAcquisitionCardProvider
    {       
        /// <summary>
        /// 返回当前插件中所有采集卡的设备名称列表（用于属性编辑器多选，绑定到字符串集合）。
        /// </summary>
        public static List<string> GetDataAcquisitionNames()
        {
            try
            {
                var plugin = DataAcquisitionPlugin.Current;
                if (plugin == null)
                {
                    return new List<string>();
                }

                var devices = plugin.GetAllDataAcquisitions();

                return devices?
                           .Select(d => d as IDevice)
                           .Where(d => d != null && !string.IsNullOrWhiteSpace(d.DeviceName))
                           .Select(d => d.DeviceName)
                           .Distinct()
                           .ToList()
                       ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Configuration.Helpers
{
    /// <summary>
    /// 配置显示名称辅助类 — 提取公共的 GetDisplayName 逻辑，消除代码重复（DRY原则）
    /// </summary>
    public static class ConfigDisplayNameHelper
    {
        /// <summary>
        /// 根据厂家、型号、序列号生成显示名称，格式：厂家 + 型号 + 序列号
        /// </summary>
        public static string BuildDisplayName(
            string manufacturer,
            string model,
            string serialNumber,
            string configName = null,
            string defaultName = "未命名配置")
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(manufacturer)) parts.Add(manufacturer);
            if (!string.IsNullOrWhiteSpace(model)) parts.Add(model);
            if (!string.IsNullOrWhiteSpace(serialNumber)) parts.Add(serialNumber);

            if (parts.Count == 0)
                return string.IsNullOrEmpty(configName) ? defaultName : configName;

            return string.Join(" ", parts);
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置显示名称辅助类
    /// 提取公共的 GetDisplayName 逻辑，消除代码重复（DRY原则）
    /// </summary>
    public static class ConfigDisplayNameHelper
    {
        /// <summary>
        /// 根据厂家、型号、序列号生成显示名称
        /// 格式：厂家 + 型号 + 编号
        /// </summary>
        /// <param name="manufacturer">厂家</param>
        /// <param name="model">型号</param>
        /// <param name="serialNumber">序列号</param>
        /// <param name="configName">配置名称（作为后备）</param>
        /// <param name="defaultName">默认名称（当所有字段都为空时使用）</param>
        /// <returns>显示名称</returns>
        public static string BuildDisplayName(
            string manufacturer,
            string model,
            string serialNumber,
            string configName = null,
            string defaultName = "未命名配置")
        {
            var parts = new List<string>();

            // 添加厂家
            if (!string.IsNullOrWhiteSpace(manufacturer))
            {
                parts.Add(manufacturer);
            }

            // 添加型号
            if (!string.IsNullOrWhiteSpace(model))
            {
                parts.Add(model);
            }

            // 添加编号（序列号）
            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                parts.Add(serialNumber);
            }

            // 如果所有部分都为空，使用 ConfigName 或默认名称作为后备
            if (parts.Count == 0)
            {
                return string.IsNullOrEmpty(configName) ? defaultName : configName;
            }

            return string.Join(" ", parts);
        }
    }
}


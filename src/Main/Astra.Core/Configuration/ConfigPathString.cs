using System;
using System.IO;
using Astra.Core.Devices.Configuration;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置路径统一管理类
    /// 提供所有配置相关的路径常量和方法
    /// </summary>
    public static class ConfigPathString
    {
        /// <summary>
        /// 配置根目录
        /// </summary>
        public static string BaseConfigDirectory => Path.Combine(AppContext.BaseDirectory, "Configs");

        /// <summary>
        /// 设备配置目录（所有设备配置统一放在此目录）
        /// </summary>
        public static string DeviceConfigDirectory => Path.Combine(BaseConfigDirectory, "Devices");

        /// <summary>
        /// 传感器配置目录（统一使用单数形式）
        /// </summary>
        public static string SensorConfigDirectory => Path.Combine(BaseConfigDirectory, "Sensor");

        /// <summary>
        /// 数据库配置目录
        /// </summary>
        public static string DatabaseConfigDirectory => Path.Combine(BaseConfigDirectory, "DataBase");

        /// <summary>
        /// 根据配置类型获取配置目录（统一入口）
        /// 优先使用明确的类型映射，然后使用约定规则
        /// </summary>
        /// <param name="configType">配置类型</param>
        /// <returns>配置目录路径</returns>
        public static string GetConfigDirectory(Type configType)
        {
            if (configType == null)
                throw new ArgumentNullException(nameof(configType));

            // 1. 特殊类型映射（明确指定）
            var typeName = configType.FullName ?? configType.Name;
            
            // 传感器配置：统一使用 Sensor 目录（单数）
            if (typeName.Contains("SensorConfig") && !typeName.Contains("SensorConfigMode"))
            {
                return SensorConfigDirectory;
            }

            // 2. 设备配置：所有 DeviceConfig 的子类统一放在 Devices 目录
            if (typeof(DeviceConfig).IsAssignableFrom(configType))
            {
                return DeviceConfigDirectory;
            }

            // 3. 默认约定：去除 Config 后缀作为目录名
            var directoryName = configType.Name.Replace("Config", "");
            return Path.Combine(BaseConfigDirectory, directoryName);
        }

        /// <summary>
        /// 确保配置目录存在
        /// </summary>
        /// <param name="configType">配置类型</param>
        public static void EnsureConfigDirectoryExists(Type configType)
        {
            var directory = GetConfigDirectory(configType);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}

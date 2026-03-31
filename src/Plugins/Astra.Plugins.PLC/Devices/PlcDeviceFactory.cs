using Astra.Core.Devices.Interfaces;
using Astra.Plugins.PLC.Configs;
using System;
using System.Collections.Generic;

namespace Astra.Plugins.PLC.Devices
{
    /// <summary>
    /// PLC 设备工厂：维护「配置类型 → 设备创建函数」映射表。
    /// <para>
    /// 新增品牌时只需在 <see cref="PlcPlugin.InitializeAsync"/> 中调用一次
    /// <see cref="Register{TConfig}"/>，其余代码无需修改。
    /// </para>
    /// <example>
    /// // 西门子 S7
    /// PlcDeviceFactory.Register&lt;S7SiemensPlcDeviceConfig&gt;(cfg => new S7SiemensPlcDevice(cfg));
    /// // 欧姆龙（示例）
    /// PlcDeviceFactory.Register&lt;OmronPlcDeviceConfig&gt;(cfg => new OmronPlcDevice(cfg));
    /// </example>
    /// </summary>
    internal static class PlcDeviceFactory
    {
        private static readonly Dictionary<Type, Func<PlcDeviceConfig, IDevice>> _registry = new();

        /// <summary>注册某一配置类型对应的设备工厂函数。</summary>
        internal static void Register<TConfig>(Func<TConfig, IDevice> factory)
            where TConfig : PlcDeviceConfig
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _registry[typeof(TConfig)] = cfg => factory((TConfig)cfg);
        }

        /// <summary>
        /// 根据配置实例的运行时类型创建对应设备。
        /// 若未注册该配置类型，返回 <c>null</c> 并跳过，不抛异常。
        /// </summary>
        internal static IDevice? Create(PlcDeviceConfig config)
        {
            if (config == null) return null;
            return _registry.TryGetValue(config.GetType(), out var factory)
                ? factory(config)
                : null;
        }
    }
}

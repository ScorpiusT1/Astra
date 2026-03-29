using Astra.Core.Devices.Interfaces;
using Astra.Core.Triggers.Interlock;
using Astra.Plugins.PLC.Configs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.PLC.Interlock
{
    /// <summary>
    /// 从 IO 配置中启用安全联锁的 BOOL/Auto 点位生成 <see cref="SafetyInterlockRuleItem"/> 列表。
    /// </summary>
    public sealed class PlcSafetyInterlockRulesProvider : ISafetyInterlockRulesProvider
    {
        public IReadOnlyList<SafetyInterlockRuleItem> GetRules()
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null)
            {
                return Array.Empty<SafetyInterlockRuleItem>();
            }

            var list = new List<SafetyInterlockRuleItem>();
            foreach (var cfg in plugin.GetAllIoConfigs())
            {
                if (cfg?.IOs == null)
                {
                    continue;
                }

                foreach (var io in cfg.IOs)
                {
                    if (io == null || !io.IsEnabled || !io.SafetyInterlockEnabled)
                    {
                        continue;
                    }

                    if (!io.SupportsSafetyInterlockDataType)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(io.Name))
                    {
                        continue;
                    }

                    var plcName = io.PlcDeviceName?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(plcName))
                    {
                        var first = plugin.GetAllPlcs().FirstOrDefault();
                        if (first is IDevice d && !string.IsNullOrWhiteSpace(d.DeviceName))
                        {
                            plcName = d.DeviceName.Trim();
                        }
                    }

                    if (string.IsNullOrEmpty(plcName))
                    {
                        continue;
                    }

                    list.Add(new SafetyInterlockRuleItem
                    {
                        RuleName = io.Name.Trim(),
                        PlcDeviceName = plcName,
                        IoPointName = io.Name.Trim(),
                        ActionOnTrue = io.SafetyInterlockActionOnTrue,
                        ActionOnFalse = io.SafetyInterlockActionOnFalse,
                        EdgeTriggered = io.SafetyInterlockEdgeTriggered
                    });
                }
            }

            return list;
        }
    }
}

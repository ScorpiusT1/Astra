using System.Collections.Generic;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 由 PLC 插件根据 IO 配置中带安全联锁的点位生成规则列表。
    /// </summary>
    public interface ISafetyInterlockRulesProvider
    {
        IReadOnlyList<SafetyInterlockRuleItem> GetRules();
    }
}

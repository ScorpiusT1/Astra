using System;
using System.Collections.Generic;

namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 与 <see cref="SafetyInterlockIoReaderBridge"/> 相同生命周期：插件初始化后注入基于 IO 的规则提供器。
    /// </summary>
    public sealed class SafetyInterlockRulesProviderBridge : ISafetyInterlockRulesProvider
    {
        private readonly object _gate = new();
        private ISafetyInterlockRulesProvider? _inner;

        public void SetImplementation(ISafetyInterlockRulesProvider? implementation)
        {
            lock (_gate)
            {
                _inner = implementation;
            }
        }

        public IReadOnlyList<SafetyInterlockRuleItem> GetRules()
        {
            ISafetyInterlockRulesProvider? inner;
            lock (_gate)
            {
                inner = _inner;
            }

            return inner?.GetRules() ?? Array.Empty<SafetyInterlockRuleItem>();
        }
    }
}

using Astra.Core.Configuration.Base;

namespace Astra.Core.Triggers.Configuration
{
    /// <summary>
    /// 触发器类配置的抽象基类，供各插件中的具体触发器配置继承。
    /// 主程序仅依赖 Core 即可通过本类型识别与订阅触发器配置，无需引用插件程序集。
    /// </summary>
    public abstract class TriggerBaseConfig : ConfigBase
    {
        protected TriggerBaseConfig()
            : base()
        {
        }

        protected TriggerBaseConfig(string configId)
            : base(configId)
        {
        }
    }
}

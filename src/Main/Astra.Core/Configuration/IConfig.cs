using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置基础接口 - 符合接口隔离原则（ISP）
    /// 只包含最基本的配置属性，避免接口臃肿
    /// </summary>
    public interface IConfig
    {
        /// <summary>
        /// 配置唯一标识符（只读，确保不可变性）
        /// </summary>
        string ConfigId { get; }

        /// <summary>
        /// 配置名称
        /// </summary>
        string ConfigName { get; set; }

        /// <summary>
        /// 创建时间（只读）
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 配置版本号（用于版本管理和冲突检测）
        /// </summary>
        int Version { get; set; }
    }

    /// <summary>
    /// 可克隆配置接口 - 接口隔离原则，仅需要克隆功能的配置实现此接口
    /// </summary>
    public interface IClonableConfig : IConfig
    {
        /// <summary>
        /// 克隆配置（生成新的ConfigId）
        /// </summary>
        IConfig Clone();

        /// <summary>
        /// 克隆配置并指定新的ConfigId
        /// </summary>
        /// <param name="newConfigId">新配置的ID</param>
        /// <returns>克隆的配置实例</returns>
        IConfig CloneWithId(string newConfigId);
    }

    /// <summary>
    /// 可观察配置接口 - 接口隔离原则，仅需要事件通知的配置实现此接口
    /// </summary>
    public interface IObservableConfig : IConfig
    {
        /// <summary>
        /// 配置变更事件
        /// </summary>
        event EventHandler<ConfigChangedEventArgs> ConfigChanged;
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class ConfigChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
}

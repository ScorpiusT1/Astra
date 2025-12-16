using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Astra.Core.Foundation.Common;

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

        /// <summary>
        /// 配置类型（只读，返回配置的实际类型）
        /// 用于快速识别配置类型，避免使用反射获取类型
        /// </summary>
        Type ConfigType { get; }

        /// <summary>
        /// 配置类型名称（可读写，用于序列化到配置文件）
        /// 序列化时自动存储类型名称，反序列化时可用于验证类型
        /// </summary>
        string ConfigTypeName { get; set; }
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

    /// <summary>
    /// 需要预保存逻辑的配置接口
    /// 符合接口隔离原则（ISP），仅需要预保存功能的配置实现此接口
    /// 用于在保存配置前执行必要的预处理操作（如保存嵌套的独立配置）
    /// </summary>
    public interface IPreSaveConfig : IConfig
    {
        /// <summary>
        /// 在保存配置前执行预处理操作
        /// </summary>
        /// <param name="configManager">配置管理器，用于保存嵌套配置</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> PreSaveAsync(IConfigurationManager configManager);
    }

    /// <summary>
    /// 需要加载后处理逻辑的配置接口
    /// 符合接口隔离原则（ISP），仅需要加载后处理功能的配置实现此接口
    /// 用于在配置加载后执行必要的恢复操作（如恢复对象引用）
    /// </summary>
    public interface IPostLoadConfig : IConfig
    {
        /// <summary>
        /// 在配置加载后执行恢复操作
        /// </summary>
        /// <param name="configManager">配置管理器，用于获取其他配置</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> PostLoadAsync(IConfigurationManager configManager);
    }
}

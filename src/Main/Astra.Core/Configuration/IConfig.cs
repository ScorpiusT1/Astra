using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置接口
    /// 所有配置类都必须实现此接口
    /// </summary>
    public interface IConfig
    {
        /// <summary>
        /// 配置ID（唯一标识）
        /// </summary>
        string ConfigId { get; set; }

        /// <summary>
        /// 配置名称
        /// </summary>
        string ConfigName { get; set; }

        /// <summary>
        /// 配置类型（用于区分不同类型的配置）
        /// </summary>
        string ConfigType { get; }

        /// <summary>
        /// 配置是否启用
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 配置版本号
        /// </summary>
        int Version { get; set; }

        /// <summary>
        /// 配置创建时间
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// 配置最后修改时间
        /// </summary>
        DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 配置变更事件
        /// </summary>
        event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        /// <summary>
        /// 验证配置
        /// </summary>
        OperationResult<bool> Validate();

        /// <summary>
        /// 克隆配置
        /// </summary>
        IConfig Clone();

        /// <summary>
        /// 转换为字典
        /// </summary>
        Dictionary<string, object> ToDictionary();

        /// <summary>
        /// 从字典加载配置
        /// </summary>
        void FromDictionary(Dictionary<string, object> dictionary);

        /// <summary>
        /// 获取需要重启的属性列表
        /// </summary>
        List<string> GetRestartRequiredProperties();

        /// <summary>
        /// 获取支持热更新的属性列表
        /// </summary>
        List<string> GetHotUpdateableProperties();

        /// <summary>
        /// 获取变更的属性列表（与另一个配置比较）
        /// </summary>
        List<string> GetChangedProperties(IConfig other);
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class ConfigChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 配置ID
        /// </summary>
        public string ConfigId { get; set; }

        /// <summary>
        /// 配置类型
        /// </summary>
        public string ConfigType { get; set; }

        /// <summary>
        /// 变更的属性列表
        /// </summary>
        public List<string> ChangedProperties { get; set; }

        /// <summary>
        /// 旧配置
        /// </summary>
        public IConfig OldConfig { get; set; }

        /// <summary>
        /// 新配置
        /// </summary>
        public IConfig NewConfig { get; set; }

        /// <summary>
        /// 变更时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 变更原因
        /// </summary>
        public string ChangedBy { get; set; }
    }
}


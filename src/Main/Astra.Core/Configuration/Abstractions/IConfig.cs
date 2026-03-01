namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 配置基础接口 — 所有配置类的最小契约。
    /// 具体扩展通过 <see cref="IClonableConfig"/>、<see cref="IObservableConfig"/>、
    /// <see cref="IValidatableConfig"/> 等接口组合实现。
    /// </summary>
    public interface IConfig
    {
        /// <summary>配置唯一标识符，由框架在创建时分配，不应由外部代码修改。</summary>
        string ConfigId { get; }

        /// <summary>配置显示名称。</summary>
        string ConfigName { get; set; }

        /// <summary>配置创建时间（只读）。</summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// 配置最后更新时间。框架通过 <c>MarkAsUpdated()</c> 维护此值，
        /// 外部代码不应直接修改。
        /// </summary>
        DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 配置版本号。每次调用 <c>MarkAsUpdated()</c> 时递增，
        /// 外部代码不应直接修改。
        /// </summary>
        int Version { get; set; }

        /// <summary>配置的运行时类型（只读）。</summary>
        Type ConfigType { get; }

        /// <summary>配置类型的完全限定名（用于跨程序集序列化）。</summary>
        string ConfigTypeName { get; set; }
    }
}

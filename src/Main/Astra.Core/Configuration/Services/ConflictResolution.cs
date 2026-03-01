namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// 导入时的冲突处理策略（当目标系统中已存在相同 ConfigId 的配置时生效）。
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>覆盖已有配置（默认）。</summary>
        Overwrite,

        /// <summary>跳过，保留目标系统中的原有配置不作任何修改。</summary>
        Skip,

        /// <summary>两者保留：为导入的配置生成新的 ConfigId，避免覆盖原有配置。</summary>
        KeepBoth
    }
}

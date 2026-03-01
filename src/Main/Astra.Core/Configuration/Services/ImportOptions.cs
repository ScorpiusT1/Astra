namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// 配置导入选项。
    /// </summary>
    public class ImportOptions
    {
        /// <summary>
        /// 冲突处理策略（默认：覆盖）。
        /// </summary>
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Overwrite;

        /// <summary>
        /// 导入前是否对实现了 <see cref="IValidatableConfig"/> 的配置执行验证。
        /// 验证失败的条目将被计入 <see cref="ImportResult.FailureCount"/>（默认 true）。
        /// </summary>
        public bool ValidateBeforeImport { get; set; } = true;

        /// <summary>
        /// 仅导入指定类型的配置。
        /// 为 null 时导入文件中所有能匹配到已注册类型的配置。
        /// </summary>
        public IReadOnlyList<Type> TypeFilter { get; set; }
    }
}

namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// 配置导入操作的结果摘要。
    /// </summary>
    public class ImportResult
    {
        /// <summary>成功导入的配置数量。</summary>
        public int ImportedCount { get; set; }

        /// <summary>因冲突策略为 <see cref="ConflictResolution.Skip"/> 而跳过的配置数量。</summary>
        public int SkippedCount { get; set; }

        /// <summary>导入失败的配置数量（类型未注册、反序列化失败、验证失败等）。</summary>
        public int FailureCount { get; set; }

        /// <summary>被跳过的配置 Id 列表。</summary>
        public List<string> SkippedIds { get; } = new();

        /// <summary>失败条目的 ConfigId → 错误描述 映射。</summary>
        public Dictionary<string, string> Failures { get; } = new();

        /// <summary>成功导入的配置实例列表（供 UI 挂到树节点等）。</summary>
        public List<IConfig> ImportedConfigs { get; } = new();

        /// <summary>是否全部成功（无任何失败）。</summary>
        public bool IsSuccess => FailureCount == 0;

        /// <summary>是否存在被跳过的条目。</summary>
        public bool HasSkipped => SkippedCount > 0;

        /// <summary>文件中的配置总条目数。</summary>
        public int TotalCount => ImportedCount + SkippedCount + FailureCount;

        /// <summary>记录一条失败信息（流式 API，方便链式调用）。</summary>
        internal ImportResult WithFailure(string key, string message)
        {
            FailureCount++;
            Failures[key] = message;
            return this;
        }
    }
}

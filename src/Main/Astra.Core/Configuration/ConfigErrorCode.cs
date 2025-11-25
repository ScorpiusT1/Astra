namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置错误代码枚举 - 提供精细化的错误分类
    /// </summary>
    public enum ConfigErrorCode
    {
        /// <summary>
        /// 未知错误
        /// </summary>
        Unknown = 0,

        // ========== 验证错误 (1000-1999) ==========
        
        /// <summary>
        /// 配置验证失败
        /// </summary>
        ValidationFailed = 1000,

        /// <summary>
        /// 配置ID为空
        /// </summary>
        ConfigIdEmpty = 1001,

        /// <summary>
        /// 配置ID重复
        /// </summary>
        ConfigIdDuplicate = 1002,

        /// <summary>
        /// 配置名称无效
        /// </summary>
        ConfigNameInvalid = 1003,

        /// <summary>
        /// 配置版本冲突
        /// </summary>
        VersionConflict = 1004,

        // ========== 文件系统错误 (2000-2999) ==========

        /// <summary>
        /// 文件不存在
        /// </summary>
        FileNotFound = 2000,

        /// <summary>
        /// 文件访问被拒绝
        /// </summary>
        FileAccessDenied = 2001,

        /// <summary>
        /// 文件读取失败
        /// </summary>
        FileReadError = 2002,

        /// <summary>
        /// 文件写入失败
        /// </summary>
        FileWriteError = 2003,

        /// <summary>
        /// 目录不存在
        /// </summary>
        DirectoryNotFound = 2004,

        // ========== JSON处理错误 (3000-3999) ==========

        /// <summary>
        /// JSON解析失败
        /// </summary>
        JsonParseError = 3000,

        /// <summary>
        /// JSON序列化失败
        /// </summary>
        JsonSerializeError = 3001,

        /// <summary>
        /// JSON反序列化失败
        /// </summary>
        JsonDeserializeError = 3002,

        /// <summary>
        /// JSON格式无效
        /// </summary>
        JsonFormatInvalid = 3003,

        // ========== 配置操作错误 (4000-4999) ==========

        /// <summary>
        /// 配置未找到
        /// </summary>
        ConfigNotFound = 4000,

        /// <summary>
        /// 配置已存在
        /// </summary>
        ConfigAlreadyExists = 4001,

        /// <summary>
        /// 配置创建失败
        /// </summary>
        ConfigCreateFailed = 4002,

        /// <summary>
        /// 配置更新失败
        /// </summary>
        ConfigUpdateFailed = 4003,

        /// <summary>
        /// 配置删除失败
        /// </summary>
        ConfigDeleteFailed = 4004,

        /// <summary>
        /// 配置克隆失败
        /// </summary>
        ConfigCloneFailed = 4005,

        /// <summary>
        /// 配置导入失败
        /// </summary>
        ConfigImportFailed = 4006,

        /// <summary>
        /// 配置导出失败
        /// </summary>
        ConfigExportFailed = 4007,

        // ========== 提供者错误 (5000-5999) ==========

        /// <summary>
        /// 提供者未注册
        /// </summary>
        ProviderNotRegistered = 5000,

        /// <summary>
        /// 提供者初始化失败
        /// </summary>
        ProviderInitializationFailed = 5001,

        /// <summary>
        /// 提供者操作失败
        /// </summary>
        ProviderOperationFailed = 5002,

        // ========== 工厂错误 (6000-6999) ==========

        /// <summary>
        /// 工厂未注册
        /// </summary>
        FactoryNotRegistered = 6000,

        /// <summary>
        /// 工厂创建失败
        /// </summary>
        FactoryCreateFailed = 6001,

        // ========== 缓存错误 (7000-7999) ==========

        /// <summary>
        /// 缓存操作失败
        /// </summary>
        CacheOperationFailed = 7000,

        // ========== 事务错误 (8000-8999) ==========

        /// <summary>
        /// 事务开始失败
        /// </summary>
        TransactionBeginFailed = 8000,

        /// <summary>
        /// 事务提交失败
        /// </summary>
        TransactionCommitFailed = 8001,

        /// <summary>
        /// 事务回滚失败
        /// </summary>
        TransactionRollbackFailed = 8002,

        /// <summary>
        /// 批量操作部分失败
        /// </summary>
        BatchOperationPartialFailure = 8003,
    }
}

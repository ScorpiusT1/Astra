namespace Astra.Core.Configuration.Exceptions
{
    /// <summary>
    /// 配置系统基础异常。所有配置相关异常都继承自此类。
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <summary>发生异常时关联的配置 ID（可选）。</summary>
        public string ConfigId { get; }

        public ConfigurationException(string message)
            : base(message) { }

        public ConfigurationException(string message, Exception innerException)
            : base(message, innerException) { }

        public ConfigurationException(string message, string configId, Exception innerException = null)
            : base(message, innerException)
        {
            ConfigId = configId;
        }

        /// <inheritdoc/>
        public override string Message => string.IsNullOrEmpty(ConfigId)
            ? base.Message
            : $"{base.Message} (ConfigId: {ConfigId})";
    }

    /// <summary>
    /// 配置验证失败异常，携带详细的验证错误列表。
    /// </summary>
    public class ConfigValidationException : ConfigurationException
    {
        public ValidationResult ValidationResult { get; }

        public ConfigValidationException(ValidationResult validationResult)
            : base(validationResult?.GetErrorSummary() ?? "配置验证失败")
        {
            ValidationResult = validationResult;
        }
    }

    /// <summary>
    /// 指定 ID 的配置不存在。
    /// </summary>
    public class ConfigNotFoundException : ConfigurationException
    {
        public ConfigNotFoundException(string configId)
            : base($"未找到配置: {configId}", configId) { }

        public ConfigNotFoundException(string configId, Exception innerException)
            : base($"未找到配置: {configId}", configId, innerException) { }
    }

    /// <summary>
    /// 尝试创建已存在的配置时抛出。
    /// </summary>
    public class ConfigAlreadyExistsException : ConfigurationException
    {
        public ConfigAlreadyExistsException(string configId)
            : base($"配置已存在: {configId}", configId) { }
    }

    /// <summary>
    /// 未为指定配置类型注册 Provider 时抛出。
    /// </summary>
    public class ProviderNotRegisteredException : ConfigurationException
    {
        public Type ConfigType { get; }

        public ProviderNotRegisteredException(Type configType)
            : base($"未注册类型 {configType?.Name} 的配置提供者")
        {
            ConfigType = configType;
        }
    }
}

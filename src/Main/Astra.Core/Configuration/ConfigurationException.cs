using System;
using System.Runtime.Serialization;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置异常基类 - 所有配置相关的异常都应继承此类
    /// </summary>
    [Serializable]
    public class ConfigurationException : Exception
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public ConfigErrorCode ErrorCode { get; set; }

        /// <summary>
        /// 配置ID（如果适用）
        /// </summary>
        public string ConfigId { get; set; }

        /// <summary>
        /// 额外的上下文信息
        /// </summary>
        public object Context { get; set; }

        public ConfigurationException()
            : this(ConfigErrorCode.Unknown, "发生配置错误")
        {
        }

        public ConfigurationException(string message)
            : this(ConfigErrorCode.Unknown, message)
        {
        }

        public ConfigurationException(ConfigErrorCode errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public ConfigurationException(ConfigErrorCode errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public ConfigurationException(ConfigErrorCode errorCode, string message, string configId)
            : base(message)
        {
            ErrorCode = errorCode;
            ConfigId = configId;
        }

        public ConfigurationException(ConfigErrorCode errorCode, string message, string configId, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            ConfigId = configId;
        }

        protected ConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = (ConfigErrorCode)info.GetInt32(nameof(ErrorCode));
            ConfigId = info.GetString(nameof(ConfigId));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorCode), (int)ErrorCode);
            info.AddValue(nameof(ConfigId), ConfigId);
        }

        public override string ToString()
        {
            var message = $"[{ErrorCode}] {Message}";
            if (!string.IsNullOrEmpty(ConfigId))
            {
                message += $" (ConfigId: {ConfigId})";
            }
            if (InnerException != null)
            {
                message += $"\n内部异常: {InnerException.Message}";
            }
            return message;
        }
    }

    /// <summary>
    /// 配置验证异常
    /// </summary>
    [Serializable]
    public class ConfigValidationException : ConfigurationException
    {
        public ValidationResult ValidationResult { get; set; }

        public ConfigValidationException(ValidationResult validationResult)
            : base(ConfigErrorCode.ValidationFailed, validationResult.Message)
        {
            ValidationResult = validationResult;
        }

        public ConfigValidationException(string message, ValidationResult validationResult)
            : base(ConfigErrorCode.ValidationFailed, message)
        {
            ValidationResult = validationResult;
        }

        protected ConfigValidationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// 配置未找到异常
    /// </summary>
    [Serializable]
    public class ConfigNotFoundException : ConfigurationException
    {
        public ConfigNotFoundException(string configId)
            : base(ConfigErrorCode.ConfigNotFound, $"未找到配置: {configId}", configId)
        {
        }

        public ConfigNotFoundException(string configId, Exception innerException)
            : base(ConfigErrorCode.ConfigNotFound, $"未找到配置: {configId}", configId, innerException)
        {
        }

        protected ConfigNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// 配置已存在异常
    /// </summary>
    [Serializable]
    public class ConfigAlreadyExistsException : ConfigurationException
    {
        public ConfigAlreadyExistsException(string configId)
            : base(ConfigErrorCode.ConfigAlreadyExists, $"配置已存在: {configId}", configId)
        {
        }

        public ConfigAlreadyExistsException(string configId, Exception innerException)
            : base(ConfigErrorCode.ConfigAlreadyExists, $"配置已存在: {configId}", configId, innerException)
        {
        }

        protected ConfigAlreadyExistsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// 提供者未注册异常
    /// </summary>
    [Serializable]
    public class ProviderNotRegisteredException : ConfigurationException
    {
        public Type ConfigType { get; set; }

        public ProviderNotRegisteredException(Type configType)
            : base(ConfigErrorCode.ProviderNotRegistered, $"未注册类型 {configType.Name} 的配置提供者")
        {
            ConfigType = configType;
        }

        protected ProviderNotRegisteredException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}

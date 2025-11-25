using System.Text;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 验证结果类 - 支持链式调用，提高易用性
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否验证成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 验证消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 错误代码
        /// </summary>
        public ConfigErrorCode ErrorCode { get; set; } = ConfigErrorCode.ValidationFailed;

        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        /// <summary>
        /// 警告列表（不影响验证成功）
        /// </summary>
        public List<ValidationWarning> Warnings { get; set; } = new List<ValidationWarning>();

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasErrors => Errors.Any();

        /// <summary>
        /// 是否有警告
        /// </summary>
        public bool HasWarnings => Warnings.Any();

        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount => Errors.Count;

        /// <summary>
        /// 警告数量
        /// </summary>
        public int WarningCount => Warnings.Count;

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ValidationResult Success(string message = "验证通过")
        {
            return new ValidationResult
            {
                IsSuccess = true,
                Message = message
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ValidationResult Failure(string message)
        {
            return new ValidationResult
            {
                IsSuccess = false,
                Message = message
            };
        }

        /// <summary>
        /// 创建失败结果（带错误码）
        /// </summary>
        public static ValidationResult Failure(string message, ConfigErrorCode errorCode)
        {
            return new ValidationResult
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode
            };
        }

        /// <summary>
        /// 创建带错误列表的失败结果
        /// </summary>
        public static ValidationResult Failure(string message, List<ValidationError> errors)
        {
            return new ValidationResult
            {
                IsSuccess = false,
                Message = message,
                Errors = errors
            };
        }

        /// <summary>
        /// 添加错误（链式调用）
        /// </summary>
        public ValidationResult WithError(string propertyName, string errorMessage, string errorCode = null)
        {
            AddError(propertyName, errorMessage, errorCode);
            IsSuccess = false;
            return this;
        }

        /// <summary>
        /// 添加错误对象（链式调用）
        /// </summary>
        public ValidationResult WithError(ValidationError error)
        {
            if (error != null)
            {
                Errors.Add(error);
                IsSuccess = false;
            }
            return this;
        }

        /// <summary>
        /// 添加多个错误（链式调用）
        /// </summary>
        public ValidationResult WithErrors(IEnumerable<ValidationError> errors)
        {
            if (errors != null)
            {
                Errors.AddRange(errors);
                if (Errors.Any())
                    IsSuccess = false;
            }
            return this;
        }

        /// <summary>
        /// 添加警告（链式调用）
        /// </summary>
        public ValidationResult WithWarning(string propertyName, string warningMessage)
        {
            AddWarning(propertyName, warningMessage);
            return this;
        }

        /// <summary>
        /// 添加警告对象（链式调用）
        /// </summary>
        public ValidationResult WithWarning(ValidationWarning warning)
        {
            if (warning != null)
            {
                Warnings.Add(warning);
            }
            return this;
        }

        /// <summary>
        /// 添加多个警告（链式调用）
        /// </summary>
        public ValidationResult WithWarnings(IEnumerable<ValidationWarning> warnings)
        {
            if (warnings != null)
            {
                Warnings.AddRange(warnings);
            }
            return this;
        }

        /// <summary>
        /// 设置错误码（链式调用）
        /// </summary>
        public ValidationResult WithErrorCode(ConfigErrorCode errorCode)
        {
            ErrorCode = errorCode;
            return this;
        }

        /// <summary>
        /// 合并其他验证结果（链式调用）
        /// </summary>
        public ValidationResult Merge(ValidationResult other)
        {
            if (other == null) return this;

            if (!other.IsSuccess)
            {
                IsSuccess = false;
            }

            Errors.AddRange(other.Errors);
            Warnings.AddRange(other.Warnings);

            return this;
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string propertyName, string errorMessage, string errorCode = null)
        {
            Errors.Add(new ValidationError
            {
                PropertyName = propertyName,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            });
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string propertyName, string warningMessage)
        {
            Warnings.Add(new ValidationWarning
            {
                PropertyName = propertyName,
                WarningMessage = warningMessage
            });
        }

        /// <summary>
        /// 获取错误摘要
        /// </summary>
        public string GetErrorSummary()
        {
            if (IsSuccess)
                return Message;

            var summary = new StringBuilder();
            summary.AppendLine(Message);

            if (Errors.Any())
            {
                summary.AppendLine($"共 {Errors.Count} 个错误:");
                foreach (var error in Errors)
                {
                    summary.AppendLine($"  - {error.PropertyName}: {error.ErrorMessage}");
                }
            }

            if (Warnings.Any())
            {
                summary.AppendLine($"共 {Warnings.Count} 个警告:");
                foreach (var warning in Warnings)
                {
                    summary.AppendLine($"  - {warning.PropertyName}: {warning.WarningMessage}");
                }
            }

            return summary.ToString();
        }

        /// <summary>
        /// 转换为异常
        /// </summary>
        public ConfigValidationException ToException()
        {
            return new ConfigValidationException(this);
        }

        /// <summary>
        /// 如果验证失败则抛出异常
        /// </summary>
        public ValidationResult ThrowIfFailed()
        {
            if (!IsSuccess)
            {
                throw ToException();
            }
            return this;
        }
    }
}

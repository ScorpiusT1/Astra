using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Astra.Core.Configuration.Validation
{
    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();

        public bool HasErrors => Errors.Any();
        public bool HasWarnings => Warnings.Any();

        private ValidationResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static ValidationResult Success(string message = "验证通过")
            => new(isSuccess: true, message);

        public static ValidationResult Failure(string message)
            => new(isSuccess: false, message);

        public static ValidationResult Failure(string message, IEnumerable<ValidationError> errors)
        {
            var result = new ValidationResult(isSuccess: false, message);
            if (errors != null) result.Errors.AddRange(errors);
            return result;
        }

        public ValidationResult WithError(string propertyName, string errorMessage, string errorCode = null)
        {
            AddError(propertyName, errorMessage, errorCode);
            return this;
        }

        public ValidationResult WithWarning(string propertyName, string warningMessage)
        {
            AddWarning(propertyName, warningMessage);
            return this;
        }

        public void AddError(string propertyName, string errorMessage, string errorCode = null)
        {
            Errors.Add(new ValidationError
            {
                PropertyName = propertyName,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                Severity = ValidationSeverity.Error
            });
            IsSuccess = false;
        }

        public void AddWarning(string propertyName, string warningMessage)
        {
            Warnings.Add(new ValidationWarning
            {
                PropertyName = propertyName,
                WarningMessage = warningMessage
            });
        }

        /// <summary>返回所有错误和警告的汇总文本</summary>
        public string GetErrorSummary()
        {
            if (!HasErrors && !HasWarnings) return Message;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Message)) sb.AppendLine(Message);

            if (HasErrors)
            {
                sb.AppendLine("错误：");
                foreach (var error in Errors)
                    sb.AppendLine($"  - {error.PropertyName}: {error.ErrorMessage}");
            }

            if (HasWarnings)
            {
                sb.AppendLine("警告：");
                foreach (var warning in Warnings)
                    sb.AppendLine($"  - {warning.PropertyName}: {warning.WarningMessage}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}

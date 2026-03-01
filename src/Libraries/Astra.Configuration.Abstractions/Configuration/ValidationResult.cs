using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();

        public bool IsValid => !Errors.Any();

        public ValidationResult AddError(string propertyName, string errorMessage, string errorCode = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            Errors.Add(new ValidationError
            {
                PropertyName = propertyName,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                Severity = severity
            });
            return this;
        }

        public ValidationResult AddWarning(string propertyName, string warningMessage, string warningCode = null, ValidationSeverity severity = ValidationSeverity.Warning)
        {
            Warnings.Add(new ValidationWarning
            {
                PropertyName = propertyName,
                WarningMessage = warningMessage,
                WarningCode = warningCode,
                Severity = severity
            });
            return this;
        }

        public override string ToString()
        {
            if (IsValid)
            {
                return "验证通过";
            }

            var errorMessages = string.Join("; ", Errors.Select(e => e.ToString()));
            return $"验证失败: {errorMessages}";
        }
    }
}


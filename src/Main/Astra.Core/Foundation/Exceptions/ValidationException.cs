using System;
using System.Collections.Generic;
using System.Linq;

// ⚠️ 迁移说明：
//   - 新创建：验证异常类
//   - 位置：Astra.Core/Foundation/Exceptions/ValidationException.cs
//   - 命名空间：Astra.Core.Foundation.Exceptions（已更新为与文件夹匹配）
//   - 原因：用于数据验证相关的异常

namespace Astra.Core.Foundation.Exceptions
{
    /// <summary>
    /// 验证异常类
    /// 
    /// ✅ 设计说明：
    ///   - 用于数据验证失败的情况
    ///   - 可以包含多个验证错误
    ///   - 继承自 BusinessException
    /// </summary>
    public class ValidationException : BusinessException
    {
        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<ValidationError> ValidationErrors { get; }

        public ValidationException(string message)
            : base(message, "Validation")
        {
            ValidationErrors = new List<ValidationError>();
        }

        public ValidationException(string message, IEnumerable<ValidationError> errors)
            : base(message, "Validation")
        {
            ValidationErrors = errors?.ToList() ?? new List<ValidationError>();
        }

        public ValidationException(string message, string fieldName, string errorMessage)
            : base(message, "Validation")
        {
            ValidationErrors = new List<ValidationError>
            {
                new ValidationError(fieldName, errorMessage)
            };
        }

        /// <summary>
        /// 添加验证错误
        /// </summary>
        public ValidationException AddError(string fieldName, string errorMessage)
        {
            ValidationErrors.Add(new ValidationError(fieldName, errorMessage));
            return this;
        }

        /// <summary>
        /// 是否有验证错误
        /// </summary>
        public bool HasErrors => ValidationErrors.Count > 0;

        public override string ToString()
        {
            var baseMessage = base.ToString();
            if (ValidationErrors.Count == 0)
                return baseMessage;

            var errors = string.Join("\n  ", ValidationErrors.Select(e => $"- {e.FieldName}: {e.ErrorMessage}"));
            return $"{baseMessage}\nValidation Errors:\n  {errors}";
        }
    }

    /// <summary>
    /// 验证错误信息
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; }

        public ValidationError(string fieldName, string errorMessage)
        {
            FieldName = fieldName ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public override string ToString()
        {
            return $"{FieldName}: {ErrorMessage}";
        }
    }
}


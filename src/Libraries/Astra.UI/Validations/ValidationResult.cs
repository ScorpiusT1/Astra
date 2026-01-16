using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Astra.UI.Validations
{
    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }
        public ValidationSeverity Severity { get; }

        private ValidationResult(bool isValid, string errorMessage, ValidationSeverity severity)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            Severity = severity;
        }

        public static ValidationResult Success() =>
            new ValidationResult(true, null, ValidationSeverity.None);

        public static ValidationResult Error(string message) =>
            new ValidationResult(false, message, ValidationSeverity.Error);

        public static ValidationResult Warning(string message) =>
            new ValidationResult(true, message, ValidationSeverity.Warning);

        public static ValidationResult Info(string message) =>
            new ValidationResult(true, message, ValidationSeverity.Info);
    }

    /// <summary>
    /// 必填验证
    /// </summary>
    public class RequiredAttribute : ValidationAttribute
    {
        public override ValidationResult Validate(object value, string propertyName)
        {
            if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
            {
                return ValidationResult.Error(
                    ErrorMessage ?? $"{propertyName} 是必填项");
            }
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 范围验证
    /// </summary>
    public class RangeAttribute : ValidationAttribute
    {
        public double Minimum { get; }
        public double Maximum { get; }

        public RangeAttribute(double minimum, double maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public override ValidationResult Validate(object value, string propertyName)
        {
            if (value == null)
                return ValidationResult.Success();

            try
            {
                var numValue = Convert.ToDouble(value);
                if (numValue < Minimum || numValue > Maximum)
                {
                    return ValidationResult.Error(
                        ErrorMessage ?? $"{propertyName} 必须在 {Minimum} 到 {Maximum} 之间");
                }
            }
            catch
            {
                return ValidationResult.Error($"{propertyName} 必须是有效的数字");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 字符串长度验证
    /// </summary>
    public class StringLengthAttribute : ValidationAttribute
    {
        public int MinimumLength { get; set; }
        public int MaximumLength { get; set; }

        public StringLengthAttribute(int maximumLength)
        {
            MaximumLength = maximumLength;
        }

        public override ValidationResult Validate(object value, string propertyName)
        {
            if (value == null)
                return ValidationResult.Success();

            var str = value.ToString();

            if (str.Length < MinimumLength)
            {
                return ValidationResult.Error(
                    ErrorMessage ?? $"{propertyName} 长度不能少于 {MinimumLength} 个字符");
            }

            if (str.Length > MaximumLength)
            {
                return ValidationResult.Error(
                    ErrorMessage ?? $"{propertyName} 长度不能超过 {MaximumLength} 个字符");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 正则表达式验证
    /// </summary>
    public class RegularExpressionAttribute : ValidationAttribute
    {
        public string Pattern { get; }

        public RegularExpressionAttribute(string pattern)
        {
            Pattern = pattern;
        }

        public override ValidationResult Validate(object value, string propertyName)
        {
            if (value == null)
                return ValidationResult.Success();

            var str = value.ToString();
            if (!Regex.IsMatch(str, Pattern))
            {
                return ValidationResult.Error(
                    ErrorMessage ?? $"{propertyName} 格式不正确");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 邮箱验证
    /// </summary>
    public class EmailAddressAttribute : RegularExpressionAttribute
    {
        private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        public EmailAddressAttribute() : base(EmailPattern)
        {
            ErrorMessage = "邮箱地址格式不正确";
        }
    }

    /// <summary>
    /// 电话号码验证
    /// </summary>
    public class PhoneAttribute : RegularExpressionAttribute
    {
        private const string PhonePattern = @"^1[3-9]\d{9}$";

        public PhoneAttribute() : base(PhonePattern)
        {
            ErrorMessage = "手机号码格式不正确";
        }
    }

    /// <summary>
    /// 自定义验证器
    /// </summary>
    public class CustomValidationAttribute : ValidationAttribute
    {
        public Type ValidatorType { get; }
        public string MethodName { get; }

        public CustomValidationAttribute(Type validatorType, string methodName)
        {
            ValidatorType = validatorType;
            MethodName = methodName;
        }

        public override ValidationResult Validate(object value, string propertyName)
        {
            try
            {
                var method = ValidatorType.GetMethod(MethodName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (method == null)
                    return ValidationResult.Error("验证方法不存在");

                var result = method.Invoke(null, new[] { value });

                if (result is ValidationResult validationResult)
                    return validationResult;

                if (result is bool isValid)
                    return isValid ? ValidationResult.Success() :
                           ValidationResult.Error(ErrorMessage ?? $"{propertyName} 验证失败");

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Error($"验证过程出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 比较验证（用于确认密码等场景）
    /// </summary>
    public class CompareAttribute : ValidationAttribute
    {
        public string CompareToProperty { get; }

        public CompareAttribute(string compareToProperty)
        {
            CompareToProperty = compareToProperty;
        }

        public override ValidationResult Validate(object value, string propertyName)
        {
            // 此验证需要在 PropertyDescriptor 中特殊处理
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// URL验证
    /// </summary>
    public class UrlAttribute : ValidationAttribute
    {
        public override ValidationResult Validate(object value, string propertyName)
        {
            if (value == null)
                return ValidationResult.Success();

            var str = value.ToString();
            if (!Uri.TryCreate(str, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return ValidationResult.Error(
                    ErrorMessage ?? $"{propertyName} 必须是有效的URL地址");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// 信用卡验证
    /// </summary>
    public class CreditCardAttribute : ValidationAttribute
    {
        public override ValidationResult Validate(object value, string propertyName)
        {
            if (value == null)
                return ValidationResult.Success();

            var cardNumber = value.ToString().Replace(" ", "").Replace("-", "");

            if (!IsValidLuhn(cardNumber))
            {
                return ValidationResult.Error(
                    ErrorMessage ?? "信用卡号码无效");
            }

            return ValidationResult.Success();
        }

        private bool IsValidLuhn(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber) || !long.TryParse(cardNumber, out _))
                return false;

            int sum = 0;
            bool alternate = false;

            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(cardNumber[i].ToString());

                if (alternate)
                {
                    n *= 2;
                    if (n > 9)
                        n -= 9;
                }

                sum += n;
                alternate = !alternate;
            }

            return sum % 10 == 0;
        }
    }
}

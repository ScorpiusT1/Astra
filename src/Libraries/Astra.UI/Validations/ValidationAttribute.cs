namespace Astra.UI.Validations
{
    /// <summary>
    /// 验证特性基类
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class ValidationAttribute : Attribute
    {
        public string ErrorMessage { get; set; }

        public abstract ValidationResult Validate(object value, string propertyName);
    }
}

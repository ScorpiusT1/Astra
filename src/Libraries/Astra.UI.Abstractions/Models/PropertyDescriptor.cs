using Astra.UI.Abstractions.Attributes;
using System;
using System.ComponentModel;
using System.Reflection;
using EditorAttribute = Astra.UI.Abstractions.Attributes.EditorAttribute;

namespace Astra.UI.Abstractions.Models
{
    public class PropertyDescriptor : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly object _targetObject;
        private readonly PropertyInfo _propertyInfo;
        private object _value;
        private bool _isBrowsable = true;
        private readonly object _defaultValue;
        private readonly List<Validations.ValidationAttribute> _validationAttributes;
        private readonly Dictionary<string, List<string>> _errors;

        // 验证相关属性
        public bool HasErrors => _errors.Count > 0;
        public string ErrorMessage { get; private set; }
        public Validations.ValidationSeverity ValidationSeverity { get; private set; }

        public string Name { get; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public int GroupOrder { get; set; } = int.MaxValue; // 属性所属分组的排序顺序（从 OrderAttribute 获取）
        public int CategoryGroupOrder { get; set; } = int.MaxValue; // 分组排序顺序（用于分组显示顺序，取该分组中所有属性的最小 GroupOrder）
        public Type PropertyType { get; }
        public Type EditorType { get; set; }
        public bool IsReadOnly { get; private set; }
        public bool IsCollection { get; }
        public Type CollectionItemType { get; }

        public bool IsBrowsable
        {
            get => _isBrowsable;
            set
            {
                if (_isBrowsable != value)
                {
                    _isBrowsable = value;
                    OnPropertyChanged(nameof(IsBrowsable));
                }
            }
        }

        public object Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    var oldValue = _value;

                    // 执行验证
                    var validationResult = ValidateValue(value);

                    if (!validationResult.IsValid)
                    {
                        // 验证失败，显示错误但不更新值
                        SetError(validationResult.ErrorMessage, validationResult.Severity);
                        OnPropertyChanged(nameof(Value));
                        OnPropertyChanged(nameof(HasErrors));
                        OnPropertyChanged(nameof(ErrorMessage));
                        return;
                    }

                    // 清除错误
                    ClearErrors();

                    // 显示警告或信息
                    if (validationResult.Severity != Validations.ValidationSeverity.None)
                    {
                        SetError(validationResult.ErrorMessage, validationResult.Severity);
                    }

                    _value = value;

                    try
                    {
                        if (!IsReadOnly && _propertyInfo?.CanWrite == true)
                        {
                            // 类型转换：如果输入的是 string，但属性类型不是 string，需要转换
                            object convertedValue = value;
                            string conversionError = null;
                            
                            if (value != null && PropertyType != value.GetType())
                            {
                                // 如果值是 string，但目标类型不是 string，进行类型转换
                                if (value is string stringValue && PropertyType != typeof(string))
                                {
                                    // 空字符串处理：对于可空类型，转换为 null；对于值类型，使用默认值
                                    if (string.IsNullOrWhiteSpace(stringValue))
                                    {
                                        var underlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
                                        if (underlyingType.IsValueType)
                                        {
                                            convertedValue = Activator.CreateInstance(underlyingType);
                                        }
                                        else
                                        {
                                            convertedValue = null;
                                        }
                                    }
                                    else
                                    {
                                        // 使用 TypeConverter 或 Convert.ChangeType 进行转换
                                        try
                                        {
                                            var typeConverter = System.ComponentModel.TypeDescriptor.GetConverter(PropertyType);
                                            if (typeConverter != null && typeConverter.CanConvertFrom(typeof(string)))
                                            {
                                                convertedValue = typeConverter.ConvertFromString(stringValue);
                                            }
                                            else if (PropertyType.IsEnum)
                                            {
                                                // 枚举类型特殊处理
                                                try
                                                {
                                                    convertedValue = Enum.Parse(PropertyType, stringValue, true);
                                                }
                                                catch (ArgumentException)
                                                {
                                                    var validValues = string.Join(", ", Enum.GetNames(PropertyType));
                                                    conversionError = $"'{stringValue}' 不是有效的 {PropertyType.Name} 值。有效值: {validValues}";
                                                    throw;
                                                }
                                            }
                                            else if (typeof(IConvertible).IsAssignableFrom(PropertyType))
                                            {
                                                // 可转换类型（int, decimal, double 等）
                                                var underlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
                                                try
                                                {
                                                    convertedValue = Convert.ChangeType(stringValue, underlyingType);
                                                }
                                                catch (FormatException)
                                                {
                                                    conversionError = $"无法将 '{stringValue}' 转换为 {PropertyType.Name} 类型（格式错误）";
                                                    throw;
                                                }
                                                catch (OverflowException)
                                                {
                                                    conversionError = $"无法将 '{stringValue}' 转换为 {PropertyType.Name} 类型（数值超出范围）";
                                                    throw;
                                                }
                                            }
                                            else
                                            {
                                                conversionError = $"无法将字符串 '{stringValue}' 转换为 {PropertyType.Name} 类型";
                                                throw new InvalidOperationException(conversionError);
                                            }
                                        }
                                        catch (Exception convertEx)
                                        {
                                            // 如果已经有友好的错误消息，使用它；否则使用异常消息
                                            if (string.IsNullOrEmpty(conversionError))
                                            {
                                                conversionError = $"类型转换失败: {convertEx.Message}";
                                            }
                                            
                                            // 设置错误提示并恢复原值
                                            SetError(conversionError, Validations.ValidationSeverity.Error);
                                            _value = oldValue;
                                            OnPropertyChanged(nameof(Value));
                                            OnPropertyChanged(nameof(HasErrors));
                                            OnPropertyChanged(nameof(ErrorMessage));
                                            return;
                                        }
                                    }
                                }
                                else if (PropertyType.IsAssignableFrom(value.GetType()))
                                {
                                    // 类型兼容，直接使用
                                    convertedValue = value;
                                }
                                else
                                {
                                    // 尝试使用 Convert.ChangeType
                                    var underlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
                                    if (typeof(IConvertible).IsAssignableFrom(value.GetType()) && 
                                        typeof(IConvertible).IsAssignableFrom(underlyingType))
                                    {
                                        try
                                        {
                                            convertedValue = Convert.ChangeType(value, underlyingType);
                                        }
                                        catch (Exception convertEx)
                                        {
                                            conversionError = $"无法将 {value.GetType().Name} 类型转换为 {PropertyType.Name} 类型: {convertEx.Message}";
                                            SetError(conversionError, Validations.ValidationSeverity.Error);
                                            _value = oldValue;
                                            OnPropertyChanged(nameof(Value));
                                            OnPropertyChanged(nameof(HasErrors));
                                            OnPropertyChanged(nameof(ErrorMessage));
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        conversionError = $"无法将 {value.GetType().Name} 类型转换为 {PropertyType.Name} 类型";
                                        SetError(conversionError, Validations.ValidationSeverity.Error);
                                        _value = oldValue;
                                        OnPropertyChanged(nameof(Value));
                                        OnPropertyChanged(nameof(HasErrors));
                                        OnPropertyChanged(nameof(ErrorMessage));
                                        return;
                                    }
                                }
                            }
                            
                            // 转换成功，设置属性值
                            _propertyInfo.SetValue(_targetObject, convertedValue);
                            // 更新 _value 为转换后的值，保持一致性
                            _value = convertedValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 捕获所有其他异常（例如属性设置失败）
                        var errorMessage = $"设置属性值失败: {ex.InnerException?.Message ?? ex.Message}";
                        SetError(errorMessage, Validations.ValidationSeverity.Error);
                        _value = oldValue;
                    }

                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(HasErrors));
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        public PropertyDescriptor(object targetObject, PropertyInfo propertyInfo)
        {
            _targetObject = targetObject ?? throw new ArgumentNullException(nameof(targetObject));
            _propertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            _errors = new Dictionary<string, List<string>>();
            _validationAttributes = new List<Validations.ValidationAttribute>();

            Name = propertyInfo.Name;
            PropertyType = propertyInfo.PropertyType;
            IsReadOnly = !propertyInfo.CanWrite;

            ParseAttributes(propertyInfo);

            _value = propertyInfo.GetValue(targetObject);
            _defaultValue = GetDefaultValue();

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(PropertyType) &&
                PropertyType != typeof(string))
            {
                IsCollection = true;
                CollectionItemType = GetCollectionItemType();
            }

            // 初始验证
            ValidateValue(_value);
        }

        private void ParseAttributes(PropertyInfo propertyInfo)
        {
            // 使用系统自带的 DisplayAttribute
            var displayAttr = propertyInfo.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
            if (displayAttr != null)
            {
                DisplayName = displayAttr.GetName() ?? Name;
                Category = displayAttr.GetGroupName() ?? "常规";
                Description = displayAttr.GetDescription();
                Order = displayAttr.GetOrder() ?? int.MaxValue; // 如果没有设置 Order，使用最大值
            }
            else
            {
                DisplayName = Name;
                Category = "常规";
                Order = int.MaxValue;
            }

            // 检查是否有 OrderAttribute（用于分组排序和组内排序）
            var orderAttr = propertyInfo.GetCustomAttribute<OrderAttribute>();
            if (orderAttr != null)
            {
                // OrderAttribute 的优先级高于 DisplayAttribute 的 Order
                if (orderAttr.GroupOrder.HasValue)
                {
                    GroupOrder = orderAttr.GroupOrder.Value;
                }
                if (orderAttr.PropertyOrder.HasValue)
                {
                    Order = orderAttr.PropertyOrder.Value;
                }
            }

            var browsableAttr = propertyInfo.GetCustomAttribute<System.ComponentModel.BrowsableAttribute>();
            _isBrowsable = browsableAttr?.Browsable ?? true;

            var editorAttr = propertyInfo.GetCustomAttribute<EditorAttribute>();
            EditorType = editorAttr?.EditorType;

            var readOnlyAttr = propertyInfo.GetCustomAttribute<System.ComponentModel.ReadOnlyAttribute>();
            if (readOnlyAttr?.IsReadOnly == true)
            {
                IsReadOnly = true;
            }

            // 收集所有验证特性
            _validationAttributes.AddRange(
                propertyInfo.GetCustomAttributes<Validations.ValidationAttribute>());
        }

        /// <summary>
        /// 验证值
        /// </summary>
        private Validations.ValidationResult ValidateValue(object value)
        {
            // 跳过只读属性的验证
            if (IsReadOnly)
                return Validations.ValidationResult.Success();

            foreach (var validator in _validationAttributes)
            {
                var result = validator.Validate(value, DisplayName);
                if (!result.IsValid || result.Severity != Validations.ValidationSeverity.None)
                {
                    return result;
                }
            }

            return Validations.ValidationResult.Success();
        }

        /// <summary>
        /// 设置错误
        /// </summary>
        private void SetError(string errorMessage, Validations.ValidationSeverity severity)
        {
            ErrorMessage = errorMessage;
            ValidationSeverity = severity;

            // 触发属性变更通知，确保 UI 能够更新
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(HasErrors));

            if (severity == Validations.ValidationSeverity.Error)
            {
                if (!_errors.ContainsKey(nameof(Value)))
                    _errors[nameof(Value)] = new List<string>();

                _errors[nameof(Value)].Clear();
                _errors[nameof(Value)].Add(errorMessage);

                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value)));
            }
        }

        /// <summary>
        /// 清除错误
        /// </summary>
        private void ClearErrors()
        {
            ErrorMessage = null;
            ValidationSeverity = Validations.ValidationSeverity.None;

            // 触发属性变更通知，确保 UI 能够更新
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(HasErrors));

            if (_errors.Count > 0)
            {
                _errors.Clear();
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value)));
            }
        }

        private Type GetCollectionItemType()
        {
            if (PropertyType.IsArray)
                return PropertyType.GetElementType();

            if (PropertyType.IsGenericType)
            {
                var genericArgs = PropertyType.GetGenericArguments();
                if (genericArgs.Length > 0)
                    return genericArgs[0];
            }

            return typeof(object);
        }

        private object GetDefaultValue()
        {
            var defaultAttr = _propertyInfo.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultAttr != null)
                return defaultAttr.Value;

            return PropertyType.IsValueType ? Activator.CreateInstance(PropertyType) : null;
        }

        public void ResetValue()
        {
            Value = _defaultValue;
        }



        // INotifyDataErrorInfo 实现
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                propertyName = nameof(Value);

            return _errors.ContainsKey(propertyName) ? _errors[propertyName] : null;
        }

        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

using Astra.Core.Foundation.Common;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Specifications;
using Astra.Core.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Devices.Configuration
{
    /// <summary>
    /// 设备配置基类
    /// 合并了设备信息和配置功能，实现 IDeviceInfo 和 IConfig 接口
    /// </summary>
    public abstract class DeviceConfig : ConfigBase, IDeviceInfo
    {
        #region IDeviceInfo 接口实现（设备基本信息）

        private string _deviceId;
        private DeviceType _type;

        /// <summary>
        /// 设备ID（由 GenerateDeviceId() 生成）
        /// </summary>
        public string DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        /// <summary>
        /// 设备类型（只读）
        /// </summary>
        public DeviceType Type
        {
            get => _type;
            protected set => SetProperty(ref _type, value);
        }

        #endregion

        #region 基础配置项

        private bool _isEnabled = true;
        private string _groupId = "G0";
        private string _slotId = "S0";

        /// <summary>
        /// 设备名称（与 ConfigName 保持一致，作为别名）
        /// 统一使用 ConfigName 作为设备名称，消除冗余
        /// 注意：不序列化此属性，因为它是 ConfigName 的别名
        /// </summary>
        [HotUpdatable]
        [JsonIgnore]
        public string DeviceName
        {
            get => ConfigName;
            set
            {
                if (ConfigName != value)
                {
                    ConfigName = value;
                    // 触发 DeviceName 的属性变更通知（ConfigName 的变更通知由基类处理）
                    OnPropertyChanged(nameof(DeviceName));
                }
            }
        }

        /// <summary>
        /// 设备是否启用
        /// </summary>
        [HotUpdatable]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 设备所属分组标识
        /// </summary>
        [HotUpdatable]
        public string GroupId
        {
            get => _groupId;
            set => SetProperty(ref _groupId, value);
        }

        /// <summary>
        /// 设备所在槽位标识
        /// </summary>
        [HotUpdatable]
        public string SlotId
        {
            get => _slotId;
            set => SetProperty(ref _slotId, value);
        }

        #endregion

        #region 属性变更通知

        /// <summary>
        /// 属性变更事件（使用自定义事件参数，保持向后兼容）
        /// </summary>
        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        /// <summary>
        /// 重写 OnPropertyChanged 以同时触发自定义 PropertyChanged 事件
        /// </summary>
        protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnPropertyChanged(propertyName, oldValue, newValue);

            // 同时触发自定义 PropertyChanged 事件以保持向后兼容
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            });

            // 跳过 ModifiedAt 和 CreatedAt 的变更事件触发（避免循环）
            if (propertyName == nameof(ModifiedAt) || propertyName == nameof(CreatedAt))
                return;

            // 直接赋值 ModifiedAt 字段，避免触发变更事件导致无限递归
            _modifiedAt = DateTime.Now;
        }

        #endregion

        #region IConfig 接口实现


        /// <summary>
        /// 配置最后修改时间（直接赋值，不触发变更事件）
        /// </summary>
        private DateTime _modifiedAt = DateTime.Now;
        public DateTime ModifiedAt
        {
            get => _modifiedAt;
            set => _modifiedAt = value;
        }

        /// <summary>
        /// 从字典加载配置（IConfig 接口）
        /// </summary>
        public virtual void FromDictionary(Dictionary<string, object> dictionary)
        {
            if (dictionary == null)
                return;

            var properties = GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanWrite && dictionary.ContainsKey(prop.Name))
                {
                    try
                    {
                        var value = dictionary[prop.Name];
                        if (value != null)
                        {
                            // 类型转换
                            if (prop.PropertyType.IsAssignableFrom(value.GetType()))
                            {
                                prop.SetValue(this, value);
                            }
                            else if (value is IConvertible)
                            {
                                var convertedValue = Convert.ChangeType(value, prop.PropertyType);
                                prop.SetValue(this, convertedValue);
                            }
                        }
                    }
                    catch
                    {
                        // 忽略无法设置的属性
                    }
                }
            }
        }

        #endregion

        #region 设备标识信息（IDeviceInfo 接口新增属性）

        private string _manufacturer = string.Empty;
        private string _model = string.Empty;
        private string _serialNumber = string.Empty;

        /// <summary>
        /// 设备厂家
        /// </summary>
        [HotUpdatable]
        public string Manufacturer
        {
            get => _manufacturer;
            set
            {
                if (SetProperty(ref _manufacturer, value))
                {
                    // 厂家变更时，重置型号
                    if (!string.IsNullOrEmpty(_model))
                    {
                        var oldModel = _model;
                        _model = string.Empty;
                        OnPropertyChanged(nameof(Model));
                    }

                    // 应用设备约束
                    ApplyDeviceConstraints();
                    
                    // 更新设备ID
                    DeviceId = GenerateDeviceId();
                }
            }
        }

        /// <summary>
        /// 设备型号
        /// </summary>
        [HotUpdatable]
        public string Model
        {
            get => _model;
            set
            {
                if (SetProperty(ref _model, value))
                {
                    // 应用设备约束
                    ApplyDeviceConstraints();
                    
                    // 更新设备ID
                    DeviceId = GenerateDeviceId();
                }
            }
        }

        /// <summary>
        /// 设备序列号
        /// </summary>
        [HotUpdatable]
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (SetProperty(ref _serialNumber, value))
                {
                    // 更新设备ID
                    DeviceId = GenerateDeviceId();
                }
            }
        }

        /// <summary>
        /// 应用设备约束（子类可重写以实现特定约束逻辑）
        /// </summary>
        protected virtual void ApplyDeviceConstraints()
        {
            var spec = DeviceSpecificationRegistry.GetSpecification(Type, Manufacturer, Model);
            if (spec == null)
            {
                return; // 未找到规格，不应用约束
            }

            // 如果配置实现了约束接口，调用应用约束方法
            if (this is IDeviceSpecificationConstraint constraint)
            {
                constraint.ApplyConstraints(spec);
            }
        }

        #endregion

        #region IDeviceInfo 接口方法实现

        /// <summary>
        /// 获取设备信息字典（包含厂家、型号和序列号）
        /// </summary>
        public virtual Dictionary<string, string> GetDeviceInfo()
        {
            return new Dictionary<string, string>
            {
                ["DeviceId"] = DeviceId ?? string.Empty,
                ["DeviceName"] = DeviceName ?? string.Empty,
                ["DeviceType"] = Type.ToString(),
                ["IsEnabled"] = IsEnabled.ToString(),
                ["GroupId"] = GroupId ?? string.Empty,
                ["SlotId"] = SlotId ?? string.Empty,
                ["Manufacturer"] = Manufacturer ?? string.Empty,
                ["Model"] = Model ?? string.Empty,
                ["SerialNumber"] = SerialNumber ?? string.Empty
            };
        }

        #endregion

        #region 配置辅助方法

        /// <summary>
        /// 生成设备ID（子类必须实现）
        /// 生成后应设置 DeviceId 属性
        /// </summary>
        public abstract string GenerateDeviceId();

        /// <summary>
        /// 克隆配置（子类必须实现）
        /// </summary>
        public abstract DeviceConfig Clone();

        /// <summary>
        /// 初始化设备ID和类型（子类构造函数中调用）
        /// </summary>
        protected void InitializeDeviceInfo(DeviceType type)
        {
            Type = type;
            DeviceId = GenerateDeviceId();

            if (string.IsNullOrWhiteSpace(DeviceId))
            {
                throw new InvalidOperationException("GenerateDeviceId() 必须返回非空的设备ID");
            }
        }

        public List<string> GetHotUpdateableProperties()
        {
            return GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(HotUpdatableAttribute), true).Any())
                .Select(p => p.Name)
                .ToList();
        }

        public List<string> GetRestartRequiredProperties()
        {
            return GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(RequireRestartAttribute), true).Any())
                .Select(p => p.Name)
                .ToList();
        }

        public List<string> GetChangedProperties(DeviceConfig other)
        {
            if (other == null || GetType() != other.GetType())
                return new List<string>();

            var changedProperties = new List<string>();

            foreach (var prop in GetType().GetProperties())
            {
                var value1 = prop.GetValue(this);
                var value2 = prop.GetValue(other);

                if (!Equals(value1, value2))
                {
                    changedProperties.Add(prop.Name);
                }
            }

            return changedProperties;
        }

        /// <summary>
        /// 获取变更的属性列表（IConfig 接口兼容方法）
        /// </summary>
        public List<string> GetChangedProperties(IConfig other)
        {
            if (other is DeviceConfig deviceConfig)
            {
                return GetChangedProperties(deviceConfig);
            }
            return new List<string>();
        }

        public virtual OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            // 设备名称验证
            if (string.IsNullOrWhiteSpace(DeviceName))
            {
                errors.Add("设备名称不能为空");
            }
            else if (DeviceName.Length > 100)
            {
                errors.Add($"设备名称长度不能超过100个字符（当前长度：{DeviceName.Length}）");
            }

            // 厂家和型号验证（如果设备类型需要）
            if (RequiresManufacturerAndModel())
            {
                if (string.IsNullOrWhiteSpace(Manufacturer))
                {
                    errors.Add("必须选择设备厂家");
                }

                if (string.IsNullOrWhiteSpace(Model))
                {
                    errors.Add("必须选择设备型号");
                }
            }

            // 根据规格验证（如果存在）
            var spec = DeviceSpecificationRegistry.GetSpecification(Type, Manufacturer, Model);
            if (spec != null)
            {
                var specErrors = ValidateAgainstSpecification(spec);
                errors.AddRange(specErrors);
            }

            if (errors.Count > 0)
            {
                var errorMessage = $"配置验证失败，发现 {errors.Count} 个问题：" + Environment.NewLine + string.Join(Environment.NewLine + "  - ", errors);
                return OperationResult<bool>.Failure(errorMessage, ErrorCodes.InvalidConfig);
            }

            return OperationResult<bool>.Succeed(true, "配置验证通过");
        }

        /// <summary>
        /// 判断是否需要厂家和型号（子类可重写）
        /// </summary>
        protected virtual bool RequiresManufacturerAndModel()
        {
            // 默认情况下，如果指定了厂家，则必须指定型号
            return !string.IsNullOrWhiteSpace(Manufacturer);
        }

        /// <summary>
        /// 根据规格验证配置（子类可重写以实现特定验证逻辑）
        /// </summary>
        protected virtual List<string> ValidateAgainstSpecification(IDeviceSpecification specification)
        {
            return new List<string>(); // 默认不进行额外验证
        }

        public virtual Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in GetType().GetProperties())
            {
                if (prop.CanRead)
                {
                    dict[prop.Name] = prop.GetValue(this);
                }
            }
            return dict;
        }

        #endregion
    }
}
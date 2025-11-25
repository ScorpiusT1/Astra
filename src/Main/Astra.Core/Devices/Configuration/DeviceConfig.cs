using Astra.Core.Foundation.Common;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Configuration;
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

        private string _deviceName;
        private bool _isEnabled = true;
        private string _groupId = "G0";
        private string _slotId = "S0";

        [HotUpdatable]
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
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

        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        protected virtual void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                var oldValue = field;
                field = value;
                OnPropertyChanged(new PropertyChangedEventArgs
                {
                    PropertyName = propertyName,
                    OldValue = oldValue,
                    NewValue = value
                });
            }
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);

            // 跳过 ModifiedAt 和 CreatedAt 的变更事件触发（避免循环）
            if (e.PropertyName == nameof(ModifiedAt) || e.PropertyName == nameof(CreatedAt))
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

        #region IDeviceInfo 接口方法实现

        /// <summary>
        /// 获取设备信息字典
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
                ["SlotId"] = SlotId ?? string.Empty
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

            if (errors.Count > 0)
            {
                var errorMessage = $"配置验证失败，发现 {errors.Count} 个问题：" + Environment.NewLine + string.Join(Environment.NewLine + "  - ", errors);
                return OperationResult<bool>.Failure(errorMessage, ErrorCodes.InvalidConfig);
            }

            return OperationResult<bool>.Succeed(true, "配置验证通过");
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
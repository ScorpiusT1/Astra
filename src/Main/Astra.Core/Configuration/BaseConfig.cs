using Astra.Core.Foundation.Common;
using Astra.Core.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置基类
    /// 提供所有配置的通用功能实现
    /// </summary>
    public abstract class BaseConfig : IConfig
    {
        private string _configId;
        private string _configName;
        private bool _isEnabled = true;
        private int _version = 1;
        private DateTime _createdAt;
        private DateTime _modifiedAt;

        /// <summary>
        /// 配置ID（唯一标识）
        /// </summary>
        public string ConfigId
        {
            get => _configId;
            set => SetProperty(ref _configId, value, nameof(ConfigId));
        }

        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName
        {
            get => _configName;
            set => SetProperty(ref _configName, value, nameof(ConfigName));
        }

        /// <summary>
        /// 配置类型（子类必须实现）
        /// </summary>
        public abstract string ConfigType { get; }

        /// <summary>
        /// 配置是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value, nameof(IsEnabled));
        }

        /// <summary>
        /// 配置版本号
        /// </summary>
        public int Version
        {
            get => _version;
            set => SetProperty(ref _version, value, nameof(Version));
        }

        /// <summary>
        /// 配置创建时间（直接赋值，不触发变更事件）
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => _createdAt = value;
        }

        /// <summary>
        /// 配置最后修改时间（直接赋值，不触发变更事件）
        /// </summary>
        public DateTime ModifiedAt
        {
            get => _modifiedAt;
            set => _modifiedAt = value;
        }

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        protected BaseConfig()
        {
            _createdAt = DateTime.Now;
            _modifiedAt = DateTime.Now;
            ConfigId = GenerateConfigId();
        }

        /// <summary>
        /// 生成配置ID（子类可以重写）
        /// </summary>
        protected virtual string GenerateConfigId()
        {
            return $"{ConfigType}_{Guid.NewGuid():N}";
        }

        #region 属性变更通知

        protected virtual void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                var oldValue = field;
                field = value;
                
                // 直接赋值 ModifiedAt，避免触发变更事件导致无限递归
                _modifiedAt = DateTime.Now;
                
                // 跳过 ModifiedAt 和 CreatedAt 的变更事件触发（避免循环）
                if (propertyName != nameof(ModifiedAt) && propertyName != nameof(CreatedAt))
                {
                    OnPropertyChanged(propertyName, oldValue, value);
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            // 触发配置变更事件
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ConfigId = ConfigId,
                ConfigType = ConfigType,
                ChangedProperties = new List<string> { propertyName },
                OldConfig = Clone(),
                NewConfig = this,
                Timestamp = DateTime.Now,
                ChangedBy = "System"
            });
        }

        #endregion

        #region IConfig 接口实现

        /// <summary>
        /// 验证配置
        /// </summary>
        public virtual OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            // 基础验证
            if (string.IsNullOrWhiteSpace(ConfigId))
            {
                errors.Add("配置ID不能为空");
            }

            if (errors.Count > 0)
            {
                var errorMessage = $"配置验证失败，发现 {errors.Count} 个问题：" + Environment.NewLine + string.Join(Environment.NewLine + "  - ", errors);
                return OperationResult<bool>.Fail(errorMessage, ErrorCodes.InvalidConfig);
            }

            return OperationResult<bool>.Succeed(true, "配置验证通过");
        }

        /// <summary>
        /// 克隆配置（子类必须实现）
        /// </summary>
        public abstract IConfig Clone();

        /// <summary>
        /// 转换为字典
        /// </summary>
        public virtual Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value = prop.GetValue(this);
                        dict[prop.Name] = value;
                    }
                    catch
                    {
                        // 忽略无法读取的属性
                    }
                }
            }
            
            return dict;
        }

        /// <summary>
        /// 从字典加载配置
        /// </summary>
        public virtual void FromDictionary(Dictionary<string, object> dictionary)
        {
            if (dictionary == null)
                return;

            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
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

        #region 辅助方法

        /// <summary>
        /// 获取变更的属性列表
        /// </summary>
        public List<string> GetChangedProperties(IConfig other)
        {
            if (other == null || GetType() != other.GetType())
                return new List<string>();

            var changedProperties = new List<string>();
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value1 = prop.GetValue(this);
                        var value2 = prop.GetValue(other);

                        if (!Equals(value1, value2))
                        {
                            changedProperties.Add(prop.Name);
                        }
                    }
                    catch
                    {
                        // 忽略无法读取的属性
                    }
                }
            }

            return changedProperties;
        }

        /// <summary>
        /// 获取需要重启的属性列表
        /// </summary>
        public List<string> GetRestartRequiredProperties()
        {
            return GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(Devices.Configuration.RequireRestartAttribute), true).Any())
                .Select(p => p.Name)
                .ToList();
        }

        /// <summary>
        /// 获取支持热更新的属性列表
        /// </summary>
        public List<string> GetHotUpdateableProperties()
        {
            return GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(Devices.Configuration.HotUpdatableAttribute), true).Any())
                .Select(p => p.Name)
                .ToList();
        }

        #endregion
    }
}


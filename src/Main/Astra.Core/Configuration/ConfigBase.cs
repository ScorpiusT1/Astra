using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置基类 - 提供IConfig的默认实现，减少重复代码（DRY原则）
    /// 符合里氏替换原则（LSP），子类可以安全替换基类
    /// </summary>
    public abstract class ConfigBase : IClonableConfig, IObservableConfig
    {
        private string _configId;
        private string _name;
        private DateTime? _updatedAt;
        private int _version;

        /// <summary>
        /// 配置唯一标识符（支持JSON序列化/反序列化）
        /// 注意：Newtonsoft.Json 可以访问 internal setter，支持反序列化
        /// </summary>
        public string ConfigId
        {
            get => _configId;
            // 使用 internal setter 以支持 JSON 反序列化（在同一程序集内可访问）
            // 外部访问通过 SetConfigId 方法，确保只在 ConfigId 为空时设置
            set => _configId = value;
        }

        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    var oldValue = _name;
                    _name = value;
                    OnPropertyChanged(nameof(ConfigName), oldValue, value);
                }
            }
        }

        /// <summary>
        /// 创建时间（只读）
        /// </summary>    
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>     
        public DateTime? UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (_updatedAt != value)
                {
                    var oldValue = _updatedAt;
                    _updatedAt = value;
                    OnPropertyChanged(nameof(UpdatedAt), oldValue, value);
                }
            }
        }

        /// <summary>
        /// 配置版本号
        /// </summary>
        public int Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    var oldValue = _version;
                    _version = value;
                    OnPropertyChanged(nameof(Version), oldValue, value);
                }
            }
        }

        private string _configTypeName;

        /// <summary>
        /// 配置类型（只读，返回配置的实际类型）
        /// 用于快速识别配置类型，避免使用反射获取类型
        /// 注意：此属性不应被序列化，因为 Type 对象序列化后无法正确反序列化
        /// </summary>
        [JsonIgnore]
        public Type ConfigType => GetType();

        /// <summary>
        /// 配置类型名称（可读写，用于序列化到配置文件）
        /// 序列化时自动存储类型名称，反序列化时可用于验证类型
        /// </summary>
        public string ConfigTypeName
        {
            get
            {
                // 如果未设置，返回当前类型的完整名称
                if (string.IsNullOrWhiteSpace(_configTypeName))
                {
                    return GetType().AssemblyQualifiedName ?? GetType().FullName ?? GetType().Name;
                }
                return _configTypeName;
            }
            set => _configTypeName = value;
        }

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        /// <summary>
        /// 无参构造函数（用于JSON反序列化）
        /// 注意：不在这里初始化 ConfigId，让 JSON 反序列化器设置
        /// 如果 JSON 中没有 ConfigId，需要在反序列化后通过其他方式设置
        /// </summary>
        protected ConfigBase()
        {
            // JSON反序列化时使用，不初始化 ConfigId
            // Newtonsoft.Json 反序列化器会通过 internal setter 设置 ConfigId
            // ConfigTypeName 会在反序列化时自动设置
        }

        /// <summary>
        /// 带ConfigId的构造函数
        /// </summary>
        /// <param name="configId">配置ID</param>
        protected ConfigBase(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
                configId = Guid.NewGuid().ToString();
            else
            {
                _configId = configId;
            }
               
            CreatedAt = DateTime.Now;
            _version = 1;
        }

        /// <summary>
        /// 克隆构造函数（用于派生类实现克隆）
        /// </summary>
        /// <param name="source">源配置</param>
        /// <param name="newConfigId">新配置ID</param>
        protected ConfigBase(ConfigBase source, string newConfigId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(newConfigId))
                throw new ArgumentException("配置ID不能为空", nameof(newConfigId));

            _configId = newConfigId;
            _name = source.ConfigName;
            CreatedAt = DateTime.Now;
            _version = 1;
        }

        /// <summary>
        /// 获取 JSON 序列化设置（子类可以重写以自定义序列化选项）
        /// </summary>
        protected virtual JsonSerializerSettings GetJsonSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                // 支持 internal setter
                ContractResolver = new DefaultContractResolver()
            };
        }

        /// <summary>
        /// 序列化当前配置实例为 JSON 字符串
        /// 子类可以重写此方法以自定义序列化行为（如排除某些属性、转换数据格式等）
        /// </summary>
        /// <returns>JSON 字符串</returns>
        public virtual string Serialize()
        {
            var settings = GetJsonSettings();
            return JsonConvert.SerializeObject(this, GetType(), settings);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化到当前实例
        /// 子类可以重写此方法以自定义反序列化行为（如数据验证、属性转换等）
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        public virtual void Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 字符串不能为空", nameof(json));

            var settings = GetJsonSettings();
            var deserialized = JsonConvert.DeserializeObject(json, GetType(), settings) as ConfigBase;
            
            if (deserialized == null)
                throw new InvalidOperationException($"反序列化失败：无法将 JSON 转换为 {GetType().Name}");

            // 复制属性到当前实例
            CopyFrom(deserialized);
        }

        /// <summary>
        /// 从 JSON 字符串创建新的配置实例（静态工厂方法）
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <param name="json">JSON 字符串</param>
        /// <returns>配置实例</returns>
        public static T Deserialize<T>(string json) where T : ConfigBase
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 字符串不能为空", nameof(json));

            // 使用默认设置
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = new DefaultContractResolver()
            };

            var instance = JsonConvert.DeserializeObject<T>(json, settings);
            
            if (instance == null)
                throw new InvalidOperationException($"反序列化失败：无法将 JSON 转换为 {typeof(T).Name}");

            return instance;
        }

        /// <summary>
        /// 从另一个配置实例复制属性（用于反序列化）
        /// 子类可以重写此方法以自定义属性复制逻辑
        /// </summary>
        /// <param name="source">源配置实例</param>
        protected virtual void CopyFrom(ConfigBase source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // 复制所有可序列化的属性
            _configId = source._configId;
            _name = source.ConfigName;
            _updatedAt = source.UpdatedAt;
            _version = source.Version;
            _configTypeName = source.ConfigTypeName;
            // CreatedAt 不复制，保持原值（如果需要复制，可以添加：CreatedAt = source.CreatedAt;）
        }

        /// <summary>
        /// 克隆配置（使用自定义序列化方法）
        /// </summary>
        public virtual IConfig Clone()
        {
            // 使用自定义的序列化方法
            var json = Serialize();
            var cloned = Deserialize<ConfigBase>(json);
            
            if (cloned != null)
            {
                // 重置元数据
                cloned._configId = Guid.NewGuid().ToString();
                cloned.CreatedAt = DateTime.Now;
                cloned._updatedAt = null;
                cloned._version = 1;
            }
            
            return cloned;
        }

        /// <summary>
        /// 克隆并指定新ID
        /// </summary>
        public IConfig CloneWithId(string newConfigId)
        {
            var cloned = Clone() as ConfigBase;
            if (cloned != null)
            {
                cloned._configId = newConfigId;
            }
            return cloned;
        }

        /// <summary>
        /// 标记配置已更新
        /// </summary>
        public virtual void MarkAsUpdated()
        {
            UpdatedAt = DateTime.Now;
            Version++;
        }

        /// <summary>
        /// 属性变更通知
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        /// <summary>
        /// 设置ConfigId（仅供内部使用，用于反序列化后设置ID）
        /// 仅在 ConfigId 为空时才设置，确保反序列化时不被覆盖
        /// </summary>
        /// <param name="configId">配置ID</param>
        public void SetConfigId(string configId)
        {
            if (string.IsNullOrWhiteSpace(_configId) && !string.IsNullOrWhiteSpace(configId))
            {
                _configId = configId;
            }
        }

        /// <summary>
        /// 获取配置的显示名称（用于树节点等UI显示）
        /// 子类可以重写此方法以自定义显示名称格式
        /// 默认返回 ConfigName
        /// </summary>
        /// <returns>配置的显示名称</returns>
        public virtual string GetDisplayName()
        {
            return string.IsNullOrEmpty(ConfigName) ? "未命名配置" : ConfigName;
        }

        public override string ToString()
        {
            return $"[{GetType().Name}] Id={ConfigId}, ConfigName={ConfigName}, Version={Version}";
        }
    }
}

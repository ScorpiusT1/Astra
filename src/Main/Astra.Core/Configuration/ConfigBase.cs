using System;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// 注意：System.Text.Json 默认不支持 protected setter，需要通过其他机制设置
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
        /// 注意：此属性不应被序列化，因为 System.Text.Json 不支持序列化 Type 对象
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
        [JsonConstructor]
        protected ConfigBase()
        {
            // JSON反序列化时使用，不初始化 ConfigId
            // JSON 反序列化器会通过 protected setter 设置 ConfigId
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
        /// 克隆配置（使用JSON序列化实现深拷贝）
        /// </summary>
        public virtual IConfig Clone()
        {
            var json = JsonSerializer.Serialize(this, GetType());
            var cloned = JsonSerializer.Deserialize(json, GetType()) as ConfigBase;
            
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

        public override string ToString()
        {
            return $"[{GetType().Name}] Id={ConfigId}, ConfigName={ConfigName}, Version={Version}";
        }
    }
}

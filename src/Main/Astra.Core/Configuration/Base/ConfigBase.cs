using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Astra.Core.Configuration.Base
{
    /// <summary>
    /// 配置基类 - 提供IConfig的默认实现，减少重复代码（DRY原则）
    /// 符合里氏替换原则（LSP），子类可以安全替换基类
    /// 自己实现 INotifyPropertyChanged 接口，不依赖外部库
    /// </summary>
    public abstract class ConfigBase : IClonableConfig, IObservableConfig, INotifyPropertyChanged
    {
        private string _configId;
        private string _name;
        private DateTime? _updatedAt;
        private int _version;

        /// <summary>
        /// 配置唯一标识符。由框架在首次保存时分配，
        /// 请通过 <see cref="SetConfigId"/> 设置（仅在 ID 为空时生效，防止覆盖已有值）。
        /// </summary>
        public string ConfigId
        {
            get => _configId;
            set => _configId = value;
        }

        /// <summary>配置名称</summary>
        public string ConfigName
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>创建时间（只读）</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>最后更新时间</summary>
        public DateTime? UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        /// <summary>配置版本号</summary>
        public int Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        private string _configTypeName;

        /// <summary>
        /// 配置类型（只读，返回配置的实际类型）
        /// </summary>
        [JsonIgnore]
        public Type ConfigType => GetType();

        /// <summary>
        /// 配置类型名称（可读写，用于序列化到配置文件）
        /// </summary>
        public string ConfigTypeName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_configTypeName))
                    return GetType().AssemblyQualifiedName ?? GetType().FullName ?? GetType().Name;
                return _configTypeName;
            }
            set => _configTypeName = value;
        }

        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 无参构造函数（用于JSON反序列化）
        /// </summary>
        protected ConfigBase() { }

        /// <summary>
        /// 带ConfigId的构造函数
        /// </summary>
        protected ConfigBase(string configId)
        {
            _configId = string.IsNullOrWhiteSpace(configId) ? Guid.NewGuid().ToString() : configId;
            CreatedAt = DateTime.Now;
            _version = 1;
        }

        /// <summary>
        /// 克隆构造函数（用于派生类实现克隆）
        /// </summary>
        protected ConfigBase(ConfigBase source, string newConfigId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(newConfigId)) throw new ArgumentException("配置ID不能为空", nameof(newConfigId));

            _configId = newConfigId;
            _name = source.ConfigName;
            CreatedAt = DateTime.Now;
            _version = 1;
        }

        /// <summary>
        /// 共享的 JSON 序列化设置（所有 ConfigBase 实例统一使用，避免重复创建）。
        /// </summary>
        private static readonly JsonSerializerSettings DefaultJsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            ContractResolver = new DefaultContractResolver()
        };

        protected virtual JsonSerializerSettings GetJsonSettings() => DefaultJsonSettings;

        public virtual string Serialize()
            => JsonConvert.SerializeObject(this, GetType(), GetJsonSettings());

        public virtual void Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON 字符串不能为空", nameof(json));
            var deserialized = JsonConvert.DeserializeObject(json, GetType(), GetJsonSettings()) as ConfigBase
                ?? throw new InvalidOperationException($"反序列化失败：无法将 JSON 转换为 {GetType().Name}");
            CopyFrom(deserialized);
        }

        public static T Deserialize<T>(string json) where T : ConfigBase
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON 字符串不能为空", nameof(json));
            return JsonConvert.DeserializeObject<T>(json, DefaultJsonSettings)
                ?? throw new InvalidOperationException($"反序列化失败：无法将 JSON 转换为 {typeof(T).Name}");
        }

        protected virtual void CopyFrom(ConfigBase source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _configId = source._configId;
            _name = source.ConfigName;
            _updatedAt = source.UpdatedAt;
            _version = source.Version;
            _configTypeName = source.ConfigTypeName;
        }

        public virtual IConfig Clone()
        {
            var cloned = JsonConvert.DeserializeObject(Serialize(), GetType(), GetJsonSettings()) as ConfigBase
                ?? throw new InvalidOperationException($"克隆失败：无法反序列化 {GetType().Name}");
            cloned._configId = Guid.NewGuid().ToString();
            cloned.CreatedAt = DateTime.Now;
            cloned._updatedAt = null;
            cloned._version = 1;
            return cloned;
        }

        public IConfig CloneWithId(string newConfigId)
        {
            var cloned = (ConfigBase)Clone();
            cloned._configId = newConfigId;
            return cloned;
        }

        public virtual void MarkAsUpdated()
        {
            UpdatedAt = DateTime.Now;
            Version++;
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            var oldValue = field;
            field = value;
            OnPropertyChanged(propertyName, oldValue, value);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => OnPropertyChanged(propertyName, null, null);

        protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        /// <summary>
        /// 设置ConfigId（仅在 ConfigId 为空时生效，避免覆盖反序列化结果）
        /// </summary>
        public void SetConfigId(string configId)
        {
            if (string.IsNullOrWhiteSpace(_configId) && !string.IsNullOrWhiteSpace(configId))
                _configId = configId;
        }

        public virtual string GetDisplayName()
            => string.IsNullOrEmpty(ConfigName) ? "未命名配置" : ConfigName;

        public override string ToString()
            => $"[{GetType().Name}] Id={ConfigId}, ConfigName={ConfigName}, Version={Version}";
    }
}

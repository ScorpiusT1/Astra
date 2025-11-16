using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Base
{
    /// <summary>
    /// 设备基类
    /// 提供所有设备的基础功能：设备信息（从配置获取）、连接管理、配置管理
    /// 数据传输功能由子类根据需要实现（通过实现 IDataTransfer 接口）
    /// </summary>
    /// <typeparam name="TConfig">设备配置类型，必须继承自 DeviceConfig</typeparam>
    public abstract class DeviceBase<TConfig> : IDevice, IConfigurable<TConfig>
        where TConfig : DeviceConfig
    {
        protected readonly DeviceConnectionBase _connection;
        protected TConfig _config;
        private readonly object _configLock = new object();

        public DeviceStatus Status => _connection.Status;
        public bool IsOnline => _connection.IsOnline;

        #region IDeviceInfo 接口实现（委托给 _config）

        public string DeviceId => _config?.DeviceId ?? string.Empty;
        public string DeviceName
        {
            get => _config?.DeviceName ?? string.Empty;
            set
            {
                if (_config != null)
                    _config.DeviceName = value;
            }
        }
        public DeviceType Type => _config?.Type ?? DeviceType.Custom;
        public bool IsEnabled
        {
            get => _config?.IsEnabled ?? true;
            set
            {
                if (_config != null)
                    _config.IsEnabled = value;
            }
        }
        public string GroupId
        {
            get => _config?.GroupId ?? string.Empty;
            set
            {
                if (_config != null)
                    _config.GroupId = value;
            }
        }
        public string SlotId
        {
            get => _config?.SlotId ?? string.Empty;
            set
            {
                if (_config != null)
                    _config.SlotId = value;
            }
        }
        public Dictionary<string, string> GetDeviceInfo() => _config?.GetDeviceInfo() ?? new Dictionary<string, string>();

        #endregion

        #region IConfigurable<TConfig> 接口实现

        public TConfig CurrentConfig
        {
            get
            {
                lock (_configLock)
                {
                    return _config != null ? (TConfig)_config.Clone() : null;
                }
            }
        }

        public event EventHandler<ConfigChangedEventArgs<TConfig>> ConfigChanged;

        public virtual OperationResult ApplyConfig(TConfig newConfig)
        {
            if (newConfig == null)
                return OperationResult.Fail("配置不能为空", ErrorCodes.InvalidData);

            var validateResult = ValidateConfig(newConfig);
            if (!validateResult.Success)
                return OperationResult.Fail($"配置验证失败: {validateResult.ErrorMessage}", ErrorCodes.InvalidConfig);

            lock (_configLock)
            {
                if (_config == null)
                {
                    _config = (TConfig)newConfig.Clone();
                    return OperationResult.Succeed("配置已设置");
                }

                var oldConfig = (TConfig)_config.Clone();
                var changedProps = _config.GetChangedProperties(newConfig);

                if (changedProps.Count == 0)
                    return OperationResult.Succeed("配置无变更");

                var restartRequired = newConfig.GetRestartRequiredProperties()
                    .Intersect(changedProps)
                    .ToList();

                if (restartRequired.Any() && IsOnline)
                {
                    return OperationResult.Fail(
                        $"设备在线时无法更新需要重启的配置项: {string.Join(", ", restartRequired)}",
                        ErrorCodes.ConfigRequireRestart);
                }

                try
                {
                    // 应用可热更新的配置
                    ApplyHotUpdateProperties(newConfig, changedProps);

                    _config = (TConfig)newConfig.Clone();

                    OnConfigChanged(new ConfigChangedEventArgs<TConfig>
                    {
                        OldConfig = oldConfig,
                        NewConfig = (TConfig)_config.Clone(),
                        ChangedProperties = changedProps
                    });

                    return OperationResult.Succeed($"配置应用成功，变更了 {changedProps.Count} 个属性")
                        .WithData("ChangedProperties", changedProps)
                        .WithData("RestartRequired", restartRequired.Any());
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"应用配置失败: {ex.Message}", ex, ErrorCodes.ConfigApplyFailed);
                }
            }
        }

        public virtual OperationResult<bool> ValidateConfig(TConfig config)
        {
            return config?.Validate() ?? OperationResult<bool>.Fail("配置不能为空", ErrorCodes.InvalidData);
        }

        public virtual List<string> GetHotUpdateableProperties()
        {
            return _config?.GetHotUpdateableProperties() ?? new List<string>();
        }

        /// <summary>
        /// 应用热更新属性（子类可重写以处理特定属性）
        /// </summary>
        protected virtual void ApplyHotUpdateProperties(TConfig newConfig, List<string> changedProps)
        {
            
        }

        protected virtual void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 子类可重写以处理特定属性变更
        }

        protected virtual void OnConfigChanged(ConfigChangedEventArgs<TConfig> e)
        {
            ConfigChanged?.Invoke(this, e);
        }

        #endregion

        #region 事件

        public event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;
        public event EventHandler<DeviceErrorEventArgs> ErrorOccurred;

        protected virtual void OnStatusChanged(DeviceStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(DeviceErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// 触发错误事件的便捷方法
        /// </summary>
        protected virtual void RaiseError(string errorMessage, int errorCode = -1, Exception exception = null)
        {
            OnErrorOccurred(new DeviceErrorEventArgs
            {
                DeviceId = DeviceId,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                Exception = exception
            });
        }

        #endregion

        #region 构造函数

        protected DeviceBase(
            DeviceConnectionBase connection,
            TConfig config)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 验证配置必须已初始化设备信息
            if (string.IsNullOrWhiteSpace(_config.DeviceId))
            {
                throw new ArgumentException("配置的 DeviceId 不能为空，请在配置构造函数中调用 InitializeDeviceInfo()", nameof(config));
            }

            // 订阅连接层的事件
            _connection.StatusChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Id))
                {
                    e.Id = DeviceId;
                }

                OnStatusChanged(e);
            };
            _connection.ErrorOccurred += (s, e) => OnErrorOccurred(e);

            // 订阅配置的属性变更事件
            _config.PropertyChanged += OnConfigPropertyChanged;
        }

        #endregion

        #region IDeviceConnection 接口实现（委托给 _connection）

        public OperationResult Connect() => _connection.Connect();
        public Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
            => _connection.ConnectAsync(cancellationToken);

        public OperationResult Disconnect() => _connection.Disconnect();
        public Task<OperationResult> DisconnectAsync(CancellationToken cancellationToken = default)
            => _connection.DisconnectAsync(cancellationToken);

        public OperationResult<bool> DeviceExists() => _connection.DeviceExists();
        public Task<OperationResult<bool>> DeviceExistsAsync(CancellationToken cancellationToken = default)
            => _connection.DeviceExistsAsync(cancellationToken);

        public OperationResult<bool> IsAlive() => _connection.IsAlive();
        public Task<OperationResult<bool>> IsAliveAsync(CancellationToken cancellationToken = default)
            => _connection.IsAliveAsync(cancellationToken);

        public OperationResult Reset() => _connection.Reset();
        public Task<OperationResult> ResetAsync(CancellationToken cancellationToken = default)
            => _connection.ResetAsync(cancellationToken);

        #endregion

        #region IDisposable 实现

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed)
                return;

            Disconnect();
            _connection?.Dispose();

            _disposed = true;
        }

        #endregion
    }
}


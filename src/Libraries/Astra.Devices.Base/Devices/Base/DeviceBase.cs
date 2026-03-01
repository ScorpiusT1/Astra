using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Base
{
    /// <summary>
    /// 设备基类
    /// </summary>
    public abstract class DeviceBase<TConfig> : IDevice, IConfigurable<TConfig>
        where TConfig : DeviceConfig
    {
        protected readonly DeviceConnectionBase _connection;
        protected TConfig _config;
        private readonly object _configLock = new object();

        public DeviceStatus Status => _connection.Status;
        public bool IsOnline => _connection.IsOnline;

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

        public string Manufacturer
        {
            get => _config?.Manufacturer ?? string.Empty;
            set
            {
                if (_config != null)
                    _config.Manufacturer = value;
            }
        }

        public string Model
        {
            get => _config?.Model ?? string.Empty;
            set
            {
                if (_config != null)
                    _config.Model = value;
            }
        }

        public string SerialNumber
        {
            get => _config?.SerialNumber ?? string.Empty;
            set
            {
                if (_config != null)
                    _config.SerialNumber = value;
            }
        }

        public Dictionary<string, string> GetDeviceInfo() => _config?.GetDeviceInfo() ?? new Dictionary<string, string>();

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
                return OperationResult.Failure("配置不能为空", ErrorCodes.InvalidData);

            var validateResult = ValidateConfig(newConfig);
            if (!validateResult.Success)
                return OperationResult.Failure($"配置验证失败: {validateResult.ErrorMessage}", ErrorCodes.InvalidConfig);

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
                    return OperationResult.Failure(
                        $"设备在线时无法更新需要重启的配置项: {string.Join(", ", restartRequired)}",
                        ErrorCodes.ConfigRequireRestart);
                }

                try
                {
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
            return config?.Validate() ?? OperationResult<bool>.Failure("配置不能为空", ErrorCodes.InvalidData);
        }

        public virtual List<string> GetHotUpdateableProperties()
        {
            return _config?.GetHotUpdateableProperties() ?? new List<string>();
        }

        protected virtual void ApplyHotUpdateProperties(TConfig newConfig, List<string> changedProps)
        {
        }

        protected virtual void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        protected virtual void OnConfigChanged(ConfigChangedEventArgs<TConfig> e)
        {
            ConfigChanged?.Invoke(this, e);
        }

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

        protected DeviceBase(
            DeviceConnectionBase connection,
            TConfig config)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(_config.DeviceId))
            {
                throw new ArgumentException("配置的 DeviceId 不能为空，请在配置构造函数中调用 InitializeDeviceInfo()", nameof(config));
            }

            _connection.StatusChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Id))
                {
                    e.Id = DeviceId;
                }

                OnStatusChanged(e);
            };
            _connection.ErrorOccurred += (s, e) => OnErrorOccurred(e);

            _config.PropertyChanged += OnConfigPropertyChanged;
        }

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

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed)
                return;

            Disconnect();
            _connection?.Dispose();

            _disposed = true;
        }
    }
}


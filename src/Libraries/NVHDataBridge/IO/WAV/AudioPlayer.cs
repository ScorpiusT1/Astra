using NAudio.Wave;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NVHDataBridge.IO.WAV
{
    /// <summary>
    /// 音频播放状态
    /// </summary>
    public enum AudioPlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    /// <summary>
    /// 音频播放器 - 提供音频文件和数据播放功能
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var player = new AudioPlayer())
    /// {
    ///     player.PlayFile("audio.wav");
    ///     player.WaitForCompletion();
    /// }
    /// </code>
    /// 
    /// 播放内存数据:
    /// <code>
    /// using (var player = new AudioPlayer())
    /// {
    ///     float[] samples = LoadAudioData();
    ///     player.PlaySamples(samples, 44100, 2);
    ///     player.WaitForCompletion();
    /// }
    /// </code>
    /// </example>
    public class AudioPlayer : IDisposable
    {
        #region 私有字段

        private WaveOutEvent _waveOut;
        private AudioFileReader _audioFile;
        private WaveStream _waveStream;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        private int _deviceNumber;

        #endregion

        #region 事件

        /// <summary>
        /// 播放完成事件
        /// </summary>
        public event EventHandler PlaybackFinished;

        /// <summary>
        /// 播放状态改变事件
        /// </summary>
        public event EventHandler<AudioPlaybackState> PlaybackStateChanged;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取当前播放状态
        /// </summary>
        public AudioPlaybackState PlaybackState
        {
            get
            {
                if (_waveOut == null)
                    return AudioPlaybackState.Stopped;

                switch (_waveOut.PlaybackState)
                {
                    case NAudio.Wave.PlaybackState.Playing:
                        return AudioPlaybackState.Playing;
                    case NAudio.Wave.PlaybackState.Paused:
                        return AudioPlaybackState.Paused;
                    default:
                        return AudioPlaybackState.Stopped;
                }
            }
        }

        /// <summary>
        /// 获取或设置音量（0.0 - 1.0）
        /// </summary>
        public float Volume
        {
            get => _waveOut?.Volume ?? 1.0f;
            set
            {
                if (_waveOut != null)
                {
                    _waveOut.Volume = Math.Clamp(value, 0.0f, 1.0f);
                }
            }
        }

        /// <summary>
        /// 获取当前播放位置（秒）
        /// </summary>
        public double CurrentTime
        {
            get => _audioFile?.CurrentTime.TotalSeconds ?? 0.0;
            set
            {
                if (_audioFile != null && value >= 0)
                {
                    _audioFile.CurrentTime = TimeSpan.FromSeconds(value);
                }
            }
        }

        /// <summary>
        /// 获取音频总时长（秒）
        /// </summary>
        public double TotalTime
        {
            get
            {
                if (_audioFile != null)
                    return _audioFile.TotalTime.TotalSeconds;
                if (_waveStream != null && _waveStream.WaveFormat != null)
                    return (double)_waveStream.Length / _waveStream.WaveFormat.AverageBytesPerSecond;
                return 0.0;
            }
        }

        /// <summary>
        /// 获取音频格式
        /// </summary>
        public WaveFormat WaveFormat => _audioFile?.WaveFormat ?? _waveStream?.WaveFormat;

        #endregion

        #region 构造函数和静态方法

        /// <summary>
        /// 创建音频播放器实例（使用默认播放设备）
        /// </summary>
        public AudioPlayer()
        {
            _deviceNumber = -1; // -1表示使用默认设备
        }

        /// <summary>
        /// 创建音频播放器实例（指定播放设备）
        /// </summary>
        /// <param name="deviceNumber">设备编号（-1表示使用默认设备）</param>
        public AudioPlayer(int deviceNumber)
        {
            // 验证将在播放时进行，因为设备列表可能在运行时变化
            _deviceNumber = deviceNumber;
        }

        /// <summary>
        /// 获取所有可用的音频播放设备
        /// </summary>
        /// <returns>音频设备信息列表</returns>
        /// <remarks>
        /// 注意：WaveOutEvent使用Windows WaveOut API，此方法尝试检测可用的设备编号。
        /// 对于更详细的设备信息，建议使用操作系统API或NAudio的MMDeviceEnumerator。
        /// </remarks>
        public static AudioDeviceInfo[] GetAvailableDevices()
        {
            var devices = new System.Collections.Generic.List<AudioDeviceInfo>();

            // WaveOutEvent通过设备编号选择设备，但我们无法直接枚举设备列表
            // 这里提供一个简化实现，返回设备编号范围
            // 实际应用中，用户可以通过其他方式（如操作系统API）获取设备信息
            // 然后使用设备编号来创建AudioPlayer实例

            // 尝试检测可用的设备编号（0-31是常见的范围）
            for (int i = 0; i < 32; i++)
            {
                try
                {
                    using (var testOut = new WaveOutEvent())
                    {
                        testOut.DeviceNumber = i;
                        // 如果能设置成功，说明设备编号有效
                        devices.Add(new AudioDeviceInfo
                        {
                            DeviceNumber = i,
                            ProductName = $"WaveOut Device {i}",
                            ManufacturerName = "Windows Audio",
                            ProductGuid = Guid.Empty,
                            ManufacturerGuid = Guid.Empty,
                            Channels = 2,
                            Capabilities = default(WaveOutCapabilities)
                        });
                    }
                }
                catch
                {
                    // 设备编号无效，停止检测
                    break;
                }
            }

            // 如果没有检测到设备，至少返回默认设备（设备0）
            if (devices.Count == 0)
            {
                devices.Add(new AudioDeviceInfo
                {
                    DeviceNumber = 0,
                    ProductName = "Default Audio Device",
                    ManufacturerName = "System",
                    ProductGuid = Guid.Empty,
                    ManufacturerGuid = Guid.Empty,
                    Channels = 2,
                    Capabilities = default(WaveOutCapabilities)
                });
            }

            return devices.ToArray();
        }

        /// <summary>
        /// 获取默认音频播放设备信息
        /// </summary>
        /// <returns>默认设备信息（设备编号0）</returns>
        public static AudioDeviceInfo GetDefaultDevice()
        {
            return new AudioDeviceInfo
            {
                DeviceNumber = 0,
                ProductName = "Default Audio Device",
                ManufacturerName = "System",
                ProductGuid = Guid.Empty,
                ManufacturerGuid = Guid.Empty,
                Channels = 2,
                Capabilities = default(WaveOutCapabilities)
            };
        }

        #endregion

        #region 播放方法

        /// <summary>
        /// 播放WAV文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <exception cref="ArgumentException">文件路径无效</exception>
        /// <exception cref="System.IO.FileNotFoundException">文件不存在</exception>
        public void PlayFile(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            lock (_lockObject)
            {
                Stop();
                _audioFile = new AudioFileReader(filePath);
                InitializeWaveOut();
            }
        }

        /// <summary>
        /// 播放内存中的音频样本数据
        /// </summary>
        /// <param name="samples">归一化的浮点样本数组（-1.0到1.0）</param>
        /// <param name="sampleRate">采样率（Hz）</param>
        /// <param name="channels">通道数（1=单声道，2=立体声）</param>
        /// <param name="bitsPerSample">位深度（16或32，默认16）</param>
        public void PlaySamples(float[] samples, int sampleRate, int channels, int bitsPerSample = 16)
        {
            ThrowIfDisposed();

            if (samples == null || samples.Length == 0)
                throw new ArgumentException("样本数据不能为空", nameof(samples));
            if (sampleRate <= 0)
                throw new ArgumentException("采样率必须大于0", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("通道数必须大于0", nameof(channels));

            lock (_lockObject)
            {
                Stop();

                WaveFormat format = bitsPerSample == 32
                    ? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)
                    : new WaveFormat(sampleRate, bitsPerSample, channels);

                // 使用RawSourceWaveStream直接提供音频数据
                // 转换样本为字节
                int bytesPerSample = bitsPerSample / 8;
                int totalBytes = samples.Length * bytesPerSample * channels;
                byte[] buffer = new byte[totalBytes];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        float sample = Math.Clamp(samples[i], -1.0f, 1.0f);
                        short sample16 = (short)Math.Clamp(sample * 32768f, -32768f, 32767f);
                        BitConverter.GetBytes(sample16).CopyTo(buffer, i * bytesPerSample * channels);
                    }
                }
                else // 32-bit float
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        float sample = Math.Clamp(samples[i], -1.0f, 1.0f);
                        BitConverter.GetBytes(sample).CopyTo(buffer, i * bytesPerSample * channels);
                    }
                }

                _waveStream = new RawSourceWaveStream(buffer, 0, buffer.Length, format);
                _audioFile = null;
                InitializeWaveOutFromStream(_waveStream);
            }
        }

        /// <summary>
        /// 播放内存中的音频样本数据（双精度版本）
        /// </summary>
        /// <param name="samples">归一化的双精度样本数组（-1.0到1.0）</param>
        /// <param name="sampleRate">采样率（Hz）</param>
        /// <param name="channels">通道数</param>
        /// <param name="bitsPerSample">位深度（16或32，默认16）</param>
        public void PlaySamples(double[] samples, int sampleRate, int channels, int bitsPerSample = 16)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            float[] floatSamples = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                floatSamples[i] = (float)samples[i];
            }

            PlaySamples(floatSamples, sampleRate, channels, bitsPerSample);
        }

        /// <summary>
        /// 播放原始采集数据（自动归一化）- 适用于采集卡等原始数据
        /// </summary>
        /// <param name="rawData">原始采集数据（任意范围）</param>
        /// <param name="sampleRate">采样率（Hz）</param>
        /// <param name="channels">通道数（1=单声道，2=立体声）</param>
        /// <param name="normalizationMethod">归一化方法（默认峰值归一化）</param>
        /// <param name="bitsPerSample">位深度（16或32，默认16）</param>
        /// <example>
        /// 播放采集卡10秒数据:
        /// <code>
        /// double[] acquisitionData = GetDataFromAcquisitionCard(); // 原始数据，例如采集了10秒
        /// int sampleRate = 44100; // 采样率
        /// 
        /// using (var player = new AudioPlayer())
        /// {
        ///     // 自动归一化并播放完整数据
        ///     player.PlayRawData(acquisitionData, sampleRate, channels: 1);
        ///     player.WaitForCompletion(); // 等待播放完成（约10秒）
        /// }
        /// </code>
        /// </example>
        public void PlayRawData(double[] rawData, int sampleRate, int channels = 1,
            NormalizationMethod normalizationMethod = NormalizationMethod.Peak, int bitsPerSample = 16)
        {
            if (rawData == null || rawData.Length == 0)
                throw new ArgumentException("原始数据不能为空", nameof(rawData));
            if (sampleRate <= 0)
                throw new ArgumentException("采样率必须大于0", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("通道数必须大于0", nameof(channels));

            double normalizationFactor = 1.0;
            // 根据归一化方法处理数据
            double[] normalizedData;
            switch (normalizationMethod)
            {
                case NormalizationMethod.Peak:
                    normalizedData = AudioNormalization.NormalizePeak(rawData, out normalizationFactor);
                    break;
                case NormalizationMethod.RMS:
                    normalizedData = AudioNormalization.NormalizeRMS(rawData);
                    break;
                case NormalizationMethod.Logarithmic:
                    normalizedData = AudioNormalization.NormalizeLogarithmic(rawData);
                    break;
                case NormalizationMethod.AGC:
                    normalizedData = AudioNormalization.NormalizeAGC(rawData);
                    break;
                default:
                    normalizedData = AudioNormalization.NormalizePeak(rawData, out normalizationFactor); // 默认峰值归一化
                    break;
            }

            // 播放归一化后的数据
            PlaySamples(normalizedData, sampleRate, channels, bitsPerSample);
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                if (_waveOut != null && _waveOut.PlaybackState == NAudio.Wave.PlaybackState.Paused)
                {
                    _waveOut.Play();
                    OnPlaybackStateChanged(AudioPlaybackState.Playing);
                }
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                if (_waveOut != null && _waveOut.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                {
                    _waveOut.Pause();
                    OnPlaybackStateChanged(AudioPlaybackState.Paused);
                }
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut?.Dispose();
                    _waveOut = null;
                }

                _audioFile?.Dispose();
                _audioFile = null;
                _waveStream?.Dispose();
                _waveStream = null;
                OnPlaybackStateChanged(AudioPlaybackState.Stopped);
            }
        }

        /// <summary>
        /// 等待播放完成（同步）
        /// </summary>
        public void WaitForCompletion()
        {
            while (PlaybackState == AudioPlaybackState.Playing)
            {
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 等待播放完成（异步）
        /// </summary>
        public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
        {
            while (PlaybackState == AudioPlaybackState.Playing && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 初始化WaveOut输出设备
        /// </summary>
        private void InitializeWaveOut()
        {
            if (_audioFile == null)
                return;

            _waveOut = new WaveOutEvent();
            if (_deviceNumber >= 0)
            {
                _waveOut.DeviceNumber = _deviceNumber;
            }
            _waveOut.Init(_audioFile);
            _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
            _waveOut.Play();
            OnPlaybackStateChanged(AudioPlaybackState.Playing);
        }

        /// <summary>
        /// 从WaveStream初始化WaveOut输出设备
        /// </summary>
        private void InitializeWaveOutFromStream(WaveStream stream)
        {
            if (stream == null)
                return;

            _waveOut = new WaveOutEvent();
            if (_deviceNumber >= 0)
            {
                _waveOut.DeviceNumber = _deviceNumber;
            }
            _waveOut.Init(stream);
            _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
            _waveOut.Play();
            OnPlaybackStateChanged(AudioPlaybackState.Playing);
        }

        /// <summary>
        /// 播放停止事件处理
        /// </summary>
        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            OnPlaybackStateChanged(AudioPlaybackState.Stopped);
            OnPlaybackFinished();
        }

        /// <summary>
        /// 触发播放完成事件
        /// </summary>
        protected virtual void OnPlaybackFinished()
        {
            PlaybackFinished?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 触发播放状态改变事件
        /// </summary>
        protected virtual void OnPlaybackStateChanged(AudioPlaybackState state)
        {
            PlaybackStateChanged?.Invoke(this, state);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AudioPlayer));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();

            _waveOut?.Dispose();
            _audioFile?.Dispose();
            _waveStream?.Dispose();

            _isDisposed = true;
        }

        #endregion
    }
}


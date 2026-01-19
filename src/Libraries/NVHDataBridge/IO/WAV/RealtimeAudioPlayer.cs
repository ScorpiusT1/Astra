using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NVHDataBridge.IO.WAV
{
    /// <summary>
    /// 实时音频播放器 - 提供流式音频数据播放功能
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var player = new RealtimeAudioPlayer(44100, 2))
    /// {
    ///     player.Start();
    ///     
    ///     // 持续添加音频数据
    ///     while (hasMoreData)
    ///     {
    ///         float[] samples = GetNextAudioChunk();
    ///         player.AddSamples(samples);
    ///     }
    ///     
    ///     player.WaitForCompletion();
    /// }
    /// </code>
    /// </example>
    public class RealtimeAudioPlayer : IDisposable
    {
        #region 私有字段

        private readonly WaveOutEvent _waveOut;
        private readonly BufferedWaveProvider _bufferedProvider;
        private readonly WaveFormat _waveFormat;
        private readonly object _lockObject = new object();
        private bool _isDisposed;
        private bool _isStarted;

        // 缓冲区配置
        private const int DEFAULT_BUFFER_LENGTH_MS = 1000; // 默认1秒缓冲区
        private const int MIN_BUFFER_LENGTH_MS = 100;      // 最小100毫秒

        #endregion

        #region 事件

        /// <summary>
        /// 缓冲区数据不足事件（需要添加更多数据）
        /// </summary>
        public event EventHandler BufferUnderrun;

        /// <summary>
        /// 播放停止事件
        /// </summary>
        public event EventHandler PlaybackStopped;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取采样率（Hz）
        /// </summary>
        public int SampleRate => _waveFormat.SampleRate;

        /// <summary>
        /// 获取通道数
        /// </summary>
        public int Channels => _waveFormat.Channels;

        /// <summary>
        /// 获取音频格式
        /// </summary>
        public WaveFormat WaveFormat => _waveFormat;

        /// <summary>
        /// 获取或设置音量（0.0 - 1.0）
        /// </summary>
        public float Volume
        {
            get => _waveOut.Volume;
            set => _waveOut.Volume = Math.Clamp(value, 0.0f, 1.0f);
        }

        /// <summary>
        /// 获取当前缓冲区中的字节数
        /// </summary>
        public int BufferedBytes => _bufferedProvider.BufferedBytes;

        /// <summary>
        /// 获取当前缓冲区中的样本数
        /// </summary>
        public int BufferedSamples => BufferedBytes / _waveFormat.BlockAlign;

        /// <summary>
        /// 获取当前缓冲区时长（秒）
        /// </summary>
        public double BufferedDuration => (double)BufferedBytes / (_waveFormat.AverageBytesPerSecond);

        /// <summary>
        /// 获取是否正在播放
        /// </summary>
        public bool IsPlaying => _waveOut.PlaybackState == PlaybackState.Playing;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建实时音频播放器
        /// </summary>
        /// <param name="sampleRate">采样率（Hz）</param>
        /// <param name="channels">通道数（1=单声道，2=立体声）</param>
        /// <param name="bitsPerSample">位深度（16或32，默认16）</param>
        /// <param name="bufferLengthMs">缓冲区长度（毫秒），默认1000ms</param>
        public RealtimeAudioPlayer(int sampleRate, int channels, int bitsPerSample = 16, int bufferLengthMs = DEFAULT_BUFFER_LENGTH_MS)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("采样率必须大于0", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("通道数必须大于0", nameof(channels));
            if (bitsPerSample != 16 && bitsPerSample != 32)
                throw new ArgumentException("位深度必须为16或32", nameof(bitsPerSample));

            _waveFormat = bitsPerSample == 32
                ? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)
                : new WaveFormat(sampleRate, bitsPerSample, channels);

            _bufferedProvider = new BufferedWaveProvider(_waveFormat)
            {
                BufferLength = Math.Max(
                    (int)(_waveFormat.AverageBytesPerSecond * bufferLengthMs / 1000.0),
                    _waveFormat.AverageBytesPerSecond * MIN_BUFFER_LENGTH_MS / 1000
                ),
                DiscardOnBufferOverflow = false
            };

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_bufferedProvider);
            _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
        }

        #endregion

        #region 播放控制

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                if (!_isStarted)
                {
                    _waveOut.Play();
                    _isStarted = true;
                }
            }
        }

        /// <summary>
        /// 停止播放并清空缓冲区
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                _waveOut.Stop();
                _bufferedProvider.ClearBuffer();
                _isStarted = false;
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            ThrowIfDisposed();
            _waveOut.Pause();
        }

        /// <summary>
        /// 恢复播放
        /// </summary>
        public void Resume()
        {
            ThrowIfDisposed();
            if (_waveOut.PlaybackState == NAudio.Wave.PlaybackState.Paused)
            {
                _waveOut.Play();
            }
        }

        #endregion

        #region 添加音频数据

        /// <summary>
        /// 添加浮点样本数据到播放缓冲区
        /// </summary>
        /// <param name="samples">归一化的浮点样本数组（-1.0到1.0）</param>
        public void AddSamples(float[] samples)
        {
            ThrowIfDisposed();

            if (samples == null || samples.Length == 0)
                return;

            byte[] buffer = ConvertSamplesToBytes(samples);
            AddBytes(buffer);
        }

        /// <summary>
        /// 添加双精度样本数据到播放缓冲区
        /// </summary>
        /// <param name="samples">归一化的双精度样本数组（-1.0到1.0）</param>
        public void AddSamples(double[] samples)
        {
            ThrowIfDisposed();

            if (samples == null || samples.Length == 0)
                return;

            float[] floatSamples = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                floatSamples[i] = (float)samples[i];
            }

            AddSamples(floatSamples);
        }

        /// <summary>
        /// 添加字节数组到播放缓冲区
        /// </summary>
        /// <param name="buffer">音频数据字节数组</param>
        public void AddBytes(byte[] buffer)
        {
            ThrowIfDisposed();

            if (buffer == null || buffer.Length == 0)
                return;

            lock (_lockObject)
            {
                _bufferedProvider.AddSamples(buffer, 0, buffer.Length);

                // 检查缓冲区是否过小
                if (BufferedBytes < _waveFormat.AverageBytesPerSecond / 10) // 小于100ms
                {
                    OnBufferUnderrun();
                }
            }
        }

        /// <summary>
        /// 清空播放缓冲区
        /// </summary>
        public void ClearBuffer()
        {
            lock (_lockObject)
            {
                _bufferedProvider.ClearBuffer();
            }
        }

        #endregion

        #region 等待方法

        /// <summary>
        /// 等待缓冲区播放完成（同步）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1表示无限等待</param>
        /// <returns>是否成功等待完成</returns>
        public bool WaitForBufferEmpty(int timeoutMs = -1)
        {
            ThrowIfDisposed();

            int elapsed = 0;
            int checkInterval = 50; // 每50ms检查一次

            while (BufferedBytes > 0)
            {
                if (timeoutMs > 0 && elapsed >= timeoutMs)
                    return false;

                Thread.Sleep(checkInterval);
                elapsed += checkInterval;
            }

            return true;
        }

        /// <summary>
        /// 等待缓冲区播放完成（异步）
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功等待完成</returns>
        public async Task<bool> WaitForBufferEmptyAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            while (BufferedBytes > 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50, cancellationToken);
            }

            return BufferedBytes == 0;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将浮点样本转换为字节数组
        /// </summary>
        private byte[] ConvertSamplesToBytes(float[] samples)
        {
            int bytesPerSample = _waveFormat.BitsPerSample / 8;
            int totalBytes = samples.Length * bytesPerSample * _waveFormat.Channels;
            byte[] buffer = new byte[totalBytes];

            if (_waveFormat.BitsPerSample == 16)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Math.Clamp(samples[i], -1.0f, 1.0f);
                    short sample16 = (short)Math.Clamp(sample * 32768f, -32768f, 32767f);
                    BitConverter.GetBytes(sample16).CopyTo(buffer, i * bytesPerSample * _waveFormat.Channels);
                }
            }
            else // 32-bit float
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Math.Clamp(samples[i], -1.0f, 1.0f);
                    BitConverter.GetBytes(sample).CopyTo(buffer, i * bytesPerSample * _waveFormat.Channels);
                }
            }

            return buffer;
        }

        /// <summary>
        /// 触发缓冲区数据不足事件
        /// </summary>
        protected virtual void OnBufferUnderrun()
        {
            BufferUnderrun?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 播放停止事件处理
        /// </summary>
        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            OnPlaybackStopped();
        }

        /// <summary>
        /// 触发播放停止事件
        /// </summary>
        protected virtual void OnPlaybackStopped()
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RealtimeAudioPlayer));
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

            lock (_lockObject)
            {
                Stop();
                _waveOut?.Dispose();
            }

            _isDisposed = true;
        }

        #endregion
    }
}


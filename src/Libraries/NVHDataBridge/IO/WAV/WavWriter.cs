using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace NVHDataBridge.IO.WAV
{
    /// <summary>
    /// WAV文件写入器 - 提供高效的大数据WAV文件写入功能
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var writer = new WavWriter("output.wav", 44100, 2))
    /// {
    ///     writer.WriteSamples(samples);
    /// }
    /// </code>
    /// 
    /// 写入自定义数据块:
    /// <code>
    /// using (var writer = new WavWriter("output.wav", 44100, 2))
    /// {
    ///     writer.WriteSamples(samples);
    ///     // 添加自定义数据块（字节数组）
    ///     byte[] customData = Encoding.UTF8.GetBytes("自定义数据");
    ///     writer.AddCustomChunk("CUST", customData);
    ///     // 或添加文本数据
    ///     writer.AddCustomChunk("INFO", "这是元数据信息");
    /// }
    /// </code>
    /// 
    /// 写入LIST/INFO块（元数据）:
    /// <code>
    /// using (var writer = new WavWriter("output.wav", 44100, 2))
    /// {
    ///     writer.WriteSamples(samples);
    ///     // 使用便捷方法设置元数据
    ///     writer.SetArtist("艺术家名称");
    ///     writer.SetTitle("歌曲标题");
    ///     writer.SetComment("这是注释");
    ///     // 或使用通用方法设置任意INFO子块
    ///     writer.SetInfoChunk("ICOP", "版权信息");
    /// }
    /// </code>
    /// </example>
    public class WavWriter : IDisposable
    {
        #region 私有字段

        private readonly WaveFileWriter _waveWriter;
        private readonly bool _ownsStream;
        private bool _isDisposed;
        private readonly string _filePath;
        private readonly Stream _underlyingStream;
        private readonly List<CustomChunk> _customChunks = new List<CustomChunk>();
        private readonly Dictionary<string, string> _infoChunks = new Dictionary<string, string>();

        // 缓冲区配置
        private const int DEFAULT_BUFFER_SIZE = 64 * 1024; // 64KB 默认缓冲区

        /// <summary>
        /// 自定义数据块结构
        /// </summary>
        private class CustomChunk
        {
            public string ChunkId { get; set; }
            public byte[] Data { get; set; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建WAV文件写入器
        /// </summary>
        /// <param name="filePath">输出文件路径</param>
        /// <param name="sampleRate">采样率（Hz）</param>
        /// <param name="channels">通道数（1=单声道, 2=立体声）</param>
        /// <param name="bitsPerSample">位深度（16或32，默认16）</param>
        public WavWriter(string filePath, int sampleRate, int channels, int bitsPerSample = 16)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            if (sampleRate <= 0)
                throw new ArgumentException("采样率必须大于0", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("通道数必须大于0", nameof(channels));
            if (bitsPerSample != 16 && bitsPerSample != 32)
                throw new ArgumentException("位深度必须为16或32", nameof(bitsPerSample));

            WaveFormat format = bitsPerSample == 32
                ? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)
                : new WaveFormat(sampleRate, bitsPerSample, channels);

            _waveWriter = new WaveFileWriter(filePath, format);
            _filePath = filePath;
            _underlyingStream = null;
            _ownsStream = true;
        }

        /// <summary>
        /// 从流创建WAV文件写入器
        /// </summary>
        /// <param name="stream">输出流</param>
        /// <param name="waveFormat">音频格式</param>
        public WavWriter(Stream stream, WaveFormat waveFormat)
        {
            _waveWriter = stream == null
                ? throw new ArgumentNullException(nameof(stream), "流不能为空")
                : waveFormat == null
                    ? throw new ArgumentNullException(nameof(waveFormat), "音频格式不能为空")
                    : new WaveFileWriter(stream, waveFormat);
            _filePath = null;
            _underlyingStream = stream;
            _ownsStream = false;
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取采样率（Hz）
        /// </summary>
        public int SampleRate => _waveWriter.WaveFormat.SampleRate;

        /// <summary>
        /// 获取通道数
        /// </summary>
        public int Channels => _waveWriter.WaveFormat.Channels;

        /// <summary>
        /// 获取位深度
        /// </summary>
        public int BitsPerSample => _waveWriter.WaveFormat.BitsPerSample;

        /// <summary>
        /// 获取音频格式
        /// </summary>
        public WaveFormat WaveFormat => _waveWriter.WaveFormat;

        /// <summary>
        /// 获取已写入的字节数
        /// </summary>
        public long Length => _waveWriter.Length;

        /// <summary>
        /// 获取已写入的样本数
        /// </summary>
        public long TotalSamples => _waveWriter.Length / _waveWriter.WaveFormat.BlockAlign;

        /// <summary>
        /// 获取当前位置
        /// </summary>
        public long Position => _waveWriter.Position;

        #endregion

        #region 写入方法 - 字节数组

        /// <summary>
        /// 写入字节数组
        /// </summary>
        /// <param name="buffer">要写入的字节数组</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">要写入的字节数</param>
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            _waveWriter.Write(buffer, offset, count);
        }

        /// <summary>
        /// 写入字节数组
        /// </summary>
        /// <param name="buffer">要写入的字节数组</param>
        public void WriteBytes(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            WriteBytes(buffer, 0, buffer.Length);
        }

        #endregion

        #region 写入方法 - 浮点数组（归一化到-1.0到1.0）

        /// <summary>
        /// 写入浮点样本数组（归一化到-1.0到1.0）
        /// </summary>
        /// <param name="samples">归一化的浮点样本数组</param>
        public void WriteSamples(float[] samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            WriteSamples(samples, 0, samples.Length);
        }

        /// <summary>
        /// 写入浮点样本数组的一部分
        /// </summary>
        /// <param name="samples">归一化的浮点样本数组</param>
        /// <param name="offset">起始索引</param>
        /// <param name="count">样本数量</param>
        public void WriteSamples(float[] samples, int offset, int count)
        {
            ThrowIfDisposed();

            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || offset >= samples.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > samples.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            int bytesPerSample = _waveWriter.WaveFormat.BitsPerSample / 8;
            int totalBytes = count * bytesPerSample * _waveWriter.WaveFormat.Channels;
            byte[] buffer = new byte[totalBytes];

            ConvertSamplesToBytes(samples, offset, count, buffer, 0);
            _waveWriter.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 批量写入浮点样本（高效处理大数据）
        /// </summary>
        /// <param name="samples">归一化的浮点样本数组的可枚举集合</param>
        /// <param name="bufferSize">缓冲区大小（样本数），默认16384</param>
        public void WriteSamplesBatch(IEnumerable<float[]> samples, int bufferSize = 16384)
        {
            ThrowIfDisposed();

            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            int bytesPerSample = _waveWriter.WaveFormat.BitsPerSample / 8;
            int bytesPerBuffer = bufferSize * bytesPerSample * _waveWriter.WaveFormat.Channels;
            byte[] byteBuffer = new byte[bytesPerBuffer];

            foreach (var sampleChunk in samples)
            {
                if (sampleChunk == null || sampleChunk.Length == 0)
                    continue;

                int chunkBytes = sampleChunk.Length * bytesPerSample * _waveWriter.WaveFormat.Channels;
                
                if (chunkBytes > byteBuffer.Length)
                {
                    byteBuffer = new byte[chunkBytes];
                }

                ConvertSamplesToBytes(sampleChunk, 0, sampleChunk.Length, byteBuffer, 0);
                _waveWriter.Write(byteBuffer, 0, chunkBytes);
            }
        }

        #endregion

        #region 写入方法 - 双精度数组（归一化到-1.0到1.0）

        /// <summary>
        /// 写入双精度样本数组（归一化到-1.0到1.0）
        /// </summary>
        /// <param name="samples">归一化的双精度样本数组</param>
        public void WriteSamples(double[] samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            WriteSamples(samples, 0, samples.Length);
        }

        /// <summary>
        /// 写入双精度样本数组的一部分
        /// </summary>
        /// <param name="samples">归一化的双精度样本数组</param>
        /// <param name="offset">起始索引</param>
        /// <param name="count">样本数量</param>
        public void WriteSamples(double[] samples, int offset, int count)
        {
            ThrowIfDisposed();

            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || offset >= samples.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > samples.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            int bytesPerSample = _waveWriter.WaveFormat.BitsPerSample / 8;
            int totalBytes = count * bytesPerSample * _waveWriter.WaveFormat.Channels;
            byte[] buffer = new byte[totalBytes];

            ConvertSamplesToBytes(samples, offset, count, buffer, 0);
            _waveWriter.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 批量写入双精度样本（高效处理大数据）
        /// </summary>
        /// <param name="samples">归一化的双精度样本数组的可枚举集合</param>
        /// <param name="bufferSize">缓冲区大小（样本数），默认16384</param>
        public void WriteSamplesBatch(IEnumerable<double[]> samples, int bufferSize = 16384)
        {
            ThrowIfDisposed();

            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            int bytesPerSample = _waveWriter.WaveFormat.BitsPerSample / 8;
            int bytesPerBuffer = bufferSize * bytesPerSample * _waveWriter.WaveFormat.Channels;
            byte[] byteBuffer = new byte[bytesPerBuffer];

            foreach (var sampleChunk in samples)
            {
                if (sampleChunk == null || sampleChunk.Length == 0)
                    continue;

                int chunkBytes = sampleChunk.Length * bytesPerSample * _waveWriter.WaveFormat.Channels;
                
                if (chunkBytes > byteBuffer.Length)
                {
                    byteBuffer = new byte[chunkBytes];
                }

                ConvertSamplesToBytes(sampleChunk, 0, sampleChunk.Length, byteBuffer, 0);
                _waveWriter.Write(byteBuffer, 0, chunkBytes);
            }
        }

        #endregion

        #region 自定义数据块写入方法

        /// <summary>
        /// 添加自定义数据块（将在文件关闭时写入）
        /// </summary>
        /// <param name="chunkId">块ID（4个字符，如"INFO", "CUST"等）</param>
        /// <param name="data">块数据</param>
        /// <exception cref="ArgumentException">chunkId长度必须为4个字符</exception>
        public void AddCustomChunk(string chunkId, byte[] data)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(chunkId) || chunkId.Length != 4)
                throw new ArgumentException("块ID必须为4个字符", nameof(chunkId));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _customChunks.Add(new CustomChunk
            {
                ChunkId = chunkId,
                Data = (byte[])data.Clone()
            });
        }

        /// <summary>
        /// 添加自定义数据块（字符串数据）
        /// </summary>
        /// <param name="chunkId">块ID（4个字符）</param>
        /// <param name="text">文本数据</param>
        public void AddCustomChunk(string chunkId, string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
            AddCustomChunk(chunkId, data);
        }

        /// <summary>
        /// 清除所有待写入的自定义块
        /// </summary>
        public void ClearCustomChunks()
        {
            ThrowIfDisposed();
            _customChunks.Clear();
        }

        /// <summary>
        /// 获取待写入的自定义块数量
        /// </summary>
        public int CustomChunkCount => _customChunks.Count;

        #endregion

        #region LIST/INFO块写入方法

        /// <summary>
        /// 设置INFO子块的值（将在文件关闭时写入LIST块）
        /// </summary>
        /// <param name="infoId">INFO子块ID（4个字符，如"IART", "INAM"等）</param>
        /// <param name="value">子块值</param>
        /// <exception cref="ArgumentException">infoId长度必须为4个字符</exception>
        public void SetInfoChunk(string infoId, string value)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(infoId) || infoId.Length != 4)
                throw new ArgumentException("INFO子块ID必须为4个字符", nameof(infoId));

            if (value == null)
                value = string.Empty;

            _infoChunks[infoId] = value;
        }

        /// <summary>
        /// 设置艺术家信息
        /// </summary>
        /// <param name="artist">艺术家名称</param>
        public void SetArtist(string artist)
        {
            SetInfoChunk("IART", artist ?? string.Empty);
        }

        /// <summary>
        /// 设置标题信息
        /// </summary>
        /// <param name="title">标题</param>
        public void SetTitle(string title)
        {
            SetInfoChunk("INAM", title ?? string.Empty);
        }

        /// <summary>
        /// 设置产品/专辑信息
        /// </summary>
        /// <param name="product">产品/专辑名称</param>
        public void SetProduct(string product)
        {
            SetInfoChunk("IPRD", product ?? string.Empty);
        }

        /// <summary>
        /// 设置创建日期
        /// </summary>
        /// <param name="date">日期字符串</param>
        public void SetCreationDate(string date)
        {
            SetInfoChunk("ICRD", date ?? string.Empty);
        }

        /// <summary>
        /// 设置版权信息
        /// </summary>
        /// <param name="copyright">版权信息</param>
        public void SetCopyright(string copyright)
        {
            SetInfoChunk("ICOP", copyright ?? string.Empty);
        }

        /// <summary>
        /// 设置注释信息
        /// </summary>
        /// <param name="comment">注释</param>
        public void SetComment(string comment)
        {
            SetInfoChunk("ICMT", comment ?? string.Empty);
        }

        /// <summary>
        /// 设置软件信息
        /// </summary>
        /// <param name="software">软件名称</param>
        public void SetSoftware(string software)
        {
            SetInfoChunk("ISFT", software ?? string.Empty);
        }

        /// <summary>
        /// 移除指定的INFO子块
        /// </summary>
        /// <param name="infoId">INFO子块ID</param>
        public void RemoveInfoChunk(string infoId)
        {
            ThrowIfDisposed();
            _infoChunks.Remove(infoId);
        }

        /// <summary>
        /// 清除所有INFO子块
        /// </summary>
        public void ClearInfoChunks()
        {
            ThrowIfDisposed();
            _infoChunks.Clear();
        }

        /// <summary>
        /// 获取所有INFO子块
        /// </summary>
        /// <returns>INFO子块的字典</returns>
        public Dictionary<string, string> GetAllInfoChunks()
        {
            ThrowIfDisposed();
            return new Dictionary<string, string>(_infoChunks);
        }

        /// <summary>
        /// 写入LIST/INFO块到文件
        /// </summary>
        private void WriteListChunk(Stream stream)
        {
            if (_infoChunks.Count == 0)
                return;

            // 写入"LIST"标识符（4字节）
            byte[] listIdBytes = System.Text.Encoding.ASCII.GetBytes("LIST");
            stream.Write(listIdBytes, 0, 4);

            // 先计算LIST块的总大小
            uint listSize = 4; // "INFO"标识符（4字节）
            foreach (var info in _infoChunks)
            {
                // 每个INFO子块：子块ID(4) + 子块大小(4) + 数据 + 可能的填充
                byte[] valueBytes = System.Text.Encoding.UTF8.GetBytes(info.Value);
                uint subChunkSize = (uint)(valueBytes.Length + 1); // +1 for null terminator
                if (subChunkSize % 2 != 0)
                    subChunkSize++; // 如果大小为奇数，需要填充
                listSize += 8 + subChunkSize; // 子块ID(4) + 大小(4) + 数据
            }

            // 写入LIST块大小（4字节，小端序）
            stream.Write(BitConverter.GetBytes(listSize), 0, 4);

            // 写入"INFO"标识符（4字节）
            byte[] infoIdBytes = System.Text.Encoding.ASCII.GetBytes("INFO");
            stream.Write(infoIdBytes, 0, 4);

            // 写入每个INFO子块
            foreach (var info in _infoChunks)
            {
                // 写入子块ID（4字节）
                byte[] subChunkIdBytes = System.Text.Encoding.ASCII.GetBytes(info.Key);
                stream.Write(subChunkIdBytes, 0, 4);

                // 准备子块数据（UTF-8编码，以null结尾）
                byte[] valueBytes = System.Text.Encoding.UTF8.GetBytes(info.Value);
                uint subChunkSize = (uint)(valueBytes.Length + 1); // +1 for null terminator
                if (subChunkSize % 2 != 0)
                    subChunkSize++; // 如果大小为奇数，需要填充

                // 写入子块大小（4字节，小端序）
                stream.Write(BitConverter.GetBytes(subChunkSize), 0, 4);

                // 写入子块数据
                stream.Write(valueBytes, 0, valueBytes.Length);
                stream.WriteByte(0); // null terminator

                // RIFF规范：如果子块大小为奇数，需要填充一个字节
                if ((valueBytes.Length + 1) % 2 != 0)
                {
                    stream.WriteByte(0);
                }
            }
        }

        /// <summary>
        /// 写入自定义数据块到文件
        /// </summary>
        private void WriteCustomChunks()
        {
            if (_customChunks.Count == 0 && _infoChunks.Count == 0)
                return;

            Stream stream = null;
            try
            {
                // 获取底层流
                if (_filePath != null && File.Exists(_filePath))
                {
                    // 从文件路径打开流
                    stream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite);
                }
                else if (_underlyingStream != null && _underlyingStream.CanSeek && _underlyingStream.CanWrite)
                {
                    stream = _underlyingStream;
                }

                if (stream == null)
                {
                    // 无法写入自定义块（流不支持或已关闭）
                    return;
                }

                // 移动到文件末尾
                stream.Seek(0, SeekOrigin.End);

                // 先写入LIST块（如果存在INFO子块）
                if (_infoChunks.Count > 0)
                {
                    WriteListChunk(stream);
                }

                // 然后写入其他自定义块
                foreach (var chunk in _customChunks)
                {
                    // 写入块ID（4字节）
                    byte[] chunkIdBytes = System.Text.Encoding.ASCII.GetBytes(chunk.ChunkId);
                    stream.Write(chunkIdBytes, 0, 4);

                    // 写入块大小（4字节，小端序）
                    uint chunkSize = (uint)chunk.Data.Length;
                    stream.Write(BitConverter.GetBytes(chunkSize), 0, 4);

                    // 写入块数据
                    stream.Write(chunk.Data, 0, chunk.Data.Length);

                    // RIFF规范：如果块大小为奇数，需要填充一个字节
                    if (chunkSize % 2 != 0)
                    {
                        stream.WriteByte(0);
                    }
                }

                // 更新RIFF文件头中的文件大小字段（位置4-7）
                // RIFF块大小 = 文件总大小 - 8（不包括RIFF标识和大小字段本身）
                long newFileSize = stream.Length;
                uint newRiffSize = (uint)(newFileSize - 8);
                stream.Seek(4, SeekOrigin.Begin);
                stream.Write(BitConverter.GetBytes(newRiffSize), 0, 4);

                stream.Flush();
            }
            finally
            {
                // 如果是从文件路径打开的流，需要关闭它
                if (stream != null && stream != _underlyingStream)
                {
                    stream.Dispose();
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将浮点样本数组转换为字节数组
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConvertSamplesToBytes(float[] samples, int offset, int count, byte[] bytes, int byteOffset)
        {
            int bytesPerSample = _waveWriter.WaveFormat.BitsPerSample / 8;
            int channels = _waveWriter.WaveFormat.Channels;

            if (_waveWriter.WaveFormat.BitsPerSample == 16)
            {
                // 16位PCM - 使用32768进行归一化，限制最大值到32767以避免溢出
                for (int i = 0; i < count; i++)
                {
                    float sample = Math.Max(-1.0f, Math.Min(1.0f, samples[offset + i]));
                    float scaled = sample * 32768f;
                    short sample16 = (short)Math.Clamp(scaled, -32768f, 32767f);
                    int byteIndex = byteOffset + (i * bytesPerSample * channels);
                    BitConverter.GetBytes(sample16).CopyTo(bytes, byteIndex);
                }
            }
            else if (_waveWriter.WaveFormat.BitsPerSample == 32 && _waveWriter.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                // 32位浮点
                for (int i = 0; i < count; i++)
                {
                    float sample = Math.Max(-1.0f, Math.Min(1.0f, samples[offset + i]));
                    int byteIndex = byteOffset + (i * bytesPerSample * channels);
                    BitConverter.GetBytes(sample).CopyTo(bytes, byteIndex);
                }
            }
            else
            {
                throw new NotSupportedException($"不支持的位深度: {_waveWriter.WaveFormat.BitsPerSample} 位");
            }
        }

        /// <summary>
        /// 将双精度样本数组转换为字节数组
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConvertSamplesToBytes(double[] samples, int offset, int count, byte[] bytes, int byteOffset)
        {
            int bytesPerSample = _waveWriter.WaveFormat.BitsPerSample / 8;
            int channels = _waveWriter.WaveFormat.Channels;

            if (_waveWriter.WaveFormat.BitsPerSample == 16)
            {
                // 16位PCM - 使用32768进行归一化，限制最大值到32767以避免溢出
                for (int i = 0; i < count; i++)
                {
                    double sample = Math.Max(-1.0, Math.Min(1.0, samples[offset + i]));
                    double scaled = sample * 32768.0;
                    short sample16 = (short)Math.Clamp(scaled, -32768.0, 32767.0);
                    int byteIndex = byteOffset + (i * bytesPerSample * channels);
                    BitConverter.GetBytes(sample16).CopyTo(bytes, byteIndex);
                }
            }
            else if (_waveWriter.WaveFormat.BitsPerSample == 32 && _waveWriter.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                // 32位浮点
                for (int i = 0; i < count; i++)
                {
                    float sample = (float)Math.Max(-1.0, Math.Min(1.0, samples[offset + i]));
                    int byteIndex = byteOffset + (i * bytesPerSample * channels);
                    BitConverter.GetBytes(sample).CopyTo(bytes, byteIndex);
                }
            }
            else
            {
                throw new NotSupportedException($"不支持的位深度: {_waveWriter.WaveFormat.BitsPerSample} 位");
            }
        }

        /// <summary>
        /// 刷新写入缓冲区
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _waveWriter.Flush();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WavWriter));
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

            _waveWriter?.Flush();
            _waveWriter?.Dispose();

            // 在关闭WaveFileWriter后写入自定义块
            WriteCustomChunks();

            _isDisposed = true;
        }

        #endregion
    }
}


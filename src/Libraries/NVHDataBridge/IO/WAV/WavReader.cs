using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NVHDataBridge.IO.WAV
{
    /// <summary>
    /// WAV文件读取器 - 提供高效的大数据WAV文件读取功能
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var reader = WavReader.Open("audio.wav"))
    /// {
    ///     var data = reader.ReadAllSamples();
    ///     Console.WriteLine($"采样率: {reader.SampleRate} Hz, 通道数: {reader.Channels}");
    /// }
    /// </code>
    /// 
    /// 读取自定义数据块:
    /// <code>
    /// using (var reader = WavReader.Open("audio.wav"))
    /// {
    ///     // 检查是否存在自定义块
    ///     if (reader.HasCustomChunk("CUST"))
    ///     {
    ///         // 读取自定义块（字节数组）
    ///         byte[] customData = reader.ReadCustomChunk("CUST");
    ///         // 或读取为字符串
    ///         string text = reader.ReadCustomChunkAsString("INFO");
    ///     }
    ///     
    ///     // 获取所有自定义块
    ///     var allChunks = reader.ReadAllCustomChunks();
    ///     foreach (var chunk in allChunks)
    ///     {
    ///         Console.WriteLine($"块ID: {chunk.Key}, 大小: {chunk.Value.Length} 字节");
    ///     }
    /// }
    /// </code>
    /// 
    /// 读取LIST/INFO块（元数据）:
    /// <code>
    /// using (var reader = WavReader.Open("audio.wav"))
    /// {
    ///     // 使用便捷方法读取元数据
    ///     string artist = reader.GetArtist();
    ///     string title = reader.GetTitle();
    ///     string comment = reader.GetComment();
    ///     // 或使用通用方法读取任意INFO子块
    ///     string copyright = reader.ReadInfoChunk("ICOP");
    ///     
    ///     // 获取所有INFO子块
    ///     var allInfo = reader.ReadAllInfoChunks();
    ///     foreach (var info in allInfo)
    ///     {
    ///         Console.WriteLine($"{info.Key}: {info.Value}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public class WavReader : IDisposable
    {
        #region 私有字段

        private readonly WaveFileReader _waveReader;
        private readonly bool _ownsStream;
        private bool _isDisposed;
        private readonly string _filePath;
        private Dictionary<string, byte[]> _customChunks;
        private Dictionary<string, string> _infoChunks;
        private bool _chunksLoaded;
        
        // 缓冲区配置
        private const int DEFAULT_BUFFER_SIZE = 64 * 1024; // 64KB 默认缓冲区

        #endregion

        #region 构造函数

        /// <summary>
        /// 从文件路径创建WAV读取器
        /// </summary>
        /// <param name="filePath">WAV文件路径</param>
        private WavReader(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"WAV文件不存在: {filePath}", filePath);

            _waveReader = new WaveFileReader(filePath);
            _filePath = filePath;
            _ownsStream = true;
            _customChunks = new Dictionary<string, byte[]>();
            _infoChunks = new Dictionary<string, string>();
            _chunksLoaded = false;
        }

        /// <summary>
        /// 从流创建WAV读取器
        /// </summary>
        /// <param name="stream">包含WAV数据的流</param>
        private WavReader(Stream stream)
        {
            _waveReader = stream == null 
                ? throw new ArgumentNullException(nameof(stream), "流不能为空")
                : new WaveFileReader(stream);
            _filePath = null;
            _ownsStream = false;
            _customChunks = new Dictionary<string, byte[]>();
            _infoChunks = new Dictionary<string, string>();
            _chunksLoaded = false;
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取采样率（Hz）
        /// </summary>
        public int SampleRate => _waveReader.WaveFormat.SampleRate;

        /// <summary>
        /// 获取通道数（1=单声道, 2=立体声）
        /// </summary>
        public int Channels => _waveReader.WaveFormat.Channels;

        /// <summary>
        /// 获取位深度（通常为16或32）
        /// </summary>
        public int BitsPerSample => _waveReader.WaveFormat.BitsPerSample;

        /// <summary>
        /// 获取音频格式
        /// </summary>
        public WaveFormat WaveFormat => _waveReader.WaveFormat;

        /// <summary>
        /// 获取音频时长（秒）
        /// </summary>
        public double Duration => _waveReader.TotalTime.TotalSeconds;

        /// <summary>
        /// 获取总样本数
        /// </summary>
        public long TotalSamples => _waveReader.Length / _waveReader.WaveFormat.BlockAlign;

        /// <summary>
        /// 获取当前位置（字节）
        /// </summary>
        public long Position => _waveReader.Position;

        /// <summary>
        /// 获取文件长度（字节）
        /// </summary>
        public long Length => _waveReader.Length;

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 打开WAV文件并返回读取器实例
        /// </summary>
        /// <param name="filePath">WAV文件路径</param>
        /// <returns>已打开的WavReader实例</returns>
        public static WavReader Open(string filePath)
        {
            return new WavReader(filePath);
        }

        /// <summary>
        /// 从流打开WAV文件并返回读取器实例
        /// </summary>
        /// <param name="stream">包含WAV数据的流</param>
        /// <returns>已打开的WavReader实例</returns>
        public static WavReader Open(Stream stream)
        {
            return new WavReader(stream);
        }

        #endregion

        #region 读取方法 - 字节数组

        /// <summary>
        /// 读取所有音频数据（字节数组）
        /// 注意：对于大文件，建议使用流式读取方法
        /// </summary>
        /// <returns>音频数据字节数组</returns>
        public byte[] ReadAllBytes()
        {
            ThrowIfDisposed();

            long length = _waveReader.Length;
            byte[] buffer = new byte[length];
            _waveReader.Position = 0;
            _waveReader.Read(buffer, 0, (int)length);
            return buffer;
        }

        /// <summary>
        /// 读取指定数量的字节
        /// </summary>
        /// <param name="count">要读取的字节数</param>
        /// <returns>读取的字节数组</returns>
        public byte[] ReadBytes(int count)
        {
            ThrowIfDisposed();

            if (count <= 0)
                throw new ArgumentException("读取数量必须大于0", nameof(count));

            byte[] buffer = new byte[count];
            int bytesRead = _waveReader.Read(buffer, 0, count);
            
            if (bytesRead < count)
            {
                Array.Resize(ref buffer, bytesRead);
            }

            return buffer;
        }

        /// <summary>
        /// 流式读取字节数据（高效处理大文件）
        /// </summary>
        /// <param name="bufferSize">缓冲区大小（字节），默认64KB</param>
        /// <returns>字节数据的可枚举集合</returns>
        public IEnumerable<byte[]> ReadBytesStreaming(int bufferSize = DEFAULT_BUFFER_SIZE)
        {
            ThrowIfDisposed();

            if (bufferSize <= 0)
                bufferSize = DEFAULT_BUFFER_SIZE;

            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = _waveReader.Read(buffer, 0, bufferSize)) > 0)
            {
                if (bytesRead < bufferSize)
                {
                    byte[] partialBuffer = new byte[bytesRead];
                    Array.Copy(buffer, partialBuffer, bytesRead);
                    yield return partialBuffer;
                }
                else
                {
                    yield return buffer;
                    buffer = new byte[bufferSize]; // 分配新缓冲区避免引用问题
                }
            }
        }

        #endregion

        #region 读取方法 - 浮点数组（归一化到-1.0到1.0）

        /// <summary>
        /// 读取所有样本并转换为浮点数组（归一化到-1.0到1.0）
        /// 注意：对于大文件，建议使用流式读取方法
        /// </summary>
        /// <returns>归一化的浮点样本数组</returns>
        public float[] ReadAllSamples()
        {
            ThrowIfDisposed();

            long totalSamples = TotalSamples;
            float[] samples = new float[totalSamples];
            int sampleIndex = 0;

            byte[] buffer = new byte[DEFAULT_BUFFER_SIZE];
            int bytesRead;

            _waveReader.Position = 0;
            while ((bytesRead = _waveReader.Read(buffer, 0, buffer.Length)) > 0)
            {
                int samplesInBuffer = bytesRead / _waveReader.WaveFormat.BlockAlign;
                ConvertBytesToSamples(buffer, bytesRead, samples, sampleIndex, samplesInBuffer);
                sampleIndex += samplesInBuffer;
            }

            return samples;
        }

        /// <summary>
        /// 读取指定数量的样本并转换为浮点数组（归一化）
        /// </summary>
        /// <param name="sampleCount">要读取的样本数</param>
        /// <returns>归一化的浮点样本数组</returns>
        public float[] ReadSamples(int sampleCount)
        {
            ThrowIfDisposed();

            if (sampleCount <= 0)
                throw new ArgumentException("样本数必须大于0", nameof(sampleCount));

            int bytesToRead = sampleCount * _waveReader.WaveFormat.BlockAlign;
            byte[] buffer = new byte[bytesToRead];
            int bytesRead = _waveReader.Read(buffer, 0, bytesToRead);
            int actualSampleCount = bytesRead / _waveReader.WaveFormat.BlockAlign;

            float[] samples = new float[actualSampleCount];
            ConvertBytesToSamples(buffer, bytesRead, samples, 0, actualSampleCount);

            return samples;
        }

        /// <summary>
        /// 流式读取样本并转换为浮点数组（高效处理大文件）
        /// </summary>
        /// <param name="bufferSize">缓冲区大小（样本数），默认16384</param>
        /// <returns>归一化浮点样本数组的可枚举集合</returns>
        public IEnumerable<float[]> ReadSamplesStreaming(int bufferSize = 16384)
        {
            ThrowIfDisposed();

            if (bufferSize <= 0)
                bufferSize = 16384;

            int bytesPerBuffer = bufferSize * _waveReader.WaveFormat.BlockAlign;
            byte[] byteBuffer = new byte[bytesPerBuffer];
            float[] sampleBuffer = new float[bufferSize];
            int bytesRead;

            while ((bytesRead = _waveReader.Read(byteBuffer, 0, bytesPerBuffer)) > 0)
            {
                int samplesInBuffer = bytesRead / _waveReader.WaveFormat.BlockAlign;
                
                if (samplesInBuffer < bufferSize)
                {
                    sampleBuffer = new float[samplesInBuffer];
                }

                ConvertBytesToSamples(byteBuffer, bytesRead, sampleBuffer, 0, samplesInBuffer);
                
                yield return sampleBuffer;

                if (samplesInBuffer < bufferSize)
                {
                    sampleBuffer = new float[bufferSize]; // 重新分配以保持大小
                }
            }
        }

        #endregion

        #region 读取方法 - 双精度数组（归一化到-1.0到1.0）

        /// <summary>
        /// 读取所有样本并转换为双精度数组（归一化到-1.0到1.0）
        /// 注意：对于大文件，建议使用流式读取方法
        /// </summary>
        /// <returns>归一化的双精度样本数组</returns>
        public double[] ReadAllSamplesDouble()
        {
            ThrowIfDisposed();

            long totalSamples = TotalSamples;
            double[] samples = new double[totalSamples];
            int sampleIndex = 0;

            byte[] buffer = new byte[DEFAULT_BUFFER_SIZE];
            int bytesRead;

            _waveReader.Position = 0;
            while ((bytesRead = _waveReader.Read(buffer, 0, buffer.Length)) > 0)
            {
                int samplesInBuffer = bytesRead / _waveReader.WaveFormat.BlockAlign;
                ConvertBytesToSamplesDouble(buffer, bytesRead, samples, sampleIndex, samplesInBuffer);
                sampleIndex += samplesInBuffer;
            }

            return samples;
        }

        /// <summary>
        /// 流式读取样本并转换为双精度数组（高效处理大文件）
        /// </summary>
        /// <param name="bufferSize">缓冲区大小（样本数），默认16384</param>
        /// <returns>归一化双精度样本数组的可枚举集合</returns>
        public IEnumerable<double[]> ReadSamplesDoubleStreaming(int bufferSize = 16384)
        {
            ThrowIfDisposed();

            if (bufferSize <= 0)
                bufferSize = 16384;

            int bytesPerBuffer = bufferSize * _waveReader.WaveFormat.BlockAlign;
            byte[] byteBuffer = new byte[bytesPerBuffer];
            double[] sampleBuffer = new double[bufferSize];
            int bytesRead;

            while ((bytesRead = _waveReader.Read(byteBuffer, 0, bytesPerBuffer)) > 0)
            {
                int samplesInBuffer = bytesRead / _waveReader.WaveFormat.BlockAlign;
                
                if (samplesInBuffer < bufferSize)
                {
                    sampleBuffer = new double[samplesInBuffer];
                }

                ConvertBytesToSamplesDouble(byteBuffer, bytesRead, sampleBuffer, 0, samplesInBuffer);
                
                yield return sampleBuffer;

                if (samplesInBuffer < bufferSize)
                {
                    sampleBuffer = new double[bufferSize];
                }
            }
        }

        #endregion

        #region 自定义数据块读取方法

        /// <summary>
        /// 读取所有自定义数据块
        /// </summary>
        /// <returns>自定义块的字典，键为块ID，值为块数据</returns>
        public Dictionary<string, byte[]> ReadAllCustomChunks()
        {
            ThrowIfDisposed();
            LoadCustomChunks();
            return new Dictionary<string, byte[]>(_customChunks);
        }

        /// <summary>
        /// 读取指定ID的自定义数据块
        /// </summary>
        /// <param name="chunkId">块ID（4个字符）</param>
        /// <returns>块数据，如果不存在则返回null</returns>
        public byte[] ReadCustomChunk(string chunkId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(chunkId) || chunkId.Length != 4)
                throw new ArgumentException("块ID必须为4个字符", nameof(chunkId));

            LoadCustomChunks();
            return _customChunks.TryGetValue(chunkId, out byte[] data) ? (byte[])data.Clone() : null;
        }

        /// <summary>
        /// 读取指定ID的自定义数据块（字符串形式）
        /// </summary>
        /// <param name="chunkId">块ID（4个字符）</param>
        /// <returns>块数据文本，如果不存在则返回null</returns>
        public string ReadCustomChunkAsString(string chunkId)
        {
            byte[] data = ReadCustomChunk(chunkId);
            if (data == null)
                return null;

            // 移除末尾的空字符（如果有）
            int length = data.Length;
            while (length > 0 && data[length - 1] == 0)
                length--;

            return System.Text.Encoding.UTF8.GetString(data, 0, length);
        }

        /// <summary>
        /// 检查是否存在指定ID的自定义块
        /// </summary>
        /// <param name="chunkId">块ID（4个字符）</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        public bool HasCustomChunk(string chunkId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(chunkId) || chunkId.Length != 4)
                return false;

            LoadCustomChunks();
            return _customChunks.ContainsKey(chunkId);
        }

        /// <summary>
        /// 获取所有自定义块的ID列表
        /// </summary>
        /// <returns>块ID的集合</returns>
        public IEnumerable<string> GetCustomChunkIds()
        {
            ThrowIfDisposed();
            LoadCustomChunks();
            return _customChunks.Keys.ToList();
        }

        #endregion

        #region LIST/INFO块读取方法

        /// <summary>
        /// 读取所有INFO子块
        /// </summary>
        /// <returns>INFO子块的字典，键为子块ID，值为子块内容</returns>
        public Dictionary<string, string> ReadAllInfoChunks()
        {
            ThrowIfDisposed();
            LoadCustomChunks();
            return new Dictionary<string, string>(_infoChunks);
        }

        /// <summary>
        /// 读取指定的INFO子块
        /// </summary>
        /// <param name="infoId">INFO子块ID（4个字符，如"IART", "INAM"等）</param>
        /// <returns>子块内容，如果不存在则返回null</returns>
        public string ReadInfoChunk(string infoId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(infoId) || infoId.Length != 4)
                throw new ArgumentException("INFO子块ID必须为4个字符", nameof(infoId));

            LoadCustomChunks();
            return _infoChunks.TryGetValue(infoId, out string value) ? value : null;
        }

        /// <summary>
        /// 获取艺术家信息
        /// </summary>
        /// <returns>艺术家名称，如果不存在则返回null</returns>
        public string GetArtist()
        {
            return ReadInfoChunk("IART");
        }

        /// <summary>
        /// 获取标题信息
        /// </summary>
        /// <returns>标题，如果不存在则返回null</returns>
        public string GetTitle()
        {
            return ReadInfoChunk("INAM");
        }

        /// <summary>
        /// 获取产品/专辑信息
        /// </summary>
        /// <returns>产品/专辑名称，如果不存在则返回null</returns>
        public string GetProduct()
        {
            return ReadInfoChunk("IPRD");
        }

        /// <summary>
        /// 获取创建日期
        /// </summary>
        /// <returns>创建日期，如果不存在则返回null</returns>
        public string GetCreationDate()
        {
            return ReadInfoChunk("ICRD");
        }

        /// <summary>
        /// 获取版权信息
        /// </summary>
        /// <returns>版权信息，如果不存在则返回null</returns>
        public string GetCopyright()
        {
            return ReadInfoChunk("ICOP");
        }

        /// <summary>
        /// 获取注释信息
        /// </summary>
        /// <returns>注释，如果不存在则返回null</returns>
        public string GetComment()
        {
            return ReadInfoChunk("ICMT");
        }

        /// <summary>
        /// 获取软件信息
        /// </summary>
        /// <returns>软件名称，如果不存在则返回null</returns>
        public string GetSoftware()
        {
            return ReadInfoChunk("ISFT");
        }

        /// <summary>
        /// 检查是否存在指定的INFO子块
        /// </summary>
        /// <param name="infoId">INFO子块ID</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        public bool HasInfoChunk(string infoId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(infoId) || infoId.Length != 4)
                return false;

            LoadCustomChunks();
            return _infoChunks.ContainsKey(infoId);
        }

        /// <summary>
        /// 获取所有INFO子块的ID列表
        /// </summary>
        /// <returns>INFO子块ID的集合</returns>
        public IEnumerable<string> GetInfoChunkIds()
        {
            ThrowIfDisposed();
            LoadCustomChunks();
            return _infoChunks.Keys.ToList();
        }

        /// <summary>
        /// 加载自定义数据块（延迟加载）
        /// </summary>
        private void LoadCustomChunks()
        {
            if (_chunksLoaded)
                return;

            Stream stream = null;
            try
            {
                // 获取底层流
                if (_filePath != null && File.Exists(_filePath))
                {
                    stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
                }
                else
                {
                    // 对于从流创建的读取器，无法直接访问原始流来读取自定义块
                    // 这种情况下，自定义块可能无法读取
                    _chunksLoaded = true;
                    return;
                }

                // 读取RIFF文件头
                byte[] header = new byte[12];
                if (stream.Read(header, 0, 12) != 12)
                {
                    _chunksLoaded = true;
                    return;
                }

                // 验证RIFF标识
                string riffId = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                if (riffId != "RIFF")
                {
                    _chunksLoaded = true;
                    return;
                }

                // 读取文件大小
                uint fileSize = BitConverter.ToUInt32(header, 4);
                string waveId = System.Text.Encoding.ASCII.GetString(header, 8, 4);
                if (waveId != "WAVE")
                {
                    _chunksLoaded = true;
                    return;
                }

                // 跳过fmt块和data块，查找自定义块
                long position = 12;
                bool foundData = false;

                while (position < stream.Length)
                {
                    stream.Seek(position, SeekOrigin.Begin);

                    // 读取块ID
                    byte[] chunkIdBytes = new byte[4];
                    if (stream.Read(chunkIdBytes, 0, 4) != 4)
                        break;

                    string chunkId = System.Text.Encoding.ASCII.GetString(chunkIdBytes);

                    // 读取块大小
                    byte[] sizeBytes = new byte[4];
                    if (stream.Read(sizeBytes, 0, 4) != 4)
                        break;

                    uint chunkSize = BitConverter.ToUInt32(sizeBytes, 0);
                    position += 8;

                    // 如果是data块，标记已找到，但继续查找后面的自定义块
                    if (chunkId == "data")
                    {
                        foundData = true;
                        // 跳过data块
                        position += chunkSize;
                        // RIFF规范：如果块大小为奇数，需要跳过填充字节
                        if (chunkSize % 2 != 0)
                            position++;
                        continue;
                    }

                    // 如果是fmt块或fact块，跳过
                    if (chunkId == "fmt " || chunkId == "fact")
                    {
                        position += chunkSize;
                        if (chunkSize % 2 != 0)
                            position++;
                        continue;
                    }

                    // 如果是LIST块，解析它
                    if (chunkId == "LIST")
                    {
                        ParseListChunk(stream, position, chunkSize);
                        position += chunkSize;
                        if (chunkSize % 2 != 0)
                            position++;
                        continue;
                    }

                    // 只有在找到data块之后才读取自定义块
                    if (foundData)
                    {
                        // 读取自定义块数据
                        byte[] chunkData = new byte[chunkSize];
                        int bytesRead = stream.Read(chunkData, 0, (int)chunkSize);
                        if (bytesRead == chunkSize)
                        {
                            _customChunks[chunkId] = chunkData;
                        }

                        position += chunkSize;
                        if (chunkSize % 2 != 0)
                            position++;
                    }
                    else
                    {
                        // 跳过未知块（在data块之前）
                        position += chunkSize;
                        if (chunkSize % 2 != 0)
                            position++;
                    }
                }
            }
            catch
            {
                // 读取失败时忽略，保持字典为空
            }
            finally
            {
                // 如果是从文件路径打开的流，需要关闭它
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            _chunksLoaded = true;
        }

        /// <summary>
        /// 解析LIST块并提取INFO子块
        /// </summary>
        /// <param name="stream">文件流</param>
        /// <param name="listDataStartPosition">LIST块数据开始位置（已跳过"LIST"和大小字段）</param>
        /// <param name="listSize">LIST块大小</param>
        private void ParseListChunk(Stream stream, long listDataStartPosition, uint listSize)
        {
            try
            {
                stream.Seek(listDataStartPosition, SeekOrigin.Begin);

                // 读取列表类型（应该是"INFO"）
                byte[] listTypeBytes = new byte[4];
                if (stream.Read(listTypeBytes, 0, 4) != 4)
                    return;

                string listType = System.Text.Encoding.ASCII.GetString(listTypeBytes);
                if (listType != "INFO")
                {
                    // 如果不是INFO类型，跳过
                    return;
                }

                long currentPos = listDataStartPosition + 4; // 跳过"INFO"标识符
                long listEndPosition = listDataStartPosition + listSize;

                // 解析INFO子块
                while (currentPos < listEndPosition)
                {
                    stream.Seek(currentPos, SeekOrigin.Begin);

                    // 读取子块ID
                    byte[] subChunkIdBytes = new byte[4];
                    if (stream.Read(subChunkIdBytes, 0, 4) != 4)
                        break;

                    string subChunkId = System.Text.Encoding.ASCII.GetString(subChunkIdBytes);
                    currentPos += 4;

                    // 读取子块大小
                    byte[] subSizeBytes = new byte[4];
                    if (stream.Read(subSizeBytes, 0, 4) != 4)
                        break;

                    uint subChunkSize = BitConverter.ToUInt32(subSizeBytes, 0);
                    currentPos += 4;

                    // 读取子块数据
                    byte[] subChunkData = new byte[subChunkSize];
                    int bytesRead = stream.Read(subChunkData, 0, (int)subChunkSize);
                    if (bytesRead != subChunkSize)
                        break;

                    // 提取字符串（移除null终止符）
                    int length = subChunkData.Length;
                    while (length > 0 && subChunkData[length - 1] == 0)
                        length--;

                    if (length > 0)
                    {
                        string value = System.Text.Encoding.UTF8.GetString(subChunkData, 0, length);
                        _infoChunks[subChunkId] = value;
                    }

                    currentPos += subChunkSize;
                    // RIFF规范：如果子块大小为奇数，需要跳过填充字节
                    if (subChunkSize % 2 != 0)
                        currentPos++;
                }
            }
            catch
            {
                // 解析失败时忽略
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将字节数组转换为浮点样本数组（归一化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConvertBytesToSamples(byte[] bytes, int byteCount, float[] samples, int startIndex, int sampleCount)
        {
            int bytesPerSample = _waveReader.WaveFormat.BitsPerSample / 8;
            int channels = _waveReader.WaveFormat.Channels;

            if (_waveReader.WaveFormat.BitsPerSample == 16)
            {
                // 16位PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * bytesPerSample * channels;
                    if (byteIndex + 1 < byteCount)
                    {
                        short sample16 = BitConverter.ToInt16(bytes, byteIndex);
                        samples[startIndex + i] = sample16 / 32768f;
                    }
                }
            }
            else if (_waveReader.WaveFormat.BitsPerSample == 32 && _waveReader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                // 32位浮点
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * bytesPerSample * channels;
                    if (byteIndex + 3 < byteCount)
                    {
                        samples[startIndex + i] = BitConverter.ToSingle(bytes, byteIndex);
                    }
                }
            }
            else if (_waveReader.WaveFormat.BitsPerSample == 32)
            {
                // 32位PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * bytesPerSample * channels;
                    if (byteIndex + 3 < byteCount)
                    {
                        int sample32 = BitConverter.ToInt32(bytes, byteIndex);
                        samples[startIndex + i] = sample32 / 2147483648f;
                    }
                }
            }
            else
            {
                throw new NotSupportedException($"不支持的位深度: {_waveReader.WaveFormat.BitsPerSample} 位");
            }
        }

        /// <summary>
        /// 将字节数组转换为双精度样本数组（归一化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConvertBytesToSamplesDouble(byte[] bytes, int byteCount, double[] samples, int startIndex, int sampleCount)
        {
            int bytesPerSample = _waveReader.WaveFormat.BitsPerSample / 8;
            int channels = _waveReader.WaveFormat.Channels;

            if (_waveReader.WaveFormat.BitsPerSample == 16)
            {
                // 16位PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * bytesPerSample * channels;
                    if (byteIndex + 1 < byteCount)
                    {
                        short sample16 = BitConverter.ToInt16(bytes, byteIndex);
                        samples[startIndex + i] = sample16 / 32768.0;
                    }
                }
            }
            else if (_waveReader.WaveFormat.BitsPerSample == 32 && _waveReader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                // 32位浮点
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * bytesPerSample * channels;
                    if (byteIndex + 3 < byteCount)
                    {
                        samples[startIndex + i] = BitConverter.ToSingle(bytes, byteIndex);
                    }
                }
            }
            else if (_waveReader.WaveFormat.BitsPerSample == 32)
            {
                // 32位PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * bytesPerSample * channels;
                    if (byteIndex + 3 < byteCount)
                    {
                        int sample32 = BitConverter.ToInt32(bytes, byteIndex);
                        samples[startIndex + i] = sample32 / 2147483648.0;
                    }
                }
            }
            else
            {
                throw new NotSupportedException($"不支持的位深度: {_waveReader.WaveFormat.BitsPerSample} 位");
            }
        }

        /// <summary>
        /// 重置读取位置到文件开头
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();
            _waveReader.Position = 0;
        }

        /// <summary>
        /// 设置读取位置
        /// </summary>
        /// <param name="position">位置（字节）</param>
        public void Seek(long position)
        {
            ThrowIfDisposed();
            if (position < 0 || position > _waveReader.Length)
                throw new ArgumentOutOfRangeException(nameof(position));

            _waveReader.Position = position;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WavReader));
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

            _waveReader?.Dispose();
            _isDisposed = true;
        }

        #endregion
    }
}


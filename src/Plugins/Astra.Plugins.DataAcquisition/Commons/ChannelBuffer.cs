using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Commons
{

    /// <summary>
    /// 单通道数据缓冲区（性能优化版 - 使用读写锁）
    /// </summary>
    public class ChannelBuffer : IDisposable
    {
        private readonly List<DataChunk> _chunks;
        private readonly int _chunkSize;
        private readonly ReaderWriterLockSlim _lock;
        private DataChunk _currentChunk;
        private bool _disposed = false;

        public int ChannelId { get; }
        public string ChannelName { get; set; }

        // 使用 volatile 确保多线程可见性
        private long _totalSamples;
        public long TotalSamples => Volatile.Read(ref _totalSamples);

        public int ChunkCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _chunks.Count + (_currentChunk != null ? 1 : 0);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public long MemoryUsageBytes
        {
            get
            {
                int count = ChunkCount;
                return (long)count * _chunkSize * sizeof(double);
            }
        }

        public ChannelBuffer(int channelId, int chunkSize = 100_000, string channelName = "")
        {
            ChannelId = channelId;
            _chunkSize = chunkSize;
            _chunks = new List<DataChunk>();
            _currentChunk = new DataChunk(chunkSize);
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            ChannelName = string.IsNullOrEmpty(channelName) ? $"Channel_{channelId}" : channelName;
        }

        /// <summary>
        /// 写入单个数据点
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(double value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_currentChunk.TryAdd(value))
                {
                    // 当前块已满，归档并创建新块
                    _chunks.Add(_currentChunk);
                    _currentChunk = new DataChunk(_chunkSize);
                    _currentChunk.TryAdd(value);
                }
                Interlocked.Increment(ref _totalSamples);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 批量写入数据（高性能）
        /// </summary>
        public void WriteBatch(ReadOnlySpan<double> data)
        {
            _lock.EnterWriteLock();
            try
            {
                int remaining = data.Length;
                int offset = 0;

                while (remaining > 0)
                {
                    int written = _currentChunk.AddRange(data.Slice(offset, remaining));
                    offset += written;
                    remaining -= written;
                    Interlocked.Add(ref _totalSamples, written);

                    if (_currentChunk.IsFull && remaining > 0)
                    {
                        _chunks.Add(_currentChunk);
                        _currentChunk = new DataChunk(_chunkSize);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 读取指定范围的数据（使用读锁，不阻塞其他读操作）
        /// </summary>
        public double[] ReadRange(long startIndex, int count)
        {
            _lock.EnterReadLock();
            try
            {
                long totalSamples = Volatile.Read(ref _totalSamples);

                if (startIndex < 0 || startIndex >= totalSamples)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));

                long endIndex = Math.Min(startIndex + count, totalSamples);
                int actualCount = (int)(endIndex - startIndex);
                double[] result = new double[actualCount];

                long currentIndex = 0;
                int resultIndex = 0;

                // 遍历已满的块
                foreach (var chunk in _chunks)
                {
                    if (currentIndex + chunk.Count <= startIndex)
                    {
                        currentIndex += chunk.Count;
                        continue;
                    }

                    int chunkStartOffset = (int)Math.Max(0, startIndex - currentIndex);
                    int chunkEndOffset = (int)Math.Min(chunk.Count, endIndex - currentIndex);
                    int chunkReadCount = chunkEndOffset - chunkStartOffset;

                    Array.Copy(chunk.Data, chunkStartOffset, result, resultIndex, chunkReadCount);
                    resultIndex += chunkReadCount;
                    currentIndex += chunk.Count;

                    if (resultIndex >= actualCount)
                        break;
                }

                // 检查当前块
                if (resultIndex < actualCount && _currentChunk.Count > 0)
                {
                    int chunkStartOffset = (int)Math.Max(0, startIndex - currentIndex);
                    int chunkReadCount = Math.Min(_currentChunk.Count - chunkStartOffset, actualCount - resultIndex);
                    if (chunkReadCount > 0)
                    {
                        Array.Copy(_currentChunk.Data, chunkStartOffset, result, resultIndex, chunkReadCount);
                    }
                }

                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 读取最新的N个样本
        /// </summary>
        public double[] ReadLatest(int count)
        {
            _lock.EnterReadLock();
            try
            {
                long totalSamples = Volatile.Read(ref _totalSamples);
                long startIndex = Math.Max(0, totalSamples - count);

                // 释放读锁后重新获取，避免死锁
                _lock.ExitReadLock();
                return ReadRange(startIndex, count);
            }
            catch
            {
                // 如果已经释放了锁，确保不会重复释放
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
                throw;
            }
        }

        /// <summary>
        /// 获取所有数据
        /// </summary>
        public double[] GetAllData()
        {
            _lock.EnterReadLock();
            try
            {
                long totalSamples = Volatile.Read(ref _totalSamples);
                double[] result = new double[totalSamples];
                int offset = 0;

                foreach (var chunk in _chunks)
                {
                    Array.Copy(chunk.Data, 0, result, offset, chunk.Count);
                    offset += chunk.Count;
                }

                if (_currentChunk.Count > 0)
                {
                    Array.Copy(_currentChunk.Data, 0, result, offset, _currentChunk.Count);
                }

                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取指定索引的单个数据（使用读锁）
        /// </summary>
        public double this[long index]
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    long totalSamples = Volatile.Read(ref _totalSamples);

                    if (index < 0 || index >= totalSamples)
                        throw new IndexOutOfRangeException();

                    long currentIndex = 0;
                    foreach (var chunk in _chunks)
                    {
                        if (index < currentIndex + chunk.Count)
                        {
                            return chunk.Data[index - currentIndex];
                        }
                        currentIndex += chunk.Count;
                    }

                    return _currentChunk.Data[index - currentIndex];
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _chunks.Clear();
                _currentChunk = new DataChunk(_chunkSize);
                Volatile.Write(ref _totalSamples, 0);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 实现 IDisposable 接口
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _lock?.Dispose();
                }
                _disposed = true;
            }
        }

        ~ChannelBuffer()
        {
            Dispose(false);
        }
    }
}

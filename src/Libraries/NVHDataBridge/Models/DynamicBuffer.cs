using System;
using System.Threading;

namespace NVHDataBridge.Models
{
    // ============================================================
    // 3️⃣ 动态可扩容缓冲区（全量数据，线程安全）
    // ============================================================
    internal sealed class DynamicBuffer<T> where T : unmanaged
    {
        private T[] _buffer;
        private long _count;
        private readonly object _lockObj = new object();

        public long Count => Volatile.Read(ref _count);

        public DynamicBuffer(int initialCapacity = 1024)
        {
            _buffer = new T[initialCapacity];
        }

        // ✅ 单样本追加（可用于实时）
        public void Append(T value)
        {
            lock (_lockObj)
            {
                if (_count >= _buffer.Length)
                {
                    Grow();
                }
                _buffer[_count] = value;
                _count++;
            }
        }

        // ✅ 批量追加（最快，推荐）
        public void AppendRange(ReadOnlySpan<T> values)
        {
            if (values.Length == 0) return;

            lock (_lockObj)
            {
                // 计算需要多少空间
                long requiredCapacity = _count + values.Length;
                while (requiredCapacity > _buffer.Length)
                {
                    Grow();
                }

                // 批量复制
                values.CopyTo(new Span<T>(_buffer, (int)_count, values.Length));
                _count += values.Length;
            }
        }

        // ✅ 获取全部数据（零拷贝，线程安全）
        // 注意：返回的 span 在读取期间是安全的，但如果后续发生扩容，span 可能指向旧数组
        // 对于实时系统，这通常是可接受的，因为读取操作通常很快完成
        public ReadOnlySpan<T> GetAll()
        {
            long count = Count;
            if (count > int.MaxValue)
                throw new InvalidOperationException($"Buffer count ({count}) exceeds maximum span length ({int.MaxValue})");
            
            // 在 lock 内部获取数组引用和计数的快照，确保一致性
            T[] buffer;
            int snapshotCount;
            lock (_lockObj)
            {
                buffer = _buffer;
                snapshotCount = (int)_count;
            }
            
            // 在 lock 外部创建 span，避免持有锁
            // 虽然数组可能被重新分配，但当前引用在读取期间是有效的
            return new ReadOnlySpan<T>(buffer, 0, snapshotCount);
        }

        // ✅ 获取范围（零拷贝，线程安全）
        public ReadOnlySpan<T> GetRange(long start, int length)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), "Start index cannot be negative");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
            if (start > int.MaxValue || length > int.MaxValue)
                throw new ArgumentOutOfRangeException("Start or length exceeds maximum span length");

            // 在 lock 内部获取数组引用和计数的快照，确保一致性
            T[] buffer;
            long snapshotCount;
            lock (_lockObj)
            {
                if (start >= _count)
                    throw new ArgumentOutOfRangeException(nameof(start), $"Start index ({start}) exceeds count ({_count})");
                if (start + length > _count)
                    throw new ArgumentOutOfRangeException(nameof(length), $"Range exceeds buffer count");

                buffer = _buffer;
                snapshotCount = _count;
            }
            
            // 在 lock 外部创建 span
            return new ReadOnlySpan<T>(buffer, (int)start, length);
        }

        // ✅ 内部扩容逻辑（智能增长策略）
        private void Grow()
        {
            long newCapacity;
            
            // 智能扩容策略：
            // - 小容量（< 1MB）：使用 2 倍增长，快速扩容
            // - 中等容量（1MB - 10MB）：使用 1.75 倍增长
            // - 大容量（> 10MB）：使用 1.5 倍增长，减少内存浪费
            const long smallThreshold = 1_000_000;  // 约 1MB for double
            const long mediumThreshold = 10_000_000; // 约 10MB for double
            
            long currentSize = _buffer.Length;
            
            if (currentSize < smallThreshold)
            {
                // 小容量：2倍增长
                newCapacity = Math.Min(currentSize * 2L, int.MaxValue);
            }
            else if (currentSize < mediumThreshold)
            {
                // 中等容量：1.75倍增长
                newCapacity = Math.Min((long)(currentSize * 1.75), int.MaxValue);
            }
            else
            {
                // 大容量：1.5倍增长，减少内存浪费
                newCapacity = Math.Min((long)(currentSize * 1.5), int.MaxValue);
            }
            
            if (newCapacity <= currentSize)
                throw new OutOfMemoryException($"Buffer capacity exceeded. Current: {currentSize}, Attempted: {newCapacity}");

            // 使用 Array.Copy 替代 Array.Resize，更好地控制内存分配
            // Array.Resize 内部也会创建新数组并复制，但这里更明确
            T[] newBuffer = new T[newCapacity];
            Array.Copy(_buffer, 0, newBuffer, 0, (int)_count);
            _buffer = newBuffer;
        }

        /// <summary>
        /// 释放未使用的容量（内存优化）
        /// </summary>
        /// <param name="threshold">如果浪费超过此百分比（0-1），则压缩</param>
        public void TrimExcess(double threshold = 0.1)
        {
            if (threshold < 0 || threshold > 1)
                throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 0 and 1");

            lock (_lockObj)
            {
                long currentCapacity = _buffer.Length;
                long actualCount = _count;
                
                // 计算浪费的百分比
                double wasteRatio = 1.0 - (double)actualCount / currentCapacity;
                
                // 如果浪费超过阈值，则压缩
                if (wasteRatio > threshold && actualCount > 0)
                {
                    // 预留 10% 的额外空间，避免立即再次扩容
                    int newCapacity = (int)Math.Min((long)(actualCount * 1.1), int.MaxValue);
                    if (newCapacity < currentCapacity)
                    {
                        T[] newBuffer = new T[newCapacity];
                        Array.Copy(_buffer, 0, newBuffer, 0, (int)actualCount);
                        _buffer = newBuffer;
                    }
                }
            }
        }

        /// <summary>
        /// 预分配容量（用于已知数据量的场景）
        /// </summary>
        /// <param name="capacity">目标容量</param>
        public void Reserve(long capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative");
            if (capacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(capacity), $"Capacity cannot exceed {int.MaxValue}");

            lock (_lockObj)
            {
                if (capacity > _buffer.Length)
                {
                    T[] newBuffer = new T[capacity];
                    Array.Copy(_buffer, 0, newBuffer, 0, (int)_count);
                    _buffer = newBuffer;
                }
            }
        }

        // ✅ 返回当前底层数组大小（用于调试）
        public int Capacity
        {
            get
            {
                lock (_lockObj)
                {
                    return _buffer.Length;
                }
            }
        }

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        /// <returns>已使用容量和总容量</returns>
        public (long Used, long Total) GetMemoryStats()
        {
            lock (_lockObj)
            {
                return (_count, _buffer.Length);
            }
        }
    }
}

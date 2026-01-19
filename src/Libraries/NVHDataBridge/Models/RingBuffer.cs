using System;
using System.Threading;

namespace NVHDataBridge.Models
{
    // ============================================================
    // 2️⃣ 无锁环形缓冲区（实时窗口，线程安全）
    // ============================================================
    internal sealed class RingBuffer<T> where T : unmanaged
    {
        private readonly T[] _buffer;
        private readonly int _sizeMask;
        private long _writeIndex;

        public RingBuffer(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive");
            if ((size & (size - 1)) != 0)
                throw new ArgumentException("RingBuffer size must be power of two", nameof(size));

            _buffer = new T[size];
            _sizeMask = size - 1;
        }

        /// <summary>
        /// 写入单个值（无锁，线程安全）
        /// </summary>
        public void Write(T value)
        {
            long index = Interlocked.Increment(ref _writeIndex) - 1;
            _buffer[index & _sizeMask] = value;
        }

        /// <summary>
        /// 批量写入（优化版，减少 Interlocked 调用）
        /// </summary>
        public void WriteRange(ReadOnlySpan<T> values)
        {
            if (values.Length == 0) return;

            // 一次性获取起始索引，减少 Interlocked 调用
            long startIndex = Interlocked.Add(ref _writeIndex, values.Length) - values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                _buffer[(startIndex + i) & _sizeMask] = values[i];
            }
        }

        /// <summary>
        /// 读取最新数据（无锁，线程安全）
        /// </summary>
        public int ReadLatest(Span<T> destination)
        {
            if (destination.IsEmpty) return 0;

            long write = Volatile.Read(ref _writeIndex);
            int count = Math.Min(destination.Length, _buffer.Length);

            long start = write - count;
            if (start < 0) start = 0;

            for (int i = 0; i < count; i++)
            {
                destination[i] = _buffer[(start + i) & _sizeMask];
            }
            return count;
        }

        /// <summary>
        /// 获取当前写入位置（用于调试）
        /// </summary>
        public long WritePosition => Volatile.Read(ref _writeIndex);
    }
}

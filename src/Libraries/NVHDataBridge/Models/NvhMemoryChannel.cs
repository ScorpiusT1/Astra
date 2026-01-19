using System;
using System.Runtime.InteropServices;

namespace NVHDataBridge.Models
{
    // ============================================================
    // 5️⃣ 泛型通道（核心：支持默认参数 + 动态扩容 + 线程安全）
    // ============================================================
    /// <summary>
    /// 泛型内存通道，支持实时和批量数据写入，线程安全
    /// </summary>
    /// <typeparam name="T">数据类型，必须是非托管类型</typeparam>
    public sealed class NvhMemoryChannel<T> : NvhMemoryChannelBase
        where T : unmanaged
    {
        // 常量定义
        private const int DEFAULT_RING_BUFFER_SIZE = 262144; // 256KB (2^18)
        private const int DEFAULT_INITIAL_CAPACITY = 131072; // 128KB
        private const int PROPERTY_UPDATE_THRESHOLD = 1000; // 批量写入超过此阈值才更新 Properties
        
        // 扩容阈值常量（用于智能预分配）
        private const long LARGE_DATA_THRESHOLD = 1_000_000; // 百万级数据阈值

        // ✅ 实时环形缓冲（可选，null 表示不使用）
        private readonly RingBuffer<T>? _ring;

        // ✅ 全量动态缓冲（可自动扩容）
        private readonly DynamicBuffer<T> _allData;

        public override Type DataType => typeof(T);
        public override long TotalSamples => _allData.Count;

        // ============================================================
        // 构造函数选项 1️⃣：完全默认
        // ============================================================
        /// <summary>
        /// 创建通道，使用默认配置
        /// </summary>
        internal NvhMemoryChannel(string name)
            : this(name, ringBufferSize: DEFAULT_RING_BUFFER_SIZE)
        {
        }

        // ============================================================
        // 构造函数选项 2️⃣：指定 RingBuffer 大小
        // ============================================================
        /// <summary>
        /// 创建通道，指定环形缓冲区大小
        /// </summary>
        internal NvhMemoryChannel(string name, int ringBufferSize)
            : this(name, ringBufferSize, initialCapacity: DEFAULT_INITIAL_CAPACITY)
        {
        }

        // ============================================================
        // 构造函数选项 3️⃣：完整控制
        // ============================================================
        /// <summary>
        /// 创建通道，完整控制所有参数
        /// </summary>
        /// <param name="name">通道名称</param>
        /// <param name="ringBufferSize">环形缓冲区大小（必须是2的幂，0表示不使用）</param>
        /// <param name="initialCapacity">初始容量</param>
        /// <param name="estimatedTotalSamples">预估总样本数（用于预分配，可选）</param>
        internal NvhMemoryChannel(
            string name,
            int ringBufferSize,
            int initialCapacity,
            long? estimatedTotalSamples = null)
            : base(name)
        {
            if (ringBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(ringBufferSize), "RingBuffer size cannot be negative");
            if (ringBufferSize > 0 && (ringBufferSize & (ringBufferSize - 1)) != 0)
                throw new ArgumentException("RingBuffer size must be power of two or zero", nameof(ringBufferSize));
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity must be positive");
            if (estimatedTotalSamples.HasValue && estimatedTotalSamples.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(estimatedTotalSamples), "Estimated total samples cannot be negative");

            // 如果提供了预估样本数，智能调整初始容量
            if (estimatedTotalSamples.HasValue && estimatedTotalSamples.Value > initialCapacity)
            {
                // 预分配容量：预估值的 120%，避免频繁扩容
                long targetCapacity = (long)(estimatedTotalSamples.Value * 1.2);
                initialCapacity = (int)Math.Min(targetCapacity, int.MaxValue);
            }

            // RingBuffer 可选：如果为 0 则不创建（节省内存）
            _ring = ringBufferSize > 0 ? new RingBuffer<T>(ringBufferSize) : null!;
            _allData = new DynamicBuffer<T>(initialCapacity);
            
            // 如果提供了预估样本数，预分配容量
            if (estimatedTotalSamples.HasValue && estimatedTotalSamples.Value > initialCapacity)
            {
                _allData.Reserve((long)(estimatedTotalSamples.Value * 1.2));
            }
        }

        // =========================
        // ✅ 实时写入（单样本）
        // =========================
        /// <summary>
        /// 写入单个样本（线程安全）
        /// </summary>
        public void WriteSample(T value)
        {
            _ring?.Write(value);
            _allData.Append(value);
            // 单样本写入时立即更新 Properties（实时性要求）
            UpdateTotalSamplesToProperties();
        }

        // =========================
        // ✅ 批量写入（最快，推荐）
        // =========================
        /// <summary>
        /// 批量写入样本（线程安全，性能优化）
        /// </summary>
        /// <param name="samples">样本数据</param>
        public void WriteSamples(ReadOnlySpan<T> samples)
        {
            if (samples.Length == 0) return;

            // 一次性追加到全量缓冲
            _allData.AppendRange(samples);

            // 优化：使用批量写入方法，减少 Interlocked 调用
            _ring?.WriteRange(samples);

            // 性能优化：批量写入时，只有超过阈值才更新 Properties
            // 减少频繁的字典操作，提升性能
            if (samples.Length >= PROPERTY_UPDATE_THRESHOLD)
            {
                UpdateTotalSamplesToProperties();
            }
        }

        /// <summary>
        /// 手动刷新总采样点到 Properties（用于批量写入后确保同步）
        /// </summary>
        public void FlushTotalSamplesToProperties()
        {
            UpdateTotalSamplesToProperties();
        }

        // ✅ 自动更新总采样点到 Properties（线程安全）
        private void UpdateTotalSamplesToProperties()
        {
            Properties.Set("wf_samples", TotalSamples);
        }

        // =========================
        // ✅ 实时读取（最近数据，线程安全）
        // =========================
        /// <summary>
        /// 读取最新的数据（从环形缓冲区）
        /// </summary>
        /// <param name="destination">目标缓冲区</param>
        /// <returns>实际读取的数据量，如果未启用 RingBuffer 返回 0</returns>
        public int ReadLatest(Span<T> destination)
        {
            if (destination.IsEmpty || _ring == null) return 0;
            return _ring.ReadLatest(destination);
        }

        // =========================
        // ✅ 范围读取（全量，线程安全）
        // =========================
        /// <summary>
        /// 读取指定范围的数据
        /// </summary>
        /// <param name="start">起始索引</param>
        /// <param name="count">数量</param>
        /// <returns>数据范围</returns>
        public ReadOnlySpan<T> ReadRange(long start, int count)
        {
            return _allData.GetRange(start, count);
        }

        // =========================
        // ✅ 一次性获取全部数据（线程安全）
        // =========================
        /// <summary>
        /// 获取所有数据
        /// </summary>
        /// <returns>所有数据的只读视图</returns>
        public ReadOnlySpan<T> ReadAll()
        {
            return _allData.GetAll();
        }

        // ✅ 调试用：查看缓冲区大小
        /// <summary>
        /// 获取当前缓冲区容量
        /// </summary>
        public int BufferCapacity => _allData.Capacity;

        /// <summary>
        /// 释放未使用的内存（内存优化）
        /// </summary>
        /// <param name="threshold">如果浪费超过此百分比（0-1），则压缩</param>
        public void TrimExcess(double threshold = 0.1)
        {
            _allData.TrimExcess(threshold);
        }

        /// <summary>
        /// 预分配容量（用于已知数据量的场景，避免频繁扩容）
        /// </summary>
        /// <param name="capacity">目标容量</param>
        public void Reserve(long capacity)
        {
            _allData.Reserve(capacity);
        }

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        /// <returns>内存统计信息</returns>
        public MemoryStats GetMemoryStats()
        {
            var (used, total) = _allData.GetMemoryStats();
            long ringBufferSize = _ring != null ? _ring.WritePosition : 0;
            int elementSize = Marshal.SizeOf<T>();
            
            return new MemoryStats
            {
                UsedSamples = used,
                TotalCapacity = total,
                RingBufferSize = ringBufferSize,
                WasteRatio = total > 0 ? 1.0 - (double)used / total : 0.0,
                ElementSize = elementSize
            };
        }

        /// <summary>
        /// 检查是否启用了 RingBuffer
        /// </summary>
        public bool IsRingBufferEnabled => _ring != null;
    }

    /// <summary>
    /// 内存使用统计信息
    /// </summary>
    public struct MemoryStats
    {
        /// <summary>已使用的样本数</summary>
        public long UsedSamples { get; set; }
        
        /// <summary>总容量（样本数）</summary>
        public long TotalCapacity { get; set; }
        
        /// <summary>RingBuffer 大小（如果启用）</summary>
        public long RingBufferSize { get; set; }
        
        /// <summary>内存浪费比例（0-1）</summary>
        public double WasteRatio { get; set; }
        
        /// <summary>元素大小（字节）</summary>
        public int ElementSize { get; set; }
        
        /// <summary>已使用的内存（字节）</summary>
        public long UsedBytes => UsedSamples * ElementSize;
        
        /// <summary>总分配的内存（字节）</summary>
        public long TotalBytes => TotalCapacity * ElementSize;
        
        /// <summary>浪费的内存（字节）</summary>
        public long WastedBytes => (TotalCapacity - UsedSamples) * ElementSize;
    }
}

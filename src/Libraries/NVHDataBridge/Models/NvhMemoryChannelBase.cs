using System;

namespace NVHDataBridge.Models
{
    // ============================================================
    // 4️⃣ 通道基类（属性）
    // ============================================================
    public abstract class NvhMemoryChannelBase
    {
        private string _name; // 改为私有字段，支持重命名

        public string Name => _name;
        public PropertyBag Properties { get; } = new PropertyBag(8);

        protected NvhMemoryChannelBase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(name));
            
            _name = name;
        }

        // ✅ 重命名通道（供 Group 调用）
        internal void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New channel name cannot be null or empty", nameof(newName));
            
            _name = newName;
        }

        public abstract Type DataType { get; }
        public abstract long TotalSamples { get; }

        // ============================================================
        // 数据读取方法（泛型，在基类中提供统一接口）
        // ============================================================

        /// <summary>
        /// 读取指定类型的数据（泛型方法）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="start">起始索引</param>
        /// <param name="count">数量</param>
        /// <returns>数据范围，如果类型不匹配返回空范围</returns>
        public ReadOnlySpan<T> ReadData<T>(long start, int count) where T : unmanaged
        {
            if (this is NvhMemoryChannel<T> typedChannel)
            {
                return typedChannel.ReadRange(start, count);
            }
            return ReadOnlySpan<T>.Empty;
        }

        /// <summary>
        /// 读取所有指定类型的数据（泛型方法）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns>所有数据，如果类型不匹配返回空范围</returns>
        public ReadOnlySpan<T> ReadAllData<T>() where T : unmanaged
        {
            if (this is NvhMemoryChannel<T> typedChannel)
            {
                return typedChannel.ReadAll();
            }
            return ReadOnlySpan<T>.Empty;
        }

        /// <summary>
        /// 读取最新的指定类型数据（泛型方法）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="destination">目标缓冲区</param>
        /// <returns>实际读取的数据量，如果类型不匹配返回 0</returns>
        public int ReadLatestData<T>(Span<T> destination) where T : unmanaged
        {
            if (this is NvhMemoryChannel<T> typedChannel)
            {
                return typedChannel.ReadLatest(destination);
            }
            return 0;
        }

        /// <summary>
        /// 写入指定类型的数据（泛型方法）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="value">单个值</param>
        /// <returns>如果类型匹配且写入成功返回 true</returns>
        public bool WriteData<T>(T value) where T : unmanaged
        {
            if (this is NvhMemoryChannel<T> typedChannel)
            {
                typedChannel.WriteSample(value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 批量写入指定类型的数据（泛型方法）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="samples">样本数据</param>
        /// <returns>如果类型匹配且写入成功返回 true</returns>
        public bool WriteData<T>(ReadOnlySpan<T> samples) where T : unmanaged
        {
            if (this is NvhMemoryChannel<T> typedChannel)
            {
                typedChannel.WriteSamples(samples);
                return true;
            }
            return false;
        }

        // ============================================================
        // TDMS 波形属性（自动同步到 Properties）
        // ============================================================

        /// <summary>
        /// 波形开始时间（wf_start_time）
        /// 自动同步到 Properties["wf_start_time"]
        /// </summary>
        public DateTime? WfStartTime
        {
            get => Properties.Get<DateTime?>("wf_start_time");
            set => Properties.Set("wf_start_time", value);
        }

        /// <summary>
        /// 波形开始偏移（wf_start_offset）
        /// 自动同步到 Properties["wf_start_offset"]
        /// </summary>
        public double? WfStartOffset
        {
            get => Properties.Get<double?>("wf_start_offset");
            set => Properties.Set("wf_start_offset", value);
        }

        /// <summary>
        /// 波形采样间隔（wf_increment）
        /// 自动同步到 Properties["wf_increment"]
        /// </summary>
        public double? WfIncrement
        {
            get => Properties.Get<double?>("wf_increment");
            set => Properties.Set("wf_increment", value);
        }

        /// <summary>
        /// 波形采样数量（wf_samples）
        /// Getter 返回实际的 TotalSamples，Setter 用于设置元数据到 Properties
        /// 实际值始终从 TotalSamples 获取，Properties 中的值仅用于 TDMS 元数据
        /// </summary>
        public long? WfSamples
        {
            get => TotalSamples; // 直接返回实际样本数，避免重复
            set => Properties.Set("wf_samples", value); // Setter 仅用于设置元数据
        }
    }
}

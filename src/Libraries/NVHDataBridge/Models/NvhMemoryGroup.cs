using System;
using System.Collections.Generic;
using System.Linq;

namespace NVHDataBridge.Models
{
    // ============================================================
    // 6️⃣ Group（支持灵活创建）
    // ============================================================
    public sealed class NvhMemoryGroup
    {
        private readonly Dictionary<string, NvhMemoryChannelBase> _channels;

        private string _name; // 改为私有字段，支持重命名

        public string Name => _name;
        public PropertyBag Properties { get; } = new PropertyBag(8);

        public IReadOnlyDictionary<string, NvhMemoryChannelBase> Channels => _channels;

        internal NvhMemoryGroup(string name, int estimatedChannelCount = 16)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Group name cannot be null or empty", nameof(name));
            
            _name = name;
            _channels = new Dictionary<string, NvhMemoryChannelBase>(estimatedChannelCount);
        }

        // ✅ 内部重命名方法（供 File 调用）
        internal void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New group name cannot be null or empty", nameof(newName));
            
            _name = newName;
        }

        // ============================================================
        // 创建通道选项 1️⃣：完全默认（最简）
        // ============================================================
        /// <summary>
        /// 创建通道（使用默认配置）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="name">通道名称</param>
        /// <returns>创建的通道</returns>
        /// <exception cref="ArgumentException">名称为空或已存在</exception>
        public NvhMemoryChannel<T> CreateChannel<T>(string name)
            where T : unmanaged
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(name));
            
            if (_channels.ContainsKey(name))
                throw new ArgumentException($"Channel '{name}' already exists in group '{Name}'", nameof(name));

            var channel = new NvhMemoryChannel<T>(name);
            _channels.Add(name, channel);
            return channel;
        }

        // ============================================================
        // 创建通道选项 2️⃣：指定 RingBuffer 大小
        // ============================================================
        /// <summary>
        /// 创建通道（指定环形缓冲区大小）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="name">通道名称</param>
        /// <param name="ringBufferSize">环形缓冲区大小（必须是2的幂）</param>
        /// <returns>创建的通道</returns>
        /// <exception cref="ArgumentException">名称为空或已存在</exception>
        public NvhMemoryChannel<T> CreateChannel<T>(
            string name,
            int ringBufferSize)
            where T : unmanaged
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(name));
            
            if (_channels.ContainsKey(name))
                throw new ArgumentException($"Channel '{name}' already exists in group '{Name}'", nameof(name));

            var channel = new NvhMemoryChannel<T>(name, ringBufferSize);
            _channels.Add(name, channel);
            return channel;
        }

        // ============================================================
        // 创建通道选项 3️⃣：完整控制
        // ============================================================
        /// <summary>
        /// 创建通道（完整控制所有参数）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="name">通道名称</param>
        /// <param name="ringBufferSize">环形缓冲区大小（必须是2的幂，0表示不使用）</param>
        /// <param name="initialCapacity">初始容量</param>
        /// <param name="estimatedTotalSamples">预估总样本数（用于预分配，可选）</param>
        /// <returns>创建的通道</returns>
        /// <exception cref="ArgumentException">名称为空或已存在</exception>
        public NvhMemoryChannel<T> CreateChannel<T>(
            string name,
            int ringBufferSize,
            int initialCapacity,
            long? estimatedTotalSamples = null)
            where T : unmanaged
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(name));
            
            if (_channels.ContainsKey(name))
                throw new ArgumentException($"Channel '{name}' already exists in group '{Name}'", nameof(name));

            var channel = new NvhMemoryChannel<T>(name, ringBufferSize, initialCapacity, estimatedTotalSamples);
            _channels.Add(name, channel);
            return channel;
        }

        // ✅ 获取已存在的通道（返回基类，不需要泛型）
        /// <summary>
        /// 获取已存在的通道
        /// </summary>
        /// <param name="name">通道名称</param>
        /// <returns>通道实例，如果不存在返回 null</returns>
        public NvhMemoryChannelBase? GetChannel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            _channels.TryGetValue(name, out var channel);
            return channel;
        }

        /// <summary>
        /// 尝试获取通道
        /// </summary>
        /// <param name="name">通道名称</param>
        /// <param name="channel">输出通道实例</param>
        /// <returns>如果找到返回 true</returns>
        public bool TryGetChannel(string name, out NvhMemoryChannelBase? channel)
        {
            channel = GetChannel(name);
            return channel != null;
        }

        /// <summary>
        /// 检查通道是否存在
        /// </summary>
        /// <param name="name">通道名称</param>
        /// <returns>如果存在返回 true</returns>
        public bool ContainsChannel(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && _channels.ContainsKey(name);
        }

        // ✅ 获取所有通道名
        public IEnumerable<string> GetChannelNames()
        {
            return _channels.Keys;
        }

        // ✅ 获取所有通道名的数组（方便使用）
        public string[] GetChannelNamesArray()
        {
            return _channels.Keys.ToArray();
        }

        // ✅ 重命名通道
        public bool RenameChannel(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName))
                throw new ArgumentException("Old channel name cannot be null or empty", nameof(oldName));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New channel name cannot be null or empty", nameof(newName));
            if (oldName == newName)
                return true; // 名称相同，无需重命名

            if (!_channels.TryGetValue(oldName, out var channel))
                return false; // 旧通道不存在

            if (_channels.ContainsKey(newName))
                throw new ArgumentException($"Channel '{newName}' already exists in group '{Name}'", nameof(newName));

            // 从字典中移除旧键
            _channels.Remove(oldName);
            
            // 更新通道内部名称
            channel.Rename(newName);
            
            // 添加新键
            _channels.Add(newName, channel);
            
            return true;
        }

        // ✅ 尝试重命名通道（不抛异常）
        public bool TryRenameChannel(string oldName, string newName)
        {
            try
            {
                return RenameChannel(oldName, newName);
            }
            catch
            {
                return false;
            }
        }
    }
}

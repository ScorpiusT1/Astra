using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NVHDataBridge.Models
{
    // ============================================================
    // 1️⃣ 属性容器（三层都需要用，线程安全）
    // ============================================================
    /// <summary>
    /// 线程安全的属性容器，用于存储键值对属性
    /// </summary>
    public sealed class PropertyBag
    {
        private readonly Dictionary<string, object> _values;
        private readonly object _lockObj = new object();
        private bool _isSealed;

        /// <summary>
        /// 创建属性容器
        /// </summary>
        /// <param name="capacity">初始容量</param>
        public PropertyBag(int capacity = 8)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative");
            
            _values = new Dictionary<string, object>(capacity);
        }

        /// <summary>
        /// 设置属性值（线程安全）
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <exception cref="ArgumentNullException">键为空</exception>
        /// <exception cref="InvalidOperationException">容器已密封</exception>
        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
            lock (_lockObj)
            {
                if (_isSealed)
                    throw new InvalidOperationException("PropertyBag is sealed and cannot be modified");
                
                _values[key] = value!;
            }
        }

        /// <summary>
        /// 尝试获取属性值（线程安全）
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="value">输出值</param>
        /// <returns>如果找到且类型匹配返回 true，否则返回 false</returns>
        public bool TryGet<T>(string key, out T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = default!;
                return false;
            }

            lock (_lockObj)
            {
                if (_values.TryGetValue(key, out var obj))
                {
                    if (obj is T typed)
                    {
                        value = typed;
                        return true;
                    }
                    // 尝试类型转换
                    try
                    {
                        value = (T)Convert.ChangeType(obj, typeof(T));
                        return true;
                    }
                    catch
                    {
                        // 转换失败
                    }
                }
            }
            
            value = default!;
            return false;
        }

        /// <summary>
        /// 获取属性值（线程安全）
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>属性值或默认值</returns>
        public T Get<T>(string key, T defaultValue = default!)
        {
            return TryGet<T>(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 检查是否包含指定键（线程安全）
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>如果包含返回 true</returns>
        public bool Contains(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            
            lock (_lockObj)
            {
                return _values.ContainsKey(key);
            }
        }

        /// <summary>
        /// 获取所有条目（线程安全，返回快照）
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Entries
        {
            get
            {
                lock (_lockObj)
                {
                    return new Dictionary<string, object>(_values);
                }
            }
        }

        /// <summary>
        /// 获取属性数量（线程安全）
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lockObj)
                {
                    return _values.Count;
                }
            }
        }

        /// <summary>
        /// 密封容器，使其只读（线程安全）
        /// </summary>
        public void Seal()
        {
            lock (_lockObj)
            {
                _isSealed = true;
            }
        }

        /// <summary>
        /// 检查容器是否已密封
        /// </summary>
        public bool IsSealed
        {
            get
            {
                lock (_lockObj)
                {
                    return _isSealed;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志上下文
    /// 用于存储和传递日志的上下文信息，避免重复传递相同的上下文数据
    /// </summary>
    public class LoggerContext
    {
        private readonly Dictionary<string, object> _context = new Dictionary<string, object>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// 设置上下文值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void Set(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("键不能为空", nameof(key));

            lock (_lockObject)
            {
                _context[key] = value;
            }
        }

        /// <summary>
        /// 获取上下文值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>值，如果不存在则返回 null</returns>
        public object Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            lock (_lockObject)
            {
                return _context.TryGetValue(key, out var value) ? value : null;
            }
        }

        /// <summary>
        /// 获取上下文值（泛型版本）
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <returns>值，如果不存在或类型不匹配则返回默认值</returns>
        public T Get<T>(string key)
        {
            var value = Get(key);
            if (value is T typedValue)
            {
                return typedValue;
            }
            return default(T);
        }

        /// <summary>
        /// 移除上下文值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            lock (_lockObject)
            {
                return _context.Remove(key);
            }
        }

        /// <summary>
        /// 检查是否包含指定的键
        /// </summary>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            lock (_lockObject)
            {
                return _context.ContainsKey(key);
            }
        }

        /// <summary>
        /// 清除所有上下文
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _context.Clear();
            }
        }

        /// <summary>
        /// 获取所有上下文数据的副本
        /// </summary>
        /// <returns>上下文数据的副本</returns>
        public Dictionary<string, object> GetAll()
        {
            lock (_lockObject)
            {
                return new Dictionary<string, object>(_context);
            }
        }

        /// <summary>
        /// 批量设置上下文值
        /// </summary>
        /// <param name="values">键值对集合</param>
        public void SetRange(Dictionary<string, object> values)
        {
            if (values == null)
                return;

            lock (_lockObject)
            {
                foreach (var kv in values)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                    {
                        _context[kv.Key] = kv.Value;
                    }
                }
            }
        }

        /// <summary>
        /// 创建作用域上下文（用于临时上下文）
        /// </summary>
        /// <returns>作用域上下文，使用 using 语句自动清理</returns>
        public IDisposable CreateScope(Dictionary<string, object> scopeContext)
        {
            return new ContextScope(this, scopeContext);
        }

        /// <summary>
        /// 上下文作用域（用于临时上下文）
        /// </summary>
        private class ContextScope : IDisposable
        {
            private readonly LoggerContext _parent;
            private readonly Dictionary<string, object> _scopeContext;
            private readonly Dictionary<string, object> _originalValues = new Dictionary<string, object>();

            public ContextScope(LoggerContext parent, Dictionary<string, object> scopeContext)
            {
                _parent = parent;
                _scopeContext = scopeContext;

                if (_scopeContext != null)
                {
                    // 保存原始值
                    foreach (var key in _scopeContext.Keys)
                    {
                        if (_parent.ContainsKey(key))
                        {
                            _originalValues[key] = _parent.Get(key);
                        }
                    }

                    // 设置作用域值
                    _parent.SetRange(_scopeContext);
                }
            }

            public void Dispose()
            {
                if (_scopeContext != null)
                {
                    // 恢复原始值或移除作用域值
                    foreach (var key in _scopeContext.Keys)
                    {
                        if (_originalValues.ContainsKey(key))
                        {
                            _parent.Set(key, _originalValues[key]);
                        }
                        else
                        {
                            _parent.Remove(key);
                        }
                    }
                }
            }
        }
    }
}


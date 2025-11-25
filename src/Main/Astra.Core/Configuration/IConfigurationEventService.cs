using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置事件服务接口 - 符合单一职责原则（SRP）
    /// 仅负责配置变更的事件订阅和通知
    /// </summary>
    public interface IConfigurationEventService
    {
        /// <summary>
        /// 订阅配置变更事件
        /// </summary>
        void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;

        /// <summary>
        /// 取消订阅
        /// </summary>
        void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;

        /// <summary>
        /// 发布配置变更事件
        /// </summary>
        void Publish<T>(T config, ConfigChangeType changeType) where T : class, IConfig;

        /// <summary>
        /// 清除所有订阅
        /// </summary>
        void ClearSubscriptions();

        /// <summary>
        /// 清除指定类型的订阅
        /// </summary>
        void ClearSubscriptions<T>() where T : class, IConfig;

        /// <summary>
        /// 获取订阅者数量
        /// </summary>
        int GetSubscriberCount<T>() where T : class, IConfig;
    }

    /// <summary>
    /// 配置事件服务实现 - 线程安全的事件发布/订阅机制
    /// 符合单一职责原则和开闭原则
    /// </summary>
    public class ConfigurationEventService : IConfigurationEventService
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers 
            = new ConcurrentDictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var type = typeof(T);
            var subscribers = _subscribers.GetOrAdd(type, _ => new List<Delegate>());

            lock (subscribers)
            {
                if (!subscribers.Contains(callback))
                {
                    subscribers.Add(callback);
                }
            }
        }

        public void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var subscribers))
            {
                lock (subscribers)
                {
                    subscribers.Remove(callback);
                }
            }
        }

        public void Publish<T>(T config, ConfigChangeType changeType) where T : class, IConfig
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var subscribers))
                return;

            // 复制订阅者列表，避免在迭代时修改集合
            List<Delegate> subscribersCopy;
            lock (subscribers)
            {
                subscribersCopy = new List<Delegate>(subscribers);
            }

            // 通知所有订阅者
            foreach (var subscriber in subscribersCopy)
            {
                try
                {
                    ((Action<T, ConfigChangeType>)subscriber)?.Invoke(config, changeType);
                }
                catch (Exception ex)
                {
                    // 捕获订阅者回调中的异常，避免影响其他订阅者
                    // 这里应该记录日志，但为了避免依赖日志系统，暂时使用Console
                    Console.Error.WriteLine($"配置事件通知失败: {ex.Message}");
                }
            }
        }

        public void ClearSubscriptions()
        {
            _subscribers.Clear();
        }

        public void ClearSubscriptions<T>() where T : class, IConfig
        {
            var type = typeof(T);
            _subscribers.TryRemove(type, out _);
        }

        public int GetSubscriberCount<T>() where T : class, IConfig
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var subscribers))
            {
                lock (subscribers)
                {
                    return subscribers.Count;
                }
            }
            return 0;
        }
    }
}

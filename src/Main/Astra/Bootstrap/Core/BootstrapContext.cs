using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Core
{
    /// <summary>
    /// 启动上下文，用于在任务之间共享数据和服务
    /// </summary>
    public class BootstrapContext
    {
        private readonly ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>();

        public BootstrapContext()
        {
            StartTime = DateTime.Now;
        }

        /// <summary>
        /// 启动时间
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// 服务集合（用于依赖注入配置）
        /// </summary>
        public IServiceCollection Services { get; set; }

        /// <summary>
        /// 服务提供者（构建后）
        /// </summary>
        public IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 日志记录器
        /// </summary>
        public IBootstrapLogger Logger { get; set; }

        /// <summary>
        /// 命令行参数
        /// </summary>
        public string[] CommandLineArgs { get; set; }

        /// <summary>
        /// 设置共享数据
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            _data[key] = value;
        }

        /// <summary>
        /// 获取共享数据
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_data.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 尝试获取共享数据
        /// </summary>
        public bool TryGetData<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 获取所有数据键
        /// </summary>
        public IEnumerable<string> GetAllKeys()
        {
            return _data.Keys;
        }
    }

    /// <summary>
    /// 启动日志接口
    /// </summary>
    public interface IBootstrapLogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
    }
}

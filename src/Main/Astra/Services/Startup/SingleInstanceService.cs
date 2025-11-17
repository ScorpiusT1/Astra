using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;

namespace Astra.Services.Startup
{
    /// <summary>
    /// 单实例服务 - 确保应用程序只能启动一个实例
    /// 
    /// 使用命名互斥锁（Mutex）实现单实例检查，支持跨用户会话的唯一性。
    /// 实现 IDisposable 接口，确保资源正确释放。
    /// </summary>
    public class SingleInstanceService : IDisposable
    {
        private Mutex _mutex;
        private readonly string _mutexName;
        private readonly ILogger<SingleInstanceService> _logger;
        private bool _isDisposed;

        /// <summary>
        /// 初始化单实例服务
        /// </summary>
        /// <param name="mutexName">互斥锁名称，如果为 null 则使用默认名称</param>
        /// <param name="logger">日志记录器，如果为 null 则使用空日志记录器</param>
        public SingleInstanceService(string mutexName = null, ILogger<SingleInstanceService> logger = null)
        {
            _mutexName = mutexName ?? "Global\\Astra_SingleInstance_Mutex";
            _logger = logger ?? NullLogger<SingleInstanceService>.Instance;
        }

        /// <summary>
        /// 检查并确保当前是唯一实例
        /// </summary>
        /// <returns>如果当前是唯一实例返回 true，如果已有实例运行返回 false</returns>
        public bool EnsureSingleInstance()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SingleInstanceService));
            }

            try
            {
                // 尝试创建命名互斥锁
                _mutex = new Mutex(
                    initiallyOwned: true,
                    name: _mutexName,
                    createdNew: out bool isNewInstance);

                // 如果互斥锁已存在，说明已有实例在运行
                if (!isNewInstance)
                {
                    _logger.LogWarning("检测到已有实例运行，互斥锁名称: {MutexName}", _mutexName);
                    
                    // 释放当前互斥锁引用（因为不是我们创建的）
                    _mutex?.Dispose();
                    _mutex = null;
                    return false;
                }

                _logger.LogInformation("单实例检查通过，互斥锁名称: {MutexName}", _mutexName);
                return true;
            }
            catch (Exception ex)
            {
                // 如果创建互斥锁失败，记录错误但允许启动（容错处理）
                _logger.LogWarning(ex, "创建单实例互斥锁失败，允许启动（容错处理）。互斥锁名称: {MutexName}", _mutexName);
                return true;
            }
        }

        /// <summary>
        /// 释放单实例互斥锁资源
        /// </summary>
        public void Release()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                    _mutex = null;
                    _logger.LogInformation("单实例互斥锁已释放");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放单实例互斥锁失败");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                Release();
                _isDisposed = true;
            }
        }
    }
}


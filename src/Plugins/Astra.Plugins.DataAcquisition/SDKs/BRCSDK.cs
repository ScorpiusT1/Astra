using Astra.Plugins.DataAcquisition.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Astra.Plugins.DataAcquisition.SDKs
{
    ///// <summary>
    ///// 耦合模式枚举
    ///// </summary>
    //public enum CouplingMode : int
    //{
    //    DC = 0,
    //    AC = 1,
    //}

    /// <summary>
    /// 时钟/触发源枚举
    /// </summary>
    public enum SourceType : int
    {
        ONBOARD = 0,
        EXTERNAL = 1,
    }

    /// <summary>
    /// BRC数据采集卡SDK封装
    /// </summary>
    public static class BRCSDK
    {
        private const string DLLPATH = "Lib/x64/brc_daq_sdk.dll";
        private const int BUFFER_SIZE = 1024;
        private const int ARRAY_BUFFER_SIZE = 512;
        private static readonly object _lockObj = new object();

        #region ============ DLL导入声明 ============

        // 获取错误信息
        [DllImport(DLLPATH, EntryPoint = "get_last_error", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int GetLastErrorNative(byte* pErr);

        // 扫描设备
        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int scan_modules();

        // 获取模块信息
        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int get_module_info(int index, ModuleInfoType moduleInfoType, void* ptr1, void* ptr2);

        // 连接/断开模块
        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int connect_module(int index);

        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int disconnect_module(int mHandle);

        // 模块属性
        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int get_module_property(int mHandle, ModulePropertyType propertyType, void* ptr1, void* ptr2);

        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int set_module_property(int mHandle, ModulePropertyType propertyType, void* ptr1, void* ptr2);

        // 通道属性
        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int get_channel_property(int mHandle, int channelIndex, ChannelPropertyType propertyType, void* ptr1, void* ptr2);

        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int set_channel_property(int mHandle, int channelIndex, ChannelPropertyType propertyType, void* ptr1, void* ptr2);

        // 数据采集
        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int start(int mHandle, bool rawValue);

        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int stop(int mHandle);

        [DllImport(DLLPATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int get_channels_data(int mHandle, double* data_array, int length, int data_array_length, int timeout);

        #endregion

        #region ============ 枚举定义 ============

        private enum ModuleInfoType : int
        {
            ProductName = 1,
            DeviceId = 2,
            ChannelCount = 3,
            SampleRateOptions = 4,
            GainOptions = 5,
            CurrentOptions = 6,
            CouplingOptions = 7,
        }

        private enum ModulePropertyType : int
        {
            ClockSource = 1,
            TrigerSource = 2,
            SampleRate = 3
        }

        private enum ChannelPropertyType : int
        {
            Enabled = 1,
            Gain = 2,
            Current = 3,
            CouplingMode = 4
        }

        #endregion

        #region ============ 错误处理 ============

        /// <summary>
        /// 获取最后一条错误信息
        /// </summary>
        /// <returns>错误信息字符串</returns>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe string GetLastError()
        {
            byte[] utf8Bytes = new byte[BUFFER_SIZE];
            fixed (byte* ptr = utf8Bytes)
            {
                int result = GetLastErrorNative(ptr);
                if (result != 0)
                {
                    throw new Exception($"获取错误信息失败，错误码: {result}");
                }

                int length = Array.IndexOf(utf8Bytes, (byte)0);
                if (length < 0) length = BUFFER_SIZE;

                return Encoding.UTF8.GetString(utf8Bytes, 0, length);
            }
        }

        /// <summary>
        /// 检查SDK调用结果并抛出异常
        /// </summary>
        private static void ThrowIfError(int result, string operation)
        {
            if (result != 0)
            {
                string error = GetLastError();
                throw new Exception($"{operation}失败: {error}");
            }
        }

        /// <summary>
        /// 从UTF8字节数组读取字符串
        /// </summary>
        private static unsafe string ReadUtf8String(byte[] buffer, int maxLength = BUFFER_SIZE)
        {
            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = Math.Min(maxLength, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        #endregion

        #region ============ 设备扫描 ============

        private static List<ModuleInfo> _moduleInfos = new List<ModuleInfo>();

        /// <summary>
        /// 扫描可用的设备
        /// </summary>
        /// <returns>可用设备列表</returns>
        public static List<ModuleInfo> ScanModules()
        {
            lock (_lockObj)
            {
                var moduleCount = scan_modules();

                if (moduleCount <= 0)
                {
                    _moduleInfos.Clear();
                    return _moduleInfos;
                }

                _moduleInfos = Enumerable.Range(0, moduleCount).Select(index =>
                {
                    try
                    {
                        return new ModuleInfo
                        {
                            DeviceId = GetModuleInfoDeviceId(index),
                            ProductName = GetModuleInfoProductName(index),
                            ChannelCount = GetModuleInfoChannelCount(index),
                            SampleRateOptions = GetModuleInfoSampleRateOptions(index),
                            CurrentOptions = GetModuleInfoSampleCurrentOptions(index),
                            CouplingOptions = GetModuleInfoSampleCouplingOptions(index),
                        };
                    }
                    catch
                    {
                        // 忽略单个设备信息获取失败，继续处理其他设备
                        return null;
                    }
                }).Where(m => m != null).ToList();

                return _moduleInfos;
            }
        }

        #endregion

        #region ============ 获取设备基础信息 ============

        /// <summary>
        /// 获取设备产品名称
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe string GetModuleInfoProductName(int index)
        {
            byte[] bytes = new byte[BUFFER_SIZE];
            fixed (byte* ptr = bytes)
            {
                ThrowIfError(get_module_info(index, ModuleInfoType.ProductName, ptr, null), "获取设备产品名称");
                return ReadUtf8String(bytes);
            }
        }

        /// <summary>
        /// 获取设备ID
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe string GetModuleInfoDeviceId(int index)
        {
            byte[] bytes = new byte[BUFFER_SIZE];
            fixed (byte* ptr = bytes)
            {
                ThrowIfError(get_module_info(index, ModuleInfoType.DeviceId, ptr, null), "获取设备ID");
                return ReadUtf8String(bytes);
            }
        }

        /// <summary>
        /// 获取通道数
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe int GetModuleInfoChannelCount(int index)
        {
            int channelCount = 0;
            ThrowIfError(get_module_info(index, ModuleInfoType.ChannelCount, &channelCount, null), "获取通道数");
            return channelCount;
        }

        /// <summary>
        /// 获取采样率选项
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe List<double> GetModuleInfoSampleRateOptions(int index)
        {
            double[] buffer = new double[BUFFER_SIZE];
            int length = 0;
            fixed (double* ptr = buffer)
            {
                ThrowIfError(get_module_info(index, ModuleInfoType.SampleRateOptions, ptr, &length), "获取采样率选项");
                return new List<double>(buffer.Take(length));
            }
        }

        /// <summary>
        /// 获取电流选项
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe List<double> GetModuleInfoSampleCurrentOptions(int index)
        {
            double[] buffer = new double[BUFFER_SIZE];
            int length = 0;
            fixed (double* ptr = buffer)
            {
                ThrowIfError(get_module_info(index, ModuleInfoType.CurrentOptions, ptr, &length), "获取电流选项");
                return new List<double>(buffer.Take(length));
            }
        }

        /// <summary>
        /// 获取耦合模式选项
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static unsafe List<CouplingMode> GetModuleInfoSampleCouplingOptions(int index)
        {
            int[] buffer = new int[ARRAY_BUFFER_SIZE];
            int length = 0;
            fixed (int* ptr = buffer)
            {
                ThrowIfError(get_module_info(index, ModuleInfoType.CouplingOptions, ptr, &length), "获取耦合模式选项");
                return buffer.Take(length).Select(i => (CouplingMode)i).ToList();
            }
        }

        #endregion

        #region ============ 设备连接 ============

        /// <summary>
        /// 连接设备
        /// </summary>
        /// <param name="moduleInfo">设备信息</param>
        /// <returns>已连接的设备对象</returns>
        /// <exception cref="ArgumentException">设备未找到</exception>
        /// <exception cref="Exception">连接失败</exception>
        public static BrcDevice Connect(ModuleInfo moduleInfo)
        {
            if (moduleInfo == null)
                throw new ArgumentNullException(nameof(moduleInfo));

            lock (_lockObj)
            {
                var index = _moduleInfos.FindIndex((m) => m.DeviceId == moduleInfo.DeviceId);

                // ✅ 修复1: 检查索引有效性
                if (index < 0)
                    throw new ArgumentException($"设备 {moduleInfo.DeviceId} 未找到或已被连接");

                int handle = connect_module(index);

                // ✅ 修复2: 检查连接结果
                if (handle < 0)
                {
                    var error = GetLastError();
                    throw new Exception($"设备 {moduleInfo.DeviceId} 连接失败: {error}");
                }

                var brcDevice = new BrcDevice(handle, moduleInfo);

                // 从列表中移除已连接的设备
                _moduleInfos.RemoveAt(index);

                return brcDevice;
            }
        }

        #endregion

        #region ============ 数据结构 ============

        /// <summary>
        /// 模块信息
        /// </summary>
        public class ModuleInfo
        {
            /// <summary>
            /// 设备ID
            /// </summary>
            public string DeviceId { get; set; }

            /// <summary>
            /// 产品名称
            /// </summary>
            public string ProductName { get; set; }

            /// <summary>
            /// 通道数
            /// </summary>
            public int ChannelCount { get; set; }

            /// <summary>
            /// 增益选项
            /// </summary>
            public List<double> GainOptions { get; set; } = new List<double>();

            /// <summary>
            /// 采样率选项
            /// </summary>
            public List<double> SampleRateOptions { get; set; } = new List<double>();

            /// <summary>
            /// 电流选项
            /// </summary>
            public List<double> CurrentOptions { get; set; } = new List<double>();

            /// <summary>
            /// 耦合模式选项
            /// </summary>
            public List<CouplingMode> CouplingOptions { get; set; } = new List<CouplingMode>();
        }

        /// <summary>
        /// 数据采集卡设备对象
        /// </summary>
        public class BrcDevice : IDisposable
        {
            private readonly int _mHandle;
            private readonly ModuleInfo _moduleInfo;
            private bool _disposed = false;
            private readonly object _channelLock = new object();

            public BrcDevice(int mHandle, ModuleInfo moduleInfo)
            {
                _mHandle = mHandle;
                _moduleInfo = moduleInfo ?? throw new ArgumentNullException(nameof(moduleInfo));
            }

            /// <summary>
            /// 获取模块信息
            /// </summary>
            public ModuleInfo ModuleInfo => _moduleInfo;

            #region ============ 模块属性 (获取/设置) ============

            /// <summary>
            /// 获取时钟源
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe SourceType GetModulePropertyClockSource()
            {
                ThrowIfDisposed();

                int clockSource = 0;
                ThrowIfError(get_module_property(_mHandle, ModulePropertyType.ClockSource, &clockSource, null), "获取时钟源");
                return (SourceType)clockSource;
            }

            /// <summary>
            /// 获取触发源
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe SourceType GetModulePropertyTrigerSource()
            {
                ThrowIfDisposed();

                int trigerSource = 0;
                ThrowIfError(get_module_property(_mHandle, ModulePropertyType.TrigerSource, &trigerSource, null), "获取触发源");
                return (SourceType)trigerSource;
            }

            /// <summary>
            /// 获取采样率
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe double GetModulePropertySampleRate()
            {
                ThrowIfDisposed();

                double sampleRate = 0;
                ThrowIfError(get_module_property(_mHandle, ModulePropertyType.SampleRate, &sampleRate, null), "获取采样率");
                return sampleRate;
            }

            /// <summary>
            /// 设置时钟源
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetModulePropertyClockSource(SourceType sourceType)
            {
                ThrowIfDisposed();

                int source = (int)sourceType;
                ThrowIfError(set_module_property(_mHandle, ModulePropertyType.ClockSource, &source, null), "设置时钟源");
            }

            /// <summary>
            /// 设置触发源
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetModulePropertyTrigerSource(SourceType sourceType)
            {
                ThrowIfDisposed();

                int source = (int)sourceType;
                ThrowIfError(set_module_property(_mHandle, ModulePropertyType.TrigerSource, &source, null), "设置触发源");
            }

            /// <summary>
            /// 设置采样率
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetModulePropertySampleRate(double sampleRate)
            {
                ThrowIfDisposed();

                ThrowIfError(set_module_property(_mHandle, ModulePropertyType.SampleRate, &sampleRate, null), "设置采样率");
            }

            #endregion

            #region ============ 通道属性 (获取/设置) ============

            /// <summary>
            /// 获取通道是否启用
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe bool GetChannelPropertyEnabled(int channelIndex)
            {
                ThrowIfDisposed();

                byte enabled = 0;
                ThrowIfError(get_channel_property(_mHandle, channelIndex, ChannelPropertyType.Enabled, &enabled, null), 
                    $"获取通道{channelIndex}状态");
                return enabled != 0;
            }

            /// <summary>
            /// 获取通道增益
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe double GetChannelPropertyGain(int channelIndex)
            {
                ThrowIfDisposed();

                double gain = 0;
                ThrowIfError(get_channel_property(_mHandle, channelIndex, ChannelPropertyType.Gain, &gain, null), 
                    $"获取通道{channelIndex}增益");
                return gain;
            }

            /// <summary>
            /// 获取通道电流
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe double GetChannelPropertyCurrent(int channelIndex)
            {
                ThrowIfDisposed();

                double current = 0;
                ThrowIfError(get_channel_property(_mHandle, channelIndex, ChannelPropertyType.Current, &current, null), 
                    $"获取通道{channelIndex}电流");
                return current;
            }

            /// <summary>
            /// 获取通道耦合模式
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe CouplingMode GetChannelPropertyCouplingMode(int channelIndex)
            {
                ThrowIfDisposed();

                int couplingMode = 0;
                ThrowIfError(get_channel_property(_mHandle, channelIndex, ChannelPropertyType.CouplingMode, &couplingMode, null), 
                    $"获取通道{channelIndex}耦合模式");
                return (CouplingMode)couplingMode;
            }

            /// <summary>
            /// 设置通道是否启用
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetChannelPropertyEnabled(int channelIndex, bool enabled)
            {
                ThrowIfDisposed();

                byte value = (byte)(enabled ? 1 : 0);
                ThrowIfError(set_channel_property(_mHandle, channelIndex, ChannelPropertyType.Enabled, &value, null), 
                    $"设置通道{channelIndex}状态");
            }

            /// <summary>
            /// 设置通道增益
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetChannelPropertyGain(int channelIndex, double gain)
            {
                ThrowIfDisposed();

                ThrowIfError(set_channel_property(_mHandle, channelIndex, ChannelPropertyType.Gain, &gain, null), 
                    $"设置通道{channelIndex}增益");
            }

            /// <summary>
            /// 设置通道电流
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetChannelPropertyCurrent(int channelIndex, double current)
            {
                ThrowIfDisposed();

                ThrowIfError(set_channel_property(_mHandle, channelIndex, ChannelPropertyType.Current, &current, null), 
                    $"设置通道{channelIndex}电流");
            }

            /// <summary>
            /// 设置通道耦合模式
            /// </summary>
            [HandleProcessCorruptedStateExceptions]
            public unsafe void SetChannelPropertyCouplingMode(int channelIndex, CouplingMode couplingMode)
            {
                ThrowIfDisposed();

                int value = (int)couplingMode;
                ThrowIfError(set_channel_property(_mHandle, channelIndex, ChannelPropertyType.CouplingMode, &value, null), 
                    $"设置通道{channelIndex}耦合模式");
            }

            #endregion

            #region ============ 数据采集 ============

            /// <summary>
            /// 断开设备连接
            /// </summary>
            public void Disconnect()
            {
                if (_disposed)
                    return;

                lock (_lockObj)
                {
                    if (disconnect_module(_mHandle) < 0)
                    {
                        string error = GetLastError();
                        throw new Exception($"断开设备连接失败: {error}");
                    }
                }
            }

            /// <summary>
            /// 开始采集
            /// </summary>
            public void Start()
            {
                ThrowIfDisposed();

                ThrowIfError(start(_mHandle, false), "开始采集");
            }

            /// <summary>
            /// 停止采集
            /// </summary>
            public void Stop()
            {
                ThrowIfDisposed();

                ThrowIfError(stop(_mHandle), "停止采集");
            }

            /// <summary>
            /// 获取通道数据
            /// </summary>
            /// <param name="array">数据缓冲区，长度必须是通道数的整数倍</param>
            /// <param name="timeout">超时时间</param>
            /// <exception cref="ArgumentException">数组长度不符合要求</exception>
            /// <exception cref="InvalidOperationException">数组长度不符合要求</exception>
            public unsafe void GetChannelsData(Memory<double> array, TimeSpan timeout)
            {
                ThrowIfDisposed();

                if (array.Length <= 0)
                    throw new ArgumentException("数组长度必须大于0", nameof(array));

                if (array.Length % _moduleInfo.ChannelCount != 0)
                    throw new InvalidOperationException($"数组长度必须是通道数({_moduleInfo.ChannelCount})的整数倍");

                lock (_channelLock)
                {
                    // 防止超时溢出，并计算正确的样本数
                    int timeoutMs = (int)Math.Min(timeout.TotalMilliseconds, int.MaxValue);
                    if (timeoutMs < 0) timeoutMs = int.MaxValue;

                    int samplesPerChannel = array.Length / _moduleInfo.ChannelCount;

                    using var handle = array.Pin();
                    // 第4个参数应为总数据长度（数组大小）
                    ThrowIfError(get_channels_data(_mHandle, (double*)handle.Pointer, samplesPerChannel, array.Length, timeoutMs), 
                        "获取通道数据");
                }
            }

            #endregion

            #region ============ IDisposable 实现 ============

            /// <summary>
            /// 检查对象是否已释放
            /// </summary>
            /// <exception cref="ObjectDisposedException">对象已释放</exception>
            private void ThrowIfDisposed()
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().Name);
            }

            /// <summary>
            /// 析构函数
            /// </summary>
            ~BrcDevice()
            {
                Dispose(false);
            }

            /// <summary>
            /// 释放资源
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// 释放资源（受保护方法）
            /// </summary>
            /// <param name="disposing">是否由Dispose方法调用</param>
            protected virtual void Dispose(bool disposing)
            {
                if (_disposed)
                    return;

                try
                {
                    Disconnect();
                }
                catch when (!disposing)
                {
                    // 析构函数中忽略异常，避免二次异常
                }
                finally
                {
                    _disposed = true;
                }
            }

            #endregion
        }

        #endregion
    }
}

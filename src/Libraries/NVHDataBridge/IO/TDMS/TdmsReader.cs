using NationalInstruments.Tdms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using File = NationalInstruments.Tdms.File;

namespace NVHDataBridge.IO.TDMS
{
    /// <summary>
    /// TDMS文件读取器 - 提供简洁友好的API来读取National Instruments TDMS文件
    /// </summary>
    /// <example>
    /// 基本使用:
    /// <code>
    /// using (var reader = TdmsReader.Open("data.tdms"))
    /// {
    ///     var data = reader.GetChannelData&lt;double&gt;("Group1", "Channel1");
    ///     Console.WriteLine($"读取了 {data.Length} 个数据点");
    /// }
    /// </code>
    /// </example>
    public class TdmsReader : IDisposable
    {
        #region 私有字段

        private readonly File _tdmsFile;
        private bool _isDisposed;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从文件路径创建TDMS读取器
        /// </summary>
        /// <param name="filePath">TDMS文件的完整路径</param>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        private TdmsReader(string filePath)
        {
            ValidateFilePath(filePath);
            _tdmsFile = new File(filePath);
        }

        /// <summary>
        /// 从流创建TDMS读取器
        /// </summary>
        /// <param name="stream">包含TDMS数据的流</param>
        /// <exception cref="ArgumentNullException">流为空</exception>
        /// <exception cref="ArgumentException">流不可读或不可定位</exception>
        private TdmsReader(Stream stream)
        {
            ValidateStream(stream);
            _tdmsFile = new File(stream);
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取底层的TDMS文件对象（用于高级操作）
        /// </summary>
        public File UnderlyingFile => _tdmsFile;

        /// <summary>
        /// 获取文件级别的属性字典
        /// </summary>
        public IDictionary<string, object> FileProperties => _tdmsFile.Properties;

        /// <summary>
        /// 获取文件中所有组的集合
        /// </summary>
        public IEnumerable<Group> Groups => _tdmsFile.Groups.Values;

        /// <summary>
        /// 获取文件中的组数量
        /// </summary>
        public int GroupCount => _tdmsFile.Groups.Count;

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 打开TDMS文件并返回读取器实例
        /// </summary>
        /// <param name="filePath">TDMS文件路径</param>
        /// <returns>已打开的TdmsReader实例</returns>
        public static TdmsReader Open(string filePath)
        {
            var reader = new TdmsReader(filePath);
            reader._tdmsFile.Open();
            return reader;
        }

        /// <summary>
        /// 从流打开TDMS文件并返回读取器实例
        /// </summary>
        /// <param name="stream">包含TDMS数据的流</param>
        /// <returns>已打开的TdmsReader实例</returns>
        public static TdmsReader Open(Stream stream)
        {
            var reader = new TdmsReader(stream);
            reader._tdmsFile.Open();
            return reader;
        }

        /// <summary>
        /// 创建TDMS读取器但不立即打开（延迟加载）
        /// </summary>
        /// <param name="filePath">TDMS文件路径</param>
        /// <returns>未打开的TdmsReader实例</returns>
        public static TdmsReader Create(string filePath)
        {
            return new TdmsReader(filePath);
        }

        /// <summary>
        /// 从流创建TDMS读取器但不立即打开（延迟加载）
        /// </summary>
        /// <param name="stream">包含TDMS数据的流</param>
        /// <returns>未打开的TdmsReader实例</returns>
        public static TdmsReader Create(Stream stream)
        {
            return new TdmsReader(stream);
        }

        #endregion

        #region 组相关操作

        /// <summary>
        /// 获取指定名称的组
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <returns>找到的组，如果不存在返回null</returns>
        public Group GetGroup(string groupName)
        {
            ValidateGroupName(groupName);

            _tdmsFile.Groups.TryGetValue(groupName, out var group);
            return group;
        }

        /// <summary>
        /// 尝试获取指定名称的组
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <param name="group">输出参数：找到的组</param>
        /// <returns>如果找到返回true，否则返回false</returns>
        public bool TryGetGroup(string groupName, out Group group)
        {
            ValidateGroupName(groupName);
            return _tdmsFile.Groups.TryGetValue(groupName, out group);
        }

        /// <summary>
        /// 检查是否存在指定名称的组
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <returns>存在返回true，否则返回false</returns>
        public bool ContainsGroup(string groupName)
        {
            ValidateGroupName(groupName);
            return _tdmsFile.Groups.ContainsKey(groupName);
        }

        /// <summary>
        /// 获取所有组的名称
        /// </summary>
        /// <returns>组名称的枚举集合</returns>
        public IEnumerable<string> GetGroupNames()
        {
            return _tdmsFile.Groups.Keys;
        }

        #endregion

        #region 通道相关操作

        /// <summary>
        /// 获取指定组中的指定通道
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <param name="channelName">通道名称</param>
        /// <returns>找到的通道，如果不存在返回null</returns>
        public Channel GetChannel(string groupName, string channelName)
        {
            ValidateGroupName(groupName);
            ValidateChannelName(channelName);

            var group = GetGroup(groupName);
            if (group == null)
                return null;

            group.Channels.TryGetValue(channelName, out var channel);
            return channel;
        }

        /// <summary>
        /// 尝试获取指定组中的指定通道
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <param name="channelName">通道名称</param>
        /// <param name="channel">输出参数：找到的通道</param>
        /// <returns>如果找到返回true，否则返回false</returns>
        public bool TryGetChannel(string groupName, string channelName, out Channel channel)
        {
            ValidateGroupName(groupName);
            ValidateChannelName(channelName);

            channel = null;
            var group = GetGroup(groupName);
            return group != null && group.Channels.TryGetValue(channelName, out channel);
        }

        /// <summary>
        /// 检查是否存在指定的通道
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <param name="channelName">通道名称</param>
        /// <returns>存在返回true，否则返回false</returns>
        public bool ContainsChannel(string groupName, string channelName)
        {
            ValidateGroupName(groupName);
            ValidateChannelName(channelName);

            var group = GetGroup(groupName);
            return group != null && group.Channels.ContainsKey(channelName);
        }

        /// <summary>
        /// 获取指定组中所有通道的名称
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <returns>通道名称的枚举集合</returns>
        public IEnumerable<string> GetChannelNames(string groupName)
        {
            var group = GetGroup(groupName);
            return group?.Channels.Keys ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// 获取指定组中的所有通道
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <returns>通道的枚举集合</returns>
        public IEnumerable<Channel> GetChannels(string groupName)
        {
            var group = GetGroup(groupName);
            return group?.Channels.Values ?? Enumerable.Empty<Channel>();
        }

        #endregion

        #region 数据读取操作

        /// <summary>
        /// 读取指定通道的数据并转换为指定类型
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="groupName">组名称</param>
        /// <param name="channelName">通道名称</param>
        /// <returns>数据数组</returns>
        /// <exception cref="ChannelNotFoundException">通道不存在</exception>
        /// <exception cref="InvalidCastException">类型转换失败</exception>
        public T[] GetChannelData<T>(string groupName, string channelName)
        {
            var channel = GetChannel(groupName, channelName);

            if (channel == null)
                throw new ChannelNotFoundException(groupName, channelName);

            if (!channel.HasData)
                return new T[0];

            return channel.GetData<T>().ToArray();
        }

        /// <summary>
        /// 尝试读取指定通道的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="groupName">组名称</param>
        /// <param name="channelName">通道名称</param>
        /// <param name="data">输出参数：读取的数据数组</param>
        /// <returns>读取成功返回true，否则返回false</returns>
        public bool TryGetChannelData<T>(string groupName, string channelName, out T[] data)
        {
            data = null;

            try
            {
                if (!TryGetChannel(groupName, channelName, out var channel))
                    return false;

                if (!channel.HasData)
                {
                    data = new T[0];
                    return true;
                }

                data = channel.GetData<T>().ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读取指定通道的数据（延迟执行）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="groupName">组名称</param>
        /// <param name="channelName">通道名称</param>
        /// <returns>数据的可枚举集合</returns>
        public IEnumerable<T> StreamChannelData<T>(string groupName, string channelName)
        {
            var channel = GetChannel(groupName, channelName);

            if (channel == null)
                throw new ChannelNotFoundException(groupName, channelName);

            return channel.HasData ? channel.GetData<T>() : Enumerable.Empty<T>();
        }

        #endregion

        #region 文件信息和摘要

        /// <summary>
        /// 获取TDMS文件的结构摘要信息
        /// </summary>
        /// <returns>文件摘要对象</returns>
        public TdmsFileSummary GetSummary()
        {
            return new TdmsFileSummary
            {
                GroupCount = _tdmsFile.Groups.Count,
                TotalChannelCount = CalculateTotalChannelCount(),
                TotalDataPoints = CalculateTotalDataPoints(),
                FilePropertyCount = _tdmsFile.Properties.Count,
                Groups = BuildGroupSummaries()
            };
        }

        /// <summary>
        /// 获取指定属性的值
        /// </summary>
        /// <typeparam name="T">属性值类型</typeparam>
        /// <param name="propertyName">属性名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>属性值或默认值</returns>
        public T GetFileProperty<T>(string propertyName, T defaultValue = default(T))
        {
            if (string.IsNullOrEmpty(propertyName))
                return defaultValue;

            if (_tdmsFile.Properties.TryGetValue(propertyName, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        #endregion

        #region 文件操作

        /// <summary>
        /// 重写TDMS文件以整理碎片和优化存储
        /// </summary>
        /// <param name="outputPath">输出文件路径</param>
        public void RewriteToFile(string outputPath)
        {
            ValidateFilePath(outputPath);
            _tdmsFile.ReWrite(outputPath);
        }

        /// <summary>
        /// 重写TDMS文件到流
        /// </summary>
        /// <param name="outputStream">输出流</param>
        public void RewriteToStream(Stream outputStream)
        {
            ValidateOutputStream(outputStream);
            _tdmsFile.ReWrite(outputStream);
        }

        #endregion

        #region 验证方法

        private void ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");

            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException($"TDMS文件不存在: {filePath}", filePath);
        }

        private void ValidateStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "流不能为空");

            if (!stream.CanRead)
                throw new ArgumentException("流必须可读", nameof(stream));

            if (!stream.CanSeek)
                throw new ArgumentException("流必须可定位", nameof(stream));
        }

        private void ValidateOutputStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "输出流不能为空");

            if (!stream.CanWrite)
                throw new ArgumentException("流必须可写", nameof(stream));
        }

        private void ValidateGroupName(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentNullException(nameof(groupName), "组名称不能为空");
        }

        private void ValidateChannelName(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentNullException(nameof(channelName), "通道名称不能为空");
        }

        #endregion

        #region 私有辅助方法

        private int CalculateTotalChannelCount()
        {
            return _tdmsFile.Groups.Values.Sum(group => group.Channels.Count);
        }

        private long CalculateTotalDataPoints()
        {
            return _tdmsFile.Groups.Values
                .SelectMany(group => group.Channels.Values)
                .Sum(channel => channel.DataCount);
        }

        private List<GroupSummary> BuildGroupSummaries()
        {
            return _tdmsFile.Groups.Values.Select(group => new GroupSummary
            {
                Name = group.Name,
                ChannelCount = group.Channels.Count,
                PropertyCount = group.Properties.Count,
                Channels = BuildChannelSummaries(group)
            }).ToList();
        }

        private List<ChannelSummary> BuildChannelSummaries(Group group)
        {
            return group.Channels.Values.Select(channel => new ChannelSummary
            {
                Name = channel.Name,
                DataType = channel.DataType?.Name ?? "Unknown",
                DataCount = channel.DataCount,
                HasData = channel.HasData,
                PropertyCount = channel.Properties.Count
            }).ToList();
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的内部实现
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                _tdmsFile?.Dispose();
            }

            _isDisposed = true;
        }

        #endregion
    }

    #region 摘要信息类

    /// <summary>
    /// TDMS文件的摘要信息
    /// </summary>
    public sealed class TdmsFileSummary
    {
        /// <summary>组的数量</summary>
        public int GroupCount { get; set; }

        /// <summary>通道的总数量</summary>
        public int TotalChannelCount { get; set; }

        /// <summary>数据点的总数量</summary>
        public long TotalDataPoints { get; set; }

        /// <summary>文件级别属性的数量</summary>
        public int FilePropertyCount { get; set; }

        /// <summary>所有组的摘要信息</summary>
        public List<GroupSummary> Groups { get; set; }

        public override string ToString()
        {
            return $"TDMS文件摘要: {GroupCount} 个组, {TotalChannelCount} 个通道, {TotalDataPoints:N0} 个数据点";
        }
    }

    /// <summary>
    /// 组的摘要信息
    /// </summary>
    public sealed class GroupSummary
    {
        /// <summary>组名称</summary>
        public string Name { get; set; }

        /// <summary>通道数量</summary>
        public int ChannelCount { get; set; }

        /// <summary>属性数量</summary>
        public int PropertyCount { get; set; }

        /// <summary>所有通道的摘要信息</summary>
        public List<ChannelSummary> Channels { get; set; }

        public override string ToString()
        {
            return $"组 '{Name}': {ChannelCount} 个通道, {PropertyCount} 个属性";
        }
    }

    /// <summary>
    /// 通道的摘要信息
    /// </summary>
    public sealed class ChannelSummary
    {
        /// <summary>通道名称</summary>
        public string Name { get; set; }

        /// <summary>数据类型</summary>
        public string DataType { get; set; }

        /// <summary>数据点数量</summary>
        public long DataCount { get; set; }

        /// <summary>是否包含数据</summary>
        public bool HasData { get; set; }

        /// <summary>属性数量</summary>
        public int PropertyCount { get; set; }

        public override string ToString()
        {
            return $"通道 '{Name}' ({DataType}): {DataCount:N0} 个数据点, {PropertyCount} 个属性";
        }
    }

    #endregion

    #region 自定义异常

    /// <summary>
    /// 通道未找到异常
    /// </summary>
    public sealed class ChannelNotFoundException : Exception
    {
        public string GroupName { get; }
        public string ChannelName { get; }

        public ChannelNotFoundException(string groupName, string channelName)
            : base($"通道未找到: {groupName}/{channelName}")
        {
            GroupName = groupName;
            ChannelName = channelName;
        }
    }

    #endregion
}


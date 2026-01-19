using NVHDataBridge.Models;
using NVHDataBridge.IO.TDMS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NationalInstruments.Tdms;

namespace NVHDataBridge.Converters
{
    /// <summary>
    /// NvhMemoryFile 与 TDMS 文件格式转换器
    /// 提供双向转换功能：保存为 TDMS 和从 TDMS 加载
    /// </summary>
    public static class NvhTdmsConverter
    {
        #region 保存为 TDMS

        /// <summary>
        /// 将 NvhMemoryFile 保存为 TDMS 文件
        /// </summary>
        /// <param name="nvhFile">要保存的 NvhMemoryFile 实例</param>
        /// <param name="filePath">TDMS 文件路径</param>
        /// <param name="options">转换选项</param>
        /// <exception cref="ArgumentNullException">nvhFile 为空</exception>
        /// <exception cref="ArgumentException">文件路径为空</exception>
        public static void SaveToTdms(NvhMemoryFile nvhFile, string filePath, TdmsConversionOptions? options = null)
        {
            if (nvhFile == null)
                throw new ArgumentNullException(nameof(nvhFile));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            options ??= new TdmsConversionOptions();

            using (var writer = new TdmsWriter(filePath, 
                batchThreshold: options.BatchThreshold, 
                maxSegmentSize: options.MaxSegmentSize,
                flushMode: options.FlushMode))
            {
                // 1. 设置文件级别属性
                SetFileProperties(writer, nvhFile, options);

                // 2. 遍历所有组
                foreach (var group in nvhFile.Groups.Values)
                {
                    // 3. 设置组级别属性
                    SetGroupProperties(writer, group, options);

                    // 4. 遍历组内所有通道
                    foreach (var channel in group.Channels.Values)
                    {
                        // 5. 写入通道数据和属性
                        WriteChannelData(writer, group.Name, channel, options);
                    }
                }

                // 6. 确保所有数据已刷新
                writer.ForceFlush();
            }
        }

        /// <summary>
        /// 设置文件级别属性
        /// </summary>
        private static void SetFileProperties(TdmsWriter writer, NvhMemoryFile nvhFile, TdmsConversionOptions options)
        {
            // 从 PropertyBag 获取标准属性
            string name = nvhFile.Properties.Get<string>("name", options.DefaultFileName ?? "NvhMemoryFile");
            string author = nvhFile.Properties.Get<string>("author", options.DefaultAuthor ?? string.Empty);
            string description = nvhFile.Properties.Get<string>("description", string.Empty);
            DateTime? datetime = nvhFile.Properties.Get<DateTime?>("datetime");

            // 设置标准根属性
            writer.SetRootProperties(name, author, description, datetime);

            // 设置自定义属性
            foreach (var entry in nvhFile.Properties.Entries)
            {
                // 跳过已设置的标准属性
                if (entry.Key.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals("author", StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals("datetime", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 通过反射设置自定义属性（TDMS Writer 的限制）
                // 注意：TdmsWriter 目前只支持链式设置标准属性，自定义属性需要在 WriteSegment 中处理
                // 这里我们暂时跳过，或者可以通过底层 API 设置
            }
        }

        /// <summary>
        /// 设置组级别属性
        /// </summary>
        private static void SetGroupProperties(TdmsWriter writer, NvhMemoryGroup group, TdmsConversionOptions options)
        {
            string description = group.Properties.Get<string>("description", string.Empty);
            var customProperties = new Dictionary<string, object>();

            // 收集自定义属性
            foreach (var entry in group.Properties.Entries)
            {
                if (!entry.Key.Equals("description", StringComparison.OrdinalIgnoreCase))
                {
                    customProperties[entry.Key] = entry.Value;
                }
            }

            writer.SetGroupProperties(group.Name, description, customProperties.Count > 0 ? customProperties : null);
        }

        /// <summary>
        /// 写入通道数据和属性
        /// </summary>
        private static void WriteChannelData(TdmsWriter writer, string groupName, NvhMemoryChannelBase channel, TdmsConversionOptions options)
        {
            // 获取通道的数据类型
            Type dataType = channel.DataType;

            // 使用反射调用泛型方法
            MethodInfo writeMethod = typeof(NvhTdmsConverter).GetMethod(
                nameof(WriteChannelDataGeneric),
                BindingFlags.NonPublic | BindingFlags.Static);

            if (writeMethod == null)
                throw new InvalidOperationException("无法找到 WriteChannelDataGeneric 方法");

            // 创建泛型方法
            MethodInfo genericMethod = writeMethod.MakeGenericMethod(dataType);

            // 调用泛型方法
            genericMethod.Invoke(null, new object[] { writer, groupName, channel, options });
        }

        /// <summary>
        /// 写入通道数据（泛型版本）
        /// </summary>
        private static void WriteChannelDataGeneric<T>(TdmsWriter writer, string groupName, NvhMemoryChannelBase channel, TdmsConversionOptions options)
            where T : unmanaged
        {
            // 转换为具体类型的通道
            if (!(channel is NvhMemoryChannel<T> typedChannel))
            {
                throw new InvalidOperationException($"通道类型不匹配: 期望 {typeof(T)}, 实际 {channel.DataType}");
            }

            // 1. 设置通道属性
            SetChannelProperties(writer, groupName, typedChannel, options);

            // 2. 读取所有数据
            ReadOnlySpan<T> allData = typedChannel.ReadAll();

            if (allData.IsEmpty)
            {
                // 即使没有数据，也要创建通道元数据
                writer.SetChannelProperties<T>(groupName, channel.Name);
                return;
            }

            // 3. 批量写入数据
            // 将 ReadOnlySpan 转换为数组（因为 TdmsWriter 需要数组）
            T[] dataArray;
            if (allData.Length > 0)
            {
                dataArray = new T[allData.Length];
                // 使用 Span 的 CopyTo 方法进行高效复制
                allData.CopyTo(new Span<T>(dataArray));
            }
            else
            {
                dataArray = Array.Empty<T>();
            }

            // 根据数据量选择写入策略
            if (dataArray.Length > options.LargeDataThreshold)
            {
                // 大数据量：使用流式写入
                writer.WriteLargeData(groupName, channel.Name, dataArray);
            }
            else
            {
                // 中等数据量：批量写入
                writer.WriteBatch(groupName, channel.Name, dataArray);
            }
        }

        /// <summary>
        /// 设置通道属性
        /// </summary>
        private static void SetChannelProperties<T>(TdmsWriter writer, string groupName, NvhMemoryChannel<T> channel, TdmsConversionOptions options)
            where T : unmanaged
        {
            var config = new ChannelConfig();

            // 从 PropertyBag 获取标准波形属性
            config.Description = channel.Properties.Get<string>("description", 
                channel.Properties.Get<string>("wf_description", string.Empty));
            config.YUnitString = channel.Properties.Get<string>("wf_yunit", string.Empty);
            config.XUnitString = channel.Properties.Get<string>("wf_xunit", string.Empty);
            config.XName = channel.Properties.Get<string>("wf_xname", string.Empty);
            config.StartTime = channel.WfStartTime ?? channel.Properties.Get<DateTime?>("wf_start_time");
            config.Increment = channel.WfIncrement ?? channel.Properties.Get<double?>("wf_increment") ?? 0.0;

            // 收集自定义属性
            foreach (var entry in channel.Properties.Entries)
            {
                // 跳过标准属性
                if (IsStandardProperty(entry.Key))
                    continue;

                if (config.CustomProperties == null)
                    config.CustomProperties = new Dictionary<string, object>();

                config.CustomProperties[entry.Key] = entry.Value;
            }

            // 设置通道属性
            writer.SetChannelProperties<T>(groupName, channel.Name, config);
        }

        /// <summary>
        /// 检查是否为标准属性
        /// </summary>
        private static bool IsStandardProperty(string key)
        {
            string lowerKey = key.ToLowerInvariant();
            return lowerKey == "description" ||
                   lowerKey == "wf_description" ||
                   lowerKey == "wf_yunit" ||
                   lowerKey == "wf_xunit" ||
                   lowerKey == "wf_xname" ||
                   lowerKey == "wf_start_time" ||
                   lowerKey == "wf_start_offset" ||
                   lowerKey == "wf_increment" ||
                   lowerKey == "wf_samples";
        }

        #endregion

        #region 从 TDMS 加载

        /// <summary>
        /// 从 TDMS 文件加载并转换为 NvhMemoryFile
        /// </summary>
        /// <param name="filePath">TDMS 文件路径</param>
        /// <param name="options">转换选项</param>
        /// <returns>转换后的 NvhMemoryFile 实例</returns>
        /// <exception cref="ArgumentException">文件路径为空或文件不存在</exception>
        public static NvhMemoryFile LoadFromTdms(string filePath, TdmsConversionOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException($"TDMS文件不存在: {filePath}", filePath);

            options ??= new TdmsConversionOptions();

            var nvhFile = new NvhMemoryFile();

            using (var reader = TdmsReader.Open(filePath))
            {
                // 1. 加载文件级别属性
                LoadFileProperties(reader, nvhFile, options);

                // 2. 遍历所有组
                foreach (var group in reader.Groups)
                {
                    // 3. 获取或创建组
                    var nvhGroup = nvhFile.GetOrCreateGroup(group.Name);

                    // 4. 加载组级别属性
                    LoadGroupProperties(group, nvhGroup, options);

                    // 5. 遍历组内所有通道
                    foreach (var channel in group.Channels.Values)
                    {
                        // 6. 加载通道数据和属性
                        LoadChannelData(nvhGroup, channel, options);
                    }
                }
            }

            return nvhFile;
        }

        /// <summary>
        /// 加载文件级别属性
        /// </summary>
        private static void LoadFileProperties(TdmsReader reader, NvhMemoryFile nvhFile, TdmsConversionOptions options)
        {
            // 加载标准属性
            string name = reader.GetFileProperty<string>("name", string.Empty);
            string author = reader.GetFileProperty<string>("author", string.Empty);
            string description = reader.GetFileProperty<string>("description", string.Empty);
            DateTime? datetime = reader.GetFileProperty<DateTime?>("datetime");

            if (!string.IsNullOrEmpty(name))
                nvhFile.Properties.Set("name", name);
            if (!string.IsNullOrEmpty(author))
                nvhFile.Properties.Set("author", author);
            if (!string.IsNullOrEmpty(description))
                nvhFile.Properties.Set("description", description);
            if (datetime.HasValue)
                nvhFile.Properties.Set("datetime", datetime.Value);

            // 加载所有其他属性
            foreach (var prop in reader.FileProperties)
            {
                if (!IsStandardFileProperty(prop.Key))
                {
                    nvhFile.Properties.Set(prop.Key, prop.Value);
                }
            }
        }

        /// <summary>
        /// 检查是否为标准文件属性
        /// </summary>
        private static bool IsStandardFileProperty(string key)
        {
            string lowerKey = key.ToLowerInvariant();
            return lowerKey == "name" ||
                   lowerKey == "author" ||
                   lowerKey == "description" ||
                   lowerKey == "datetime";
        }

        /// <summary>
        /// 加载组级别属性
        /// </summary>
        private static void LoadGroupProperties(Group group, NvhMemoryGroup nvhGroup, TdmsConversionOptions options)
        {
            // 加载标准属性
            if (group.Properties.TryGetValue("description", out var description))
            {
                nvhGroup.Properties.Set("description", description);
            }

            // 加载所有其他属性
            foreach (var prop in group.Properties)
            {
                if (!prop.Key.Equals("description", StringComparison.OrdinalIgnoreCase))
                {
                    nvhGroup.Properties.Set(prop.Key, prop.Value);
                }
            }
        }

        /// <summary>
        /// 加载通道数据和属性
        /// </summary>
        private static void LoadChannelData(NvhMemoryGroup nvhGroup, Channel channel, TdmsConversionOptions options)
        {
            // 获取通道的数据类型
            Type dataType = channel.DataType;

            if (dataType == null)
            {
                // 如果无法确定类型，跳过此通道
                return;
            }

            // 检查是否为 unmanaged 类型（NvhMemoryChannel 只支持 unmanaged 类型）
            if (dataType == typeof(string) || !IsUnmanagedType(dataType))
            {
                // 字符串类型或其他非 unmanaged 类型不支持，跳过
                System.Diagnostics.Debug.WriteLine($"跳过非 unmanaged 类型的通道: {channel.Name}, 类型: {dataType.Name}");
                return;
            }

            // 使用反射调用泛型方法
            MethodInfo loadMethod = typeof(NvhTdmsConverter).GetMethod(
                nameof(LoadChannelDataGeneric),
                BindingFlags.NonPublic | BindingFlags.Static);

            if (loadMethod == null)
                throw new InvalidOperationException("无法找到 LoadChannelDataGeneric 方法");

            // 创建泛型方法
            MethodInfo genericMethod = loadMethod.MakeGenericMethod(dataType);

            // 调用泛型方法
            genericMethod.Invoke(null, new object[] { nvhGroup, channel, options });
        }

        /// <summary>
        /// 检查类型是否为 unmanaged 类型
        /// </summary>
        private static bool IsUnmanagedType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
                return true;

            if (type.IsValueType)
            {
                // 检查结构体的所有字段是否都是 unmanaged 类型
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!IsUnmanagedType(field.FieldType))
                        return false;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 加载通道数据（泛型版本）
        /// </summary>
        private static void LoadChannelDataGeneric<T>(NvhMemoryGroup nvhGroup, Channel channel, TdmsConversionOptions options)
            where T : unmanaged
        {
            // 1. 创建通道
            var nvhChannel = nvhGroup.CreateChannel<T>(channel.Name);

            // 2. 加载通道属性
            LoadChannelProperties(channel, nvhChannel, options);

            // 3. 读取通道数据
            if (channel.HasData)
            {
                try
                {
                    // 尝试读取数据
                    T[] data = channel.GetData<T>().ToArray();

                    if (data.Length > 0)
                    {
                        // 批量写入数据
                        nvhChannel.WriteSamples(data);
                    }
                }
                catch (Exception ex)
                {
                    // 如果读取失败，记录但不抛出异常（允许部分通道失败）
                    // 在实际应用中，可以考虑记录日志
                    System.Diagnostics.Debug.WriteLine($"加载通道数据失败: {channel.Name}, 错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 加载通道属性
        /// </summary>
        private static void LoadChannelProperties<T>(Channel channel, NvhMemoryChannel<T> nvhChannel, TdmsConversionOptions options)
            where T : unmanaged
        {
            // 加载标准波形属性
            if (channel.Properties.TryGetValue("description", out var description))
            {
                nvhChannel.Properties.Set("description", description);
                nvhChannel.Properties.Set("wf_description", description);
            }

            if (channel.Properties.TryGetValue("wf_yunit", out var yUnit))
            {
                nvhChannel.Properties.Set("wf_yunit", yUnit);
            }

            if (channel.Properties.TryGetValue("wf_xunit", out var xUnit))
            {
                nvhChannel.Properties.Set("wf_xunit", xUnit);
            }

            if (channel.Properties.TryGetValue("wf_xname", out var xName))
            {
                nvhChannel.Properties.Set("wf_xname", xName);
            }

            if (channel.Properties.TryGetValue("wf_start_time", out var startTime))
            {
                if (startTime is DateTime dt)
                {
                    nvhChannel.WfStartTime = dt;
                }
            }

            if (channel.Properties.TryGetValue("wf_start_offset", out var startOffset))
            {
                if (startOffset is double offset)
                {
                    nvhChannel.WfStartOffset = offset;
                }
            }

            if (channel.Properties.TryGetValue("wf_increment", out var increment))
            {
                if (increment is double inc)
                {
                    nvhChannel.WfIncrement = inc;
                }
            }

            if (channel.Properties.TryGetValue("wf_samples", out var samples))
            {
                if (samples is long count)
                {
                    nvhChannel.WfSamples = count;
                }
            }

            // 加载所有其他自定义属性
            foreach (var prop in channel.Properties)
            {
                if (!IsStandardProperty(prop.Key))
                {
                    nvhChannel.Properties.Set(prop.Key, prop.Value);
                }
            }
        }

        #endregion
    }

    #region 转换选项

    /// <summary>
    /// TDMS 转换选项
    /// </summary>
    public class TdmsConversionOptions
    {
        /// <summary>
        /// 批量写入阈值（默认 10000）
        /// </summary>
        public int BatchThreshold { get; set; } = 10000;

        /// <summary>
        /// 最大段大小（默认 100MB）
        /// </summary>
        public long MaxSegmentSize { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// 刷新模式（默认 Auto）
        /// </summary>
        public FlushMode FlushMode { get; set; } = FlushMode.Auto;

        /// <summary>
        /// 大数据阈值（默认 50MB）
        /// </summary>
        public long LargeDataThreshold { get; set; } = 50 * 1024 * 1024;

        /// <summary>
        /// 默认文件名（用于保存时）
        /// </summary>
        public string? DefaultFileName { get; set; }

        /// <summary>
        /// 默认作者（用于保存时）
        /// </summary>
        public string? DefaultAuthor { get; set; }
    }

    #endregion
}


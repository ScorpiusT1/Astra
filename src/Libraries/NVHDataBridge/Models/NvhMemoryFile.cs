using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NVHDataBridge.Models
{
    public sealed class NvhMemoryFile
    {
        private readonly Dictionary<string, NvhMemoryGroup> _groups;

        public PropertyBag Properties { get; } = new PropertyBag(16);

        public IReadOnlyDictionary<string, NvhMemoryGroup> Groups => _groups;

        public NvhMemoryFile(int estimatedGroupCount = 8)
        {
            _groups = new Dictionary<string, NvhMemoryGroup>(estimatedGroupCount);
        }

        /// <summary>
        /// 获取或创建组
        /// </summary>
        /// <param name="name">组名称</param>
        /// <returns>组实例</returns>
        /// <exception cref="ArgumentException">名称为空</exception>
        public NvhMemoryGroup GetOrCreateGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Group name cannot be null or empty", nameof(name));

            if (!_groups.TryGetValue(name, out var group))
            {
                group = new NvhMemoryGroup(name);
                _groups.Add(name, group);
            }

            return group;
        }

        /// <summary>
        /// 尝试获取组
        /// </summary>
        /// <param name="name">组名称</param>
        /// <param name="group">输出组实例</param>
        /// <returns>如果找到返回 true</returns>
        public bool TryGetGroup(string name, out NvhMemoryGroup? group)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                group = null;
                return false;
            }
            return _groups.TryGetValue(name, out group);
        }

        /// <summary>
        /// 检查组是否存在
        /// </summary>
        /// <param name="name">组名称</param>
        /// <returns>如果存在返回 true</returns>
        public bool ContainsGroup(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && _groups.ContainsKey(name);
        }

        // ✅ 获取所有组名
        public IEnumerable<string> GetGroupNames()
        {
            return _groups.Keys;
        }

        // ✅ 获取所有组名的数组（方便使用）
        public string[] GetGroupNamesArray()
        {
            return _groups.Keys.ToArray();
        }

        // ✅ 重命名组
        public bool RenameGroup(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName))
                throw new ArgumentException("Old group name cannot be null or empty", nameof(oldName));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New group name cannot be null or empty", nameof(newName));
            if (oldName == newName)
                return true; // 名称相同，无需重命名

            if (!_groups.TryGetValue(oldName, out var group))
                return false; // 旧组不存在

            if (_groups.ContainsKey(newName))
                throw new ArgumentException($"Group '{newName}' already exists", nameof(newName));

            // 从字典中移除旧键
            _groups.Remove(oldName);
            
            // 更新组内部名称
            group.Rename(newName);
            
            // 添加新键
            _groups.Add(newName, group);
            
            return true;
        }

        // ✅ 尝试重命名组（不抛异常）
        public bool TryRenameGroup(string oldName, string newName)
        {
            try
            {
                return RenameGroup(oldName, newName);
            }
            catch
            {
                return false;
            }
        }

        #region 合并多个文件到 Signal 组

        /// <summary>
        /// 合并多个 NvhMemoryFile 的所有组和通道到一个新的 NvhMemoryFile 的 "Signal" 组中
        /// </summary>
        /// <param name="sourceFiles">要合并的源文件集合</param>
        /// <param name="options">合并选项</param>
        /// <returns>合并后的新 NvhMemoryFile 实例</returns>
        /// <exception cref="ArgumentNullException">sourceFiles 为空</exception>
        public static NvhMemoryFile MergeToSignalGroup(
            IEnumerable<NvhMemoryFile> sourceFiles, 
            MergeOptions? options = null)
        {
            if (sourceFiles == null)
                throw new ArgumentNullException(nameof(sourceFiles));

            options ??= new MergeOptions();

            var mergedFile = new NvhMemoryFile();
            var signalGroup = mergedFile.GetOrCreateGroup(options.TargetGroupName ?? "Signal");

            // 用于跟踪已使用的通道名称，避免冲突
            var usedChannelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int fileIndex = 0;

            foreach (var sourceFile in sourceFiles)
            {
                if (sourceFile == null) continue;

                // 遍历源文件的所有组
                foreach (var sourceGroup in sourceFile.Groups.Values)
                {
                    // 遍历组内的所有通道
                    foreach (var sourceChannel in sourceGroup.Channels.Values)
                    {
                        // 生成目标通道名称
                        string targetChannelName = GenerateChannelName(
                            sourceChannel.Name,
                            sourceGroup.Name,
                            fileIndex,
                            options,
                            usedChannelNames);

                        // 复制通道到目标组
                        CopyChannelToGroup(sourceChannel, signalGroup, targetChannelName, options);
                        usedChannelNames.Add(targetChannelName);
                    }
                }

                fileIndex++;
            }

            return mergedFile;
        }

        /// <summary>
        /// 合并多个 NvhMemoryFile 的所有组和通道到一个新的 NvhMemoryFile 的 "Signal" 组中（数组版本）
        /// </summary>
        /// <param name="sourceFiles">要合并的源文件数组</param>
        /// <param name="options">合并选项</param>
        /// <returns>合并后的新 NvhMemoryFile 实例</returns>
        public static NvhMemoryFile MergeToSignalGroup(
            params NvhMemoryFile[] sourceFiles)
        {
            return MergeToSignalGroup(sourceFiles, null);
        }

        /// <summary>
        /// 生成目标通道名称（处理名称冲突）
        /// </summary>
        private static string GenerateChannelName(
            string originalName,
            string groupName,
            int fileIndex,
            MergeOptions options,
            HashSet<string> usedNames)
        {
            string baseName = originalName;

            // 根据选项添加前缀
            if (options.IncludeGroupNameInChannelName && !string.IsNullOrEmpty(groupName))
            {
                baseName = $"{groupName}_{baseName}";
            }

            if (options.IncludeFileIndexInChannelName)
            {
                baseName = $"{baseName}_File{fileIndex}";
            }

            // 如果名称已存在，添加数字后缀
            string finalName = baseName;
            int suffix = 1;
            while (usedNames.Contains(finalName))
            {
                finalName = $"{baseName}_{suffix}";
                suffix++;
            }

            return finalName;
        }

        /// <summary>
        /// 复制通道到目标组（使用反射处理不同数据类型）
        /// </summary>
        private static void CopyChannelToGroup(
            NvhMemoryChannelBase sourceChannel,
            NvhMemoryGroup targetGroup,
            string targetChannelName,
            MergeOptions options)
        {
            Type dataType = sourceChannel.DataType;

            // 使用反射调用泛型方法
            var method = typeof(NvhMemoryFile).GetMethod(
                nameof(CopyChannelToGroupGeneric),
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
                throw new InvalidOperationException("无法找到 CopyChannelToGroupGeneric 方法");

            // 创建泛型方法
            var genericMethod = method.MakeGenericMethod(dataType);

            // 调用泛型方法
            genericMethod.Invoke(null, new object[] { sourceChannel, targetGroup, targetChannelName, options });
        }

        /// <summary>
        /// 复制通道到目标组（泛型版本）
        /// </summary>
        private static void CopyChannelToGroupGeneric<T>(
            NvhMemoryChannelBase sourceChannel,
            NvhMemoryGroup targetGroup,
            string targetChannelName,
            MergeOptions options)
            where T : unmanaged
        {
            // 转换为具体类型的通道
            if (!(sourceChannel is NvhMemoryChannel<T> typedSourceChannel))
            {
                throw new InvalidOperationException($"通道类型不匹配: 期望 {typeof(T)}, 实际 {sourceChannel.DataType}");
            }

            // 1. 创建目标通道
            var targetChannel = targetGroup.CreateChannel<T>(targetChannelName);

            // 2. 复制通道属性
            if (options.CopyProperties)
            {
                foreach (var prop in sourceChannel.Properties.Entries)
                {
                    targetChannel.Properties.Set(prop.Key, prop.Value);
                }
            }

            // 3. 复制通道数据
            if (options.CopyData && typedSourceChannel.TotalSamples > 0)
            {
                ReadOnlySpan<T> sourceData = typedSourceChannel.ReadAll();
                if (!sourceData.IsEmpty)
                {
                    // 批量写入数据
                    targetChannel.WriteSamples(sourceData);
                }
            }
        }

        #endregion
    }

    #region 合并选项

    /// <summary>
    /// 合并选项
    /// </summary>
    public class MergeOptions
    {
        /// <summary>
        /// 目标组名称（默认 "Signal"）
        /// </summary>
        public string? TargetGroupName { get; set; } = "Signal";

        /// <summary>
        /// 是否在通道名称中包含组名（默认 false）
        /// </summary>
        public bool IncludeGroupNameInChannelName { get; set; } = false;

        /// <summary>
        /// 是否在通道名称中包含文件索引（默认 false）
        /// </summary>
        public bool IncludeFileIndexInChannelName { get; set; } = false;

        /// <summary>
        /// 是否复制通道属性（默认 true）
        /// </summary>
        public bool CopyProperties { get; set; } = true;

        /// <summary>
        /// 是否复制通道数据（默认 true）
        /// </summary>
        public bool CopyData { get; set; } = true;
    }

    #endregion
}

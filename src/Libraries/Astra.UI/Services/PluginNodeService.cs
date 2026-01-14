using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Manifest;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.UI.Controls;
using Astra.UI.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Astra.UI.Services
{
    /// <summary>
    /// 插件节点服务 - 从插件清单文件中读取节点信息并转换为工具项
    /// </summary>
    public class PluginNodeService
    {
        private readonly IPluginHost _pluginHost;
        private readonly List<IManifestSerializer> _serializers;

        public PluginNodeService(IPluginHost pluginHost, IEnumerable<IManifestSerializer> serializers = null)
        {
            _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
            _serializers = serializers?.ToList() ?? new List<IManifestSerializer>();
            
            // 如果没有提供序列化器，添加默认的 XML 序列化器
            if (_serializers.Count == 0)
            {
                _serializers.Add(new XmlManifestSerializer());
            }
        }

        /// <summary>
        /// 从所有已加载的插件中获取节点工具类别
        /// </summary>
        public ObservableCollection<ToolCategory> GetToolCategoriesFromPlugins()
        {
            var categories = new ObservableCollection<ToolCategory>();

            if (_pluginHost?.LoadedPlugins == null)
                return categories;

            foreach (var plugin in _pluginHost.LoadedPlugins)
            {
                try
                {
                    // 改为返回多个类别（按分类分组）
                    var pluginCategories = CreateToolCategoriesFromPlugin(plugin);
                    foreach (var category in pluginCategories)
                    {
                        if (category != null && category.Tools.Count > 0)
                        {
                            categories.Add(category);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PluginNodeService] 加载插件 {plugin?.Id} 的节点失败: {ex.Message}");
                }
            }

            return categories;
        }

        /// <summary>
        /// 从插件创建工具类别（支持按分类分组）
        /// </summary>
        /// <param name="plugin">插件实例</param>
        /// <returns>工具类别列表（可能包含多个类别，按分类分组）</returns>
        private List<ToolCategory> CreateToolCategoriesFromPlugin(IPlugin plugin)
        {
            if (plugin == null)
                return new List<ToolCategory>();

            // 获取插件清单文件路径
            var manifestPath = FindManifestFile(plugin);
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                System.Diagnostics.Debug.WriteLine($"[PluginNodeService] 未找到插件 {plugin.Id} 的清单文件");
                return new List<ToolCategory>();
            }

            // 读取清单文件
            var manifest = LoadManifest(manifestPath);
            if (manifest == null || manifest.Nodes == null || manifest.Nodes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginNodeService] 插件 {plugin.Id} 的清单文件中没有节点信息");
                return new List<ToolCategory>();
            }

            var pluginName = manifest.Addin?.Name ?? plugin.Name ?? "插件节点";
            var pluginDescription = manifest.Addin?.Description ?? $"来自插件 {plugin.Name} 的节点";
            // 获取插件的图标路径，如果为空则使用默认图标
            var pluginIconCode = GetIconCodeFromPath(manifest.Addin?.IconPath) ?? "Plug";

            // 按 Category 分组节点
            // 如果 Category 为空，使用 null 作为键，后续会使用插件名称
            var groupedNodes = manifest.Nodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Name) && !string.IsNullOrWhiteSpace(n.TypeName))
                .GroupBy(n => string.IsNullOrWhiteSpace(n.Category) ? null : n.Category)
                .ToList();

            var categories = new List<ToolCategory>();

            foreach (var group in groupedNodes)
            {
                // 如果 Category 为 null，使用插件名称；否则使用 "插件名 - 分类名"
                var categoryName = group.Key == null 
                    ? pluginName 
                    : $"{pluginName} - {group.Key}";

                var category = new ToolCategory
                {
                    Name = categoryName,
                    IconCode = pluginIconCode, // 使用插件的 IconPath 转换后的图标代码
                    Description = group.Key == null 
                        ? pluginDescription 
                        : $"{pluginDescription} - {group.Key}",
                    IsEnabled = true,
                    CategoryColor = GetCategoryColor(plugin),
                    CategoryLightColor = GetCategoryLightColor(plugin),
                    Tools = new ObservableCollection<IToolItem>()
                };

                // 将 NodeInfo 转换为 ToolItem
                foreach (var nodeInfo in group)
                {
                    // 处理 IconCode：支持 FontAwesome 图标名或文件路径
                    var iconCode = GetIconCodeFromPath(nodeInfo.IconCode) ?? "Circle";
                    
                    var toolItem = new ToolItem
                    {
                        Name = nodeInfo.Name,
                        IconCode = iconCode, // 支持 FontAwesome 或文件路径
                        Description = nodeInfo.Description ?? string.Empty,
                        NodeType = nodeInfo.TypeName, // 使用类型名称字符串，FlowEditor 会动态解析
                        IsEnabled = true
                    };

                    category.Tools.Add(toolItem);
                }

                categories.Add(category);
            }

            return categories;
        }

        /// <summary>
        /// 查找插件的清单文件路径
        /// </summary>
        private string FindManifestFile(IPlugin plugin)
        {
            // 方法1: 从插件程序集路径推断
            // 需要访问 PluginDescriptor，但 IPlugin 接口可能没有直接暴露
            // 尝试从插件程序集位置推断

            // 方法2: 从插件目录查找 .addin 文件
            // 假设清单文件在插件程序集所在目录，文件名为 插件名.addin 或 插件ID.addin

            try
            {
                // 获取插件程序集路径（通过反射）
                var pluginType = plugin.GetType();
                var assembly = pluginType.Assembly;
                var assemblyPath = assembly.Location;

                if (string.IsNullOrEmpty(assemblyPath))
                    return null;

                var pluginDirectory = Path.GetDirectoryName(assemblyPath);
                if (string.IsNullOrEmpty(pluginDirectory))
                    return null;

                // 尝试多种可能的清单文件名
                var possibleNames = new[]
                {
                    $"{plugin.Id}.addin",
                    $"{plugin.Name}.addin",
                    $"{pluginType.Name}.addin",
                    Path.GetFileNameWithoutExtension(assemblyPath) + ".addin"
                };

                foreach (var name in possibleNames)
                {
                    var manifestPath = Path.Combine(pluginDirectory, name);
                    if (File.Exists(manifestPath))
                    {
                        return manifestPath;
                    }
                }

                // 如果没找到，尝试在目录中查找所有 .addin 文件
                var addinFiles = Directory.GetFiles(pluginDirectory, "*.addin", SearchOption.TopDirectoryOnly);
                if (addinFiles.Length == 1)
                {
                    return addinFiles[0];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginNodeService] 查找清单文件失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 加载清单文件
        /// </summary>
        private AddinManifest LoadManifest(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                return null;

            try
            {
                // 查找合适的序列化器
                var serializer = _serializers.FirstOrDefault(s => s.CanHandle(manifestPath));
                if (serializer == null)
                {
                    // 如果没有注册序列化器，使用默认的 XML 序列化器
                    serializer = new XmlManifestSerializer();
                }

                using var stream = File.OpenRead(manifestPath);
                return serializer.Deserialize(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginNodeService] 加载清单文件失败 {manifestPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从图标路径或图标代码获取图标代码
        /// 支持两种格式：
        /// 1. FontAwesome 图标名（如 "Plug", "Circle"）- 直接返回
        /// 2. 文件路径（如 "icon.png", "path/to/icon.png", "C:\path\to\icon.png"）- 提取文件名（不含扩展名）作为图标名
        /// 
        /// 注意：由于 IconConverter 只支持 FontAwesome 图标，文件路径会被转换为文件名。
        /// 如果文件名不是有效的 FontAwesome 图标名，IconConverter 会使用默认图标 "Circle"。
        /// </summary>
        /// <param name="iconPath">图标路径或图标代码</param>
        /// <returns>处理后的图标代码，如果输入为空则返回 null</returns>
        private string GetIconCodeFromPath(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            // 如果 IconPath 不包含路径分隔符，可能是直接的 FontAwesome 图标名
            if (!iconPath.Contains("/") && !iconPath.Contains("\\"))
            {
                // 直接作为 FontAwesome 图标名使用
                return iconPath;
            }

            // 如果是文件路径，尝试提取文件名（不含扩展名）作为图标名
            // 例如："path/to/icon.png" -> "icon", "C:\path\to\icon.png" -> "icon"
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(iconPath);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    // 返回文件名，IconConverter 会尝试将其解析为 FontAwesome 图标
                    // 如果解析失败，IconConverter 会使用默认图标
                    return fileName;
                }
            }
            catch
            {
                // 如果路径解析失败，返回 null，使用默认图标
            }

            return null;
        }

        /// <summary>
        /// 获取类别颜色
        /// </summary>
        private Brush GetCategoryColor(IPlugin plugin)
        {
            return Application.Current?.FindResource("PrimaryBrush") as Brush 
                   ?? new SolidColorBrush(Colors.Blue);
        }

        /// <summary>
        /// 获取类别浅色
        /// </summary>
        private Brush GetCategoryLightColor(IPlugin plugin)
        {
            return Application.Current?.FindResource("LightPrimaryBrush") as Brush 
                   ?? new SolidColorBrush(Colors.LightBlue);
        }
    }
}


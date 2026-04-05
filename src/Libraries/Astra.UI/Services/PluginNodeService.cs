using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Manifest;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.UI.Controls;
using Astra.UI.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using System.Windows.Media;

namespace Astra.UI.Services
{
    /// <summary>
    /// 插件节点服务 - 从插件清单文件中读取节点信息并转换为工具项
    /// </summary>
    public class PluginNodeService
    {
        /// <summary>
        /// 主流程画布工具箱仅展示该插件注册的节点（与子流程完整工具箱分离）。
        /// </summary>
        public const string MasterWorkflowToolboxPluginId = "Astra.Plugins.Logic";

        private readonly IPluginHost _pluginHost;
        private readonly List<IManifestSerializer> _serializers;

        public PluginNodeService(IPluginHost pluginHost, IEnumerable<IManifestSerializer> serializers = null)
        {
            _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
            var list = serializers?.Where(s => s != null).ToList() ?? new List<IManifestSerializer>();
            if (list.Count == 0)
            {
                list.Add(new XmlManifestSerializer());
            }
            else
            {
                // 确保存在 XmlManifestSerializer，并放在列表首位，使 .addin 由 Xml 解析（而非仅依赖回退分支）
                var xml = list.OfType<XmlManifestSerializer>().FirstOrDefault();
                if (xml == null)
                {
                    list.Insert(0, new XmlManifestSerializer());
                }
                else if (list.IndexOf(xml) > 0)
                {
                    list.Remove(xml);
                    list.Insert(0, xml);
                }
            }

            _serializers = list;
        }

        /// <summary>
        /// 从所有已加载的插件中获取节点工具类别。
        /// </summary>
        /// <remarks>
        /// 依赖 <see cref="IPluginHost.LoadedPlugins"/>；若在插件尚未注册到宿主之前调用，结果为空。
        /// 调用方（如 <see cref="Astra.UI.ViewModels.MultiFlowEditorViewModel"/>）应在适当时机（如 ApplicationIdle 或视图 Loaded）再刷新。
        /// </remarks>
        public ObservableCollection<ToolCategory> GetToolCategoriesFromPlugins()
            => CollectToolCategories(_ => true);

        /// <summary>
        /// 仅从指定插件 Id（不区分大小写）加载工具类别，用于主流程等场景下的精简工具箱。
        /// </summary>
        public ObservableCollection<ToolCategory> GetToolCategoriesForPlugins(params string[] pluginIds)
        {
            var idSet = new HashSet<string>(
                pluginIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            if (idSet.Count == 0)
            {
                return new ObservableCollection<ToolCategory>();
            }

            return CollectToolCategories(p => idSet.Contains(p.Id));
        }

        private ObservableCollection<ToolCategory> CollectToolCategories(Func<IPlugin, bool> includePlugin)
        {
            var categories = new ObservableCollection<ToolCategory>();

            if (_pluginHost?.LoadedPlugins == null)
                return categories;

            var entries = new List<(ToolCategory Cat, int PluginIdx, int CatIdx)>();
            var pluginIdx = 0;
            foreach (var plugin in _pluginHost.LoadedPlugins)
            {
                if (plugin == null || !includePlugin(plugin))
                {
                    pluginIdx++;
                    continue;
                }

                try
                {
                    var pluginCategories = CreateToolCategoriesFromPlugin(plugin);
                    var catIdx = 0;
                    foreach (var category in pluginCategories)
                    {
                        if (category != null && category.Tools.Count > 0)
                            entries.Add((category, pluginIdx, catIdx));
                        catIdx++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PluginNodeService] 加载插件 {plugin?.Id} 的节点失败: {ex.Message}");
                }

                pluginIdx++;
            }

            // 越小越靠前：先插件清单 Addin/Order（ToolboxOrder），再宿主加载顺序，再同一插件内分类的 SortOrder（节点 Order 最小值），最后分类序号
            var sorted = entries
                .OrderBy(e => e.Cat.PluginOrder ?? int.MaxValue)
                .ThenBy(e => e.PluginIdx)
                .ThenBy(e => e.Cat.SortOrder)
                .ThenBy(e => e.CatIdx)
                .Select(e => e.Cat)
                .ToList();
            return new ObservableCollection<ToolCategory>(sorted);
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

            // 工具箱根顺序：以清单 Addin/Order（ToolboxOrder）为准（越小越靠前）；反序列化未带上时从 XML 再读一遍
            var pluginOrder = manifest.Addin?.ToolboxOrder ?? TryReadAddinOrderFromXml(manifestPath);

            // 带清单索引，便于同 Order 时保持 .addin 中的声明顺序
            var indexedNodes = manifest.Nodes
                .Select((n, i) => (Node: n, Index: i))
                .Where(x => !string.IsNullOrWhiteSpace(x.Node.Name) && !string.IsNullOrWhiteSpace(x.Node.TypeName))
                .ToList();

            // 如果 Category 为空，使用 null 作为键，后续会使用插件名称
            var groupedNodes = indexedNodes
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Node.Category) ? null : x.Node.Category)
                .ToList();

            var orderedGroups = groupedNodes
                .OrderBy(g => g.Min(x => x.Node.Order))
                .ThenBy(g => g.Min(x => x.Index))
                .ToList();

            var categories = new List<ToolCategory>();

            foreach (var group in orderedGroups)
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
                    Tools = new ObservableCollection<IToolItem>(),
                    SortOrder = group.Min(x => x.Node.Order),
                    PluginOrder = pluginOrder
                };

                // 将 NodeInfo 转换为 ToolItem（按 Order，再按清单顺序）
                foreach (var x in group.OrderBy(t => t.Node.Order).ThenBy(t => t.Index))
                {
                    var nodeInfo = x.Node;
                    // 处理 IconCode：支持 FontAwesome 图标名或文件路径
                    var iconCode = GetIconCodeFromPath(nodeInfo.IconCode) ?? "Circle";
                    
                    var toolItem = new ToolItem
                    {
                        Name = nodeInfo.Name,
                        IconCode = iconCode, // 支持 FontAwesome 或文件路径
                        Description = nodeInfo.Description ?? string.Empty,
                        NodeType = nodeInfo.TypeName, // 使用类型名称字符串，FlowEditor 会动态解析
                        // 组名：为空或空白时不设置，避免在 UI 中占用多余的组头空间
                        GroupName = string.IsNullOrWhiteSpace(nodeInfo.Group) ? null : nodeInfo.Group,
                        IsEnabled = true
                    };

                    category.Tools.Add(toolItem);
                }

                categories.Add(category);
            }

            return categories;
        }

        /// <summary>
        /// 查找插件的清单文件路径（与插件主 DLL 同目录）
        /// </summary>
        private string FindManifestFile(IPlugin plugin)
        {
            try
            {
                var pluginType = plugin.GetType();
                var assembly = pluginType.Assembly;
                var assemblyPath = assembly.Location;

                if (string.IsNullOrEmpty(assemblyPath))
                {
                    return null;
                }

                var pluginDirectory = Path.GetDirectoryName(assemblyPath);
                if (string.IsNullOrEmpty(pluginDirectory))
                {
                    return null;
                }

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
        /// 从 .addin 的 XML 中读取 <c>Addin/Order</c>（与 <see cref="AddinInfo.ToolboxOrder"/> 对应），避免个别环境下 XmlSerializer 未填充该字段。
        /// </summary>
        private static int? TryReadAddinOrderFromXml(string manifestPath)
        {
            try
            {
                var doc = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
                var addin = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Addin");
                var orderEl = addin?.Elements().FirstOrDefault(e => e.Name.LocalName == "Order");
                if (orderEl != null && int.TryParse(orderEl.Value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginNodeService] TryReadAddinOrderFromXml 失败 {manifestPath}: {ex.Message}");
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


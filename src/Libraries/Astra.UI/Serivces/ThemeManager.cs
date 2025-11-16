using Astra.UI.Abstractions.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Astra.UI.Serivces
{
    /// <summary>
    /// 主题管理器（线程安全的单例）
    /// </summary>
    public sealed class ThemeManager : IThemeProvider
    {
        #region Singleton

        private static readonly Lazy<ThemeManager> _instance =
            new Lazy<ThemeManager>(() => new ThemeManager());

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ThemeManager Instance => _instance.Value;

        private ThemeManager()
        {
            InitializeBuiltInThemes();
        }

        #endregion

        #region Fields & Properties

        private readonly Dictionary<string, ITheme> _themes = new Dictionary<string, ITheme>(StringComparer.OrdinalIgnoreCase);
        private ITheme _currentTheme;
        private IThemeConfigStorage _configStorage;

        /// <summary>
        /// 当前主题
        /// </summary>
        public ITheme CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    var oldTheme = _currentTheme;
                    _currentTheme = value;
                    ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, value));
                }
            }
        }

        /// <summary>
        /// 主题改变事件
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化内置主题
        /// </summary>
        private void InitializeBuiltInThemes()
        {
            RegisterTheme(BuiltInThemes.Light);
            RegisterTheme(BuiltInThemes.Dark);
            RegisterTheme(BuiltInThemes.Blue);

            _currentTheme = BuiltInThemes.Light;
        }

        /// <summary>
        /// 配置主题管理器（应用启动时调用）
        /// </summary>
        /// <param name="configStorage">配置存储实现（可选，不提供则不持久化）</param>
        /// <param name="autoLoad">是否自动加载保存的主题</param>
        public void Configure(IThemeConfigStorage configStorage = null, bool autoLoad = true)
        {
            _configStorage = configStorage;

            if (autoLoad && _configStorage != null)
            {
                LoadSavedTheme();
            }
        }

        #endregion

        #region IThemeProvider Implementation

        /// <summary>
        /// 获取所有可用主题
        /// </summary>
        public IEnumerable<ITheme> GetAvailableThemes()
        {
            return _themes.Values.ToList();
        }

        /// <summary>
        /// 根据 ID 获取主题
        /// </summary>
        public ITheme GetTheme(string themeId)
        {
            if (string.IsNullOrWhiteSpace(themeId))
                return null;

            return _themes.TryGetValue(themeId, out var theme) ? theme : null;
        }

        /// <summary>
        /// 注册自定义主题
        /// </summary>
        public void RegisterTheme(ITheme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            if (string.IsNullOrWhiteSpace(theme.Id))
                throw new ArgumentException("主题 ID 不能为空", nameof(theme));

            _themes[theme.Id] = theme;
            System.Diagnostics.Debug.WriteLine($"[ThemeManager] 注册主题: {theme.DisplayName} ({theme.Id})");
        }

        /// <summary>
        /// 注销主题
        /// </summary>
        public bool UnregisterTheme(string themeId)
        {
            if (string.IsNullOrWhiteSpace(themeId))
                return false;

            var theme = GetTheme(themeId);
            if (theme?.IsBuiltIn == true)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] 无法注销内置主题: {themeId}");
                return false;
            }

            var result = _themes.Remove(themeId);

            // 如果当前主题被注销，切换到默认主题
            if (result && CurrentTheme?.Id == themeId)
            {
                ApplyTheme(BuiltInThemes.Light);
            }

            return result;
        }

        #endregion

        #region Theme Application

        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme(ITheme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            var app = Application.Current;
            if (app == null)
            {
                System.Diagnostics.Debug.WriteLine("[ThemeManager] Application.Current 为 null，无法应用主题");
                return;
            }

            try
            {
                // 移除旧主题资源
                RemoveCurrentThemeResources();

                // 加载新主题资源字典
                var resourceUri = GetResourceUri(theme.ResourceUri);
                var themeDict = new ResourceDictionary
                {
                    Source = resourceUri
                };

                // 插入到最前面，确保优先级最高
                app.Resources.MergedDictionaries.Insert(0, themeDict);

                // 更新当前主题
                CurrentTheme = theme;

                // 保存主题偏好
                _configStorage?.SaveThemeId(theme.Id);

                System.Diagnostics.Debug.WriteLine($"[ThemeManager] ✅ 主题应用成功: {theme.DisplayName} ({resourceUri})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] ❌ 主题应用失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] 堆栈跟踪: {ex.StackTrace}");
                throw new InvalidOperationException($"应用主题失败: {theme.DisplayName}", ex);
            }
        }

        /// <summary>
        /// 根据 ID 应用主题
        /// </summary>
        public void ApplyTheme(string themeId)
        {
            var theme = GetTheme(themeId);
            if (theme == null)
                throw new ArgumentException($"未找到主题: {themeId}", nameof(themeId));

            ApplyTheme(theme);
        }

        /// <summary>
        /// 移除当前主题资源
        /// </summary>
        private void RemoveCurrentThemeResources()
        {
            var app = Application.Current;
            if (app == null) return;

            var toRemove = new List<ResourceDictionary>();

            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null && IsThemeResourceDictionary(dict.Source))
                {
                    toRemove.Add(dict);
                }
            }

            foreach (var dict in toRemove)
            {
                app.Resources.MergedDictionaries.Remove(dict);
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] 移除旧主题: {dict.Source}");
            }
        }

        /// <summary>
        /// 判断是否为主题资源字典
        /// </summary>
        private bool IsThemeResourceDictionary(Uri uri)
        {
            var path = uri.OriginalString;

            // 检查所有已注册主题的路径
            return _themes.Values.Any(t =>
                path.Contains(t.ResourceUri.OriginalString, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取资源 URI（处理跨程序集引用）
        /// </summary>
        private Uri GetResourceUri(Uri resourceUri)
        {
            // 如果已经是绝对 URI，直接返回
            if (resourceUri.IsAbsoluteUri)
                return resourceUri;

            // 构建 Pack URI
            var assemblyName = GetType().Assembly.GetName().Name;
            var packUri = $"pack://application:,,,/{assemblyName};component{resourceUri.OriginalString}";

            System.Diagnostics.Debug.WriteLine($"[ThemeManager] 转换 URI: {resourceUri} -> {packUri}");

            return new Uri(packUri, UriKind.Absolute);
        }

        #endregion

        #region Persistence

        /// <summary>
        /// 加载保存的主题
        /// </summary>
        public void LoadSavedTheme()
        {
            if (_configStorage == null)
            {
                System.Diagnostics.Debug.WriteLine("[ThemeManager] 未配置存储，使用默认主题");
                ApplyTheme(BuiltInThemes.Light);
                return;
            }

            try
            {
                var themeId = _configStorage.LoadThemeId();

                if (!string.IsNullOrEmpty(themeId))
                {
                    var theme = GetTheme(themeId);
                    if (theme != null)
                    {
                        ApplyTheme(theme);
                        System.Diagnostics.Debug.WriteLine($"[ThemeManager] 加载保存的主题: {theme.DisplayName}");
                        return;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThemeManager] 未找到保存的主题: {themeId}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] 加载主题失败: {ex.Message}");
            }

            // 使用默认主题
            ApplyTheme(BuiltInThemes.Light);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 切换到下一个主题（循环）
        /// </summary>
        public void ToggleTheme()
        {
            var themes = GetAvailableThemes().ToList();
            if (themes.Count == 0) return;

            var currentIndex = themes.FindIndex(t => t.Id == CurrentTheme?.Id);
            var nextIndex = (currentIndex + 1) % themes.Count;

            ApplyTheme(themes[nextIndex]);
        }

        #endregion
    }
}

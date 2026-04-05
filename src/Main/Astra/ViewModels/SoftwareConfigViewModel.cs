using System.Collections.Generic;
using System.Linq;
using Astra.Configuration;
using Astra.Core.Foundation.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Astra.ViewModels
{
    public partial class SoftwareConfigViewModel : ObservableObject, IDisposable
    {
        private const string SolutionsFolderName = "Solutions";
        private const string SolutionFilePattern = "*.sol";
        private readonly IConfigurationManager? _configurationManager;
        private bool _isRefreshingWorkflowOptions;
        private FileSystemWatcher? _solutionsWatcher;
        private FileSystemWatcher? _baseDirectoryWatcher;
        private DispatcherTimer? _workflowOptionsDebounceTimer;
        private DispatcherTimer? _triggerOptionsDebounceTimer;
        private readonly Action<TriggerBaseConfig, ConfigChangeType> _onTriggerConfigChangedHandler;
        private bool _isDisposed;

        [ObservableProperty]
        private SoftwareConfig _config;

        [ObservableProperty]
        private ObservableCollection<SelectionOption> _workflowOptions = new ObservableCollection<SelectionOption>();

    [ObservableProperty]
    private ObservableCollection<SelectionOption> _triggerOptions = new ObservableCollection<SelectionOption>();

    [ObservableProperty]
    private ObservableCollection<DutViewModel> _dutViewModels = new ObservableCollection<DutViewModel>();

        public SoftwareConfigViewModel(SoftwareConfig config, IConfigurationManager configurationManager)
        {
            _config = config ?? new SoftwareConfig();
            _configurationManager = configurationManager;
            _onTriggerConfigChangedHandler = OnTriggerConfigChanged;

            HookDutEvents();
            SyncDutViewModels();
            LoadWorkflowOptionsFromSolutions();
            InitializeWorkflowOptionsWatcher();
            InitializeTriggerOptionsSubscription();
            _ = LoadTriggerOptionsAsync();

            if(string.IsNullOrWhiteSpace(Config.ReportOutputRootDirectory))
                Config.ReportOutputRootDirectory = PathHelper.GetReportDefaultRootDirectory();
            
        }

        [RelayCommand]
        private void BrowseReportOutputRoot()
        {
            if (_isDisposed || Config == null || _configurationManager == null)
                return;

            var dlg = new OpenFolderDialog
            {
                Title = "选择报告输出根目录",
                Multiselect = false,
            };

            var current = Config.ReportOutputRootDirectory?.Trim();
            if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                dlg.InitialDirectory = current;
            else
                dlg.InitialDirectory = PathHelper.GetReportDefaultRootDirectory();

            if (dlg.ShowDialog() != true)
                return;

            var picked = dlg.FolderName?.Trim();
            if (string.IsNullOrWhiteSpace(picked))
                return;

            Config.ReportOutputRootDirectory = picked;
            _ = PublishSoftwareConfigUpdatedAsync();
        }

    /// <summary>DUT 集合，便于 XAML 绑定</summary>
    public ObservableCollection<DutConfig> Duts => Config?.Duts;

        private void HookDutEvents()
        {
            if (Config?.Duts == null)
                return;

            Config.Duts.CollectionChanged += OnDutsCollectionChanged;

            foreach (var dut in Config.Duts)
            {
                if (dut != null)
                {
                    dut.PropertyChanged += OnDutPropertyChanged;
                }
            }
        }

    private void OnDutsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<DutConfig>())
            {
                item.PropertyChanged -= OnDutPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<DutConfig>())
            {
                item.PropertyChanged += OnDutPropertyChanged;
            }
        }

        SyncDutViewModels();
    }

    private void SyncDutViewModels()
    {
        DutViewModels.Clear();
        if (Config?.Duts == null) return;
        foreach (var dut in Config.Duts)
        {
            if (dut != null)
                DutViewModels.Add(new DutViewModel(dut));
        }
        foreach (var vm in DutViewModels)
            vm.ApplyTriggerSelection(TriggerOptions);
    }

        private void OnDutPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DutConfig dut)
                return;

            if (e.PropertyName == nameof(DutConfig.WorkflowId))
            {
                if (_isRefreshingWorkflowOptions)
                    return;

                var workflow = WorkflowOptions.FirstOrDefault(x => string.Equals(x.Id, dut.WorkflowId, StringComparison.OrdinalIgnoreCase));
                dut.WorkflowName = workflow?.Name ?? string.Empty;
                Config.CurrentWorkflowId = dut.WorkflowId ?? string.Empty;
                Config.CurrentWorkflowName = dut.WorkflowName ?? string.Empty;
                _ = PublishSoftwareConfigUpdatedAsync();
            }
            else if (e.PropertyName == nameof(DutConfig.WorkflowName))
            {
                // 兜底：若界面或历史数据只改了名称，尝试反查并同步 WorkflowId
                var workflow = WorkflowOptions.FirstOrDefault(x => string.Equals(x.Name, dut.WorkflowName, StringComparison.OrdinalIgnoreCase));
                if (workflow != null && !string.Equals(dut.WorkflowId, workflow.Id, StringComparison.OrdinalIgnoreCase))
                {
                    dut.WorkflowId = workflow.Id;
                }
            }
        else if (e.PropertyName == nameof(DutConfig.TriggerConfigId))
        {
            // TriggerName 由 DutViewModel.SelectedTriggerOption.set 已同步；
            // 此处保留作为兜底（若 TriggerConfigId 被外部直接修改）。
            var trigger = TriggerOptions.FirstOrDefault(x => string.Equals(x.Id, dut.TriggerConfigId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(dut.TriggerName, trigger?.Name ?? string.Empty, StringComparison.Ordinal))
                dut.TriggerName = trigger?.Name ?? string.Empty;
        }
        }

        private async Task PublishSoftwareConfigUpdatedAsync()
        {
            if (_configurationManager == null || Config == null)
                return;

            try
            {
                await _configurationManager.SaveAsync(Config).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 选择脚本时只做“刷新通知”，失败不阻塞当前编辑。
                Debug.WriteLine($"[SoftwareConfigViewModel] 发布脚本选择变更失败: {ex.Message}");
            }
        }

        private void LoadWorkflowOptionsFromSolutions()
        {
            WorkflowOptions.Clear();
            WorkflowOptions.Add(SelectionOption.Empty("未选择"));

            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SolutionsFolderName);
            if (!Directory.Exists(folder))
            {
                return;
            }

            var files = Directory
                .EnumerateFiles(folder, SolutionFilePattern, SearchOption.TopDirectoryOnly)
                .Select(file => new SelectionOption
                {
                    Id = file,
                    Name = Path.GetFileNameWithoutExtension(file)
                })
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                WorkflowOptions.Add(file);
            }

            if (Config?.Duts == null)
                return;

            foreach (var dut in Config.Duts)
            {
                if (dut == null)
                    continue;

                var workflow = WorkflowOptions.FirstOrDefault(x => string.Equals(x.Id, dut.WorkflowId, StringComparison.OrdinalIgnoreCase));
                dut.WorkflowName = workflow?.Name ?? string.Empty;
            }

            // 加载界面时，若已有当前脚本则保留；否则从首个已配置 DUT 回填一次
            if (string.IsNullOrWhiteSpace(Config.CurrentWorkflowId))
            {
                var firstSelected = Config.Duts.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d?.WorkflowId));
                if (firstSelected != null)
                {
                    Config.CurrentWorkflowId = firstSelected.WorkflowId ?? string.Empty;
                    Config.CurrentWorkflowName = firstSelected.WorkflowName ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// 在脚本下拉框展开时刷新一次脚本列表，避免在选择变更事件中重复加载。
        /// </summary>
        public void RefreshWorkflowOptions()
        {
            if (_isDisposed)
                return;

            if (_isRefreshingWorkflowOptions)
                return;

            var workflowIdSnapshot = Config?.Duts?
                .Where(d => d != null)
                .ToDictionary(d => d, d => d.WorkflowId ?? string.Empty)
                ?? new System.Collections.Generic.Dictionary<DutConfig, string>();
            var currentWorkflowIdSnapshot = Config?.CurrentWorkflowId ?? string.Empty;
            var currentWorkflowNameSnapshot = Config?.CurrentWorkflowName ?? string.Empty;

            var latest = GetWorkflowOptionsFromSolutions();
            var current = WorkflowOptions?.ToList() ?? new System.Collections.Generic.List<SelectionOption>();

            // 选项未变化则不刷新，避免 ComboBox 重建 ItemsSource 导致已选值被清空。
            if (AreWorkflowOptionsEquivalent(current, latest))
                return;

            _isRefreshingWorkflowOptions = true;
            try
            {
                LoadWorkflowOptionsFromSolutions();

                // 保护已选脚本：刷新列表后还原刷新前的 WorkflowId，避免被控件临时回写为空。
                foreach (var pair in workflowIdSnapshot)
                {
                    var dut = pair.Key;
                    var oldWorkflowId = pair.Value ?? string.Empty;
                    if (dut == null || string.IsNullOrWhiteSpace(oldWorkflowId))
                        continue;

                    if (!string.Equals(dut.WorkflowId, oldWorkflowId, StringComparison.OrdinalIgnoreCase))
                    {
                        dut.WorkflowId = oldWorkflowId;
                    }

                    var workflow = WorkflowOptions.FirstOrDefault(x => string.Equals(x.Id, oldWorkflowId, StringComparison.OrdinalIgnoreCase));
                    dut.WorkflowName = workflow?.Name ?? dut.WorkflowName ?? string.Empty;
                }

                if (Config != null && !string.IsNullOrWhiteSpace(currentWorkflowIdSnapshot))
                {
                    Config.CurrentWorkflowId = currentWorkflowIdSnapshot;
                    var currentWorkflow = WorkflowOptions.FirstOrDefault(x => string.Equals(x.Id, currentWorkflowIdSnapshot, StringComparison.OrdinalIgnoreCase));
                    Config.CurrentWorkflowName = currentWorkflow?.Name ?? currentWorkflowNameSnapshot;
                }
            }
            finally
            {
                _isRefreshingWorkflowOptions = false;
            }
        }

        private static bool AreWorkflowOptionsEquivalent(
            System.Collections.Generic.IReadOnlyList<SelectionOption> left,
            System.Collections.Generic.IReadOnlyList<SelectionOption> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                var l = left[i];
                var r = right[i];
                if (!string.Equals(l?.Id, r?.Id, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(l?.Name, r?.Name, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private System.Collections.Generic.List<SelectionOption> GetWorkflowOptionsFromSolutions()
        {
            var result = new System.Collections.Generic.List<SelectionOption>
            {
                SelectionOption.Empty("未选择")
            };

            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SolutionsFolderName);
            if (!Directory.Exists(folder))
            {
                return result;
            }

            var files = Directory
                .EnumerateFiles(folder, SolutionFilePattern, SearchOption.TopDirectoryOnly)
                .Select(file => new SelectionOption
                {
                    Id = file,
                    Name = Path.GetFileNameWithoutExtension(file)
                })
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.AddRange(files);
            return result;
        }

        private void InitializeWorkflowOptionsWatcher()
        {
            try
            {
                _workflowOptionsDebounceTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(300),
                    DispatcherPriority.Background,
                    OnWorkflowOptionsDebounceTimerTick,
                    Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
                _workflowOptionsDebounceTimer.Stop();

                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                _baseDirectoryWatcher = new FileSystemWatcher(baseDirectory)
                {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };
                _baseDirectoryWatcher.Created += OnBaseDirectoryChanged;
                _baseDirectoryWatcher.Deleted += OnBaseDirectoryChanged;
                _baseDirectoryWatcher.Renamed += OnBaseDirectoryRenamed;
                _baseDirectoryWatcher.Error += OnSolutionsFolderWatcherError;

                EnsureSolutionsWatcher();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoftwareConfigViewModel] 初始化脚本目录监听失败: {ex.Message}");
            }
        }

        private void EnsureSolutionsWatcher()
        {
            var solutionsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SolutionsFolderName);
            var folderExists = Directory.Exists(solutionsFolder);

            if (!folderExists)
            {
                ReleaseSolutionsWatcher();
                return;
            }

            if (_solutionsWatcher != null &&
                string.Equals(_solutionsWatcher.Path, solutionsFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ReleaseSolutionsWatcher();
            _solutionsWatcher = new FileSystemWatcher(solutionsFolder, SolutionFilePattern)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _solutionsWatcher.Created += OnSolutionsFolderChanged;
            _solutionsWatcher.Deleted += OnSolutionsFolderChanged;
            _solutionsWatcher.Changed += OnSolutionsFolderChanged;
            _solutionsWatcher.Renamed += OnSolutionsFolderRenamed;
            _solutionsWatcher.Error += OnSolutionsFolderWatcherError;
        }

        private void OnBaseDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            if (!string.Equals(e.Name, SolutionsFolderName, StringComparison.OrdinalIgnoreCase))
                return;

            EnsureSolutionsWatcher();
            ScheduleWorkflowOptionsRefresh();
        }

        private void OnBaseDirectoryRenamed(object sender, RenamedEventArgs e)
        {
            var oldIsSolutions = string.Equals(e.OldName, SolutionsFolderName, StringComparison.OrdinalIgnoreCase);
            var newIsSolutions = string.Equals(e.Name, SolutionsFolderName, StringComparison.OrdinalIgnoreCase);
            if (!oldIsSolutions && !newIsSolutions)
                return;

            EnsureSolutionsWatcher();
            ScheduleWorkflowOptionsRefresh();
        }

        private void OnSolutionsFolderChanged(object sender, FileSystemEventArgs e)
        {
            ScheduleWorkflowOptionsRefresh();
        }

        private void OnSolutionsFolderRenamed(object sender, RenamedEventArgs e)
        {
            ScheduleWorkflowOptionsRefresh();
        }

        private void OnSolutionsFolderWatcherError(object sender, ErrorEventArgs e)
        {
            EnsureSolutionsWatcher();
            ScheduleWorkflowOptionsRefresh();
        }

        private void ScheduleWorkflowOptionsRefresh()
        {
            if (_isDisposed)
                return;

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                RestartWorkflowOptionsDebounceTimer();
                return;
            }

            dispatcher.BeginInvoke(new Action(RestartWorkflowOptionsDebounceTimer), DispatcherPriority.Background);
        }

        private void RestartWorkflowOptionsDebounceTimer()
        {
            if (_workflowOptionsDebounceTimer == null || _isDisposed)
                return;

            _workflowOptionsDebounceTimer.Stop();
            _workflowOptionsDebounceTimer.Start();
        }

        private void OnWorkflowOptionsDebounceTimerTick(object? sender, EventArgs e)
        {
            if (_workflowOptionsDebounceTimer != null)
            {
                _workflowOptionsDebounceTimer.Stop();
            }

            RefreshWorkflowOptions();
        }

        /// <summary>
        /// 监听触发器配置的增删改，防抖后刷新软件配置页「触发器」下拉选项。
        /// </summary>
        private void InitializeTriggerOptionsSubscription()
        {
            if (_configurationManager == null)
                return;

            _configurationManager.Subscribe<TriggerBaseConfig>(_onTriggerConfigChangedHandler);

            _triggerOptionsDebounceTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(300),
                DispatcherPriority.Background,
                OnTriggerOptionsDebounceTimerTick,
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
            _triggerOptionsDebounceTimer.Stop();
        }

        private void OnTriggerConfigChanged(TriggerBaseConfig config, ConfigChangeType changeType)
        {
            if (_isDisposed)
                return;

            // 删除触发器后立即刷新下拉并清理无效引用；增改仍防抖，避免连续保存时频繁拉全量。
            if (changeType == ConfigChangeType.Deleted)
            {
                _ = LoadTriggerOptionsAsync();
                return;
            }

            ScheduleTriggerOptionsRefresh();
        }

        private void ScheduleTriggerOptionsRefresh()
        {
            if (_isDisposed)
                return;

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                RestartTriggerOptionsDebounceTimer();
                return;
            }

            dispatcher.BeginInvoke(new Action(RestartTriggerOptionsDebounceTimer), DispatcherPriority.Background);
        }

        private void RestartTriggerOptionsDebounceTimer()
        {
            if (_triggerOptionsDebounceTimer == null || _isDisposed)
                return;

            _triggerOptionsDebounceTimer.Stop();
            _triggerOptionsDebounceTimer.Start();
        }

        private void OnTriggerOptionsDebounceTimerTick(object? sender, EventArgs e)
        {
            if (_triggerOptionsDebounceTimer != null)
                _triggerOptionsDebounceTimer.Stop();

            _ = LoadTriggerOptionsAsync();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_configurationManager != null)
            {
                _configurationManager.Unsubscribe<TriggerBaseConfig>(_onTriggerConfigChangedHandler);
            }

            if (_triggerOptionsDebounceTimer != null)
            {
                _triggerOptionsDebounceTimer.Stop();
                _triggerOptionsDebounceTimer.Tick -= OnTriggerOptionsDebounceTimerTick;
                _triggerOptionsDebounceTimer = null;
            }

            if (Config?.Duts != null)
            {
                Config.Duts.CollectionChanged -= OnDutsCollectionChanged;
                foreach (var dut in Config.Duts)
                {
                    if (dut != null)
                    {
                        dut.PropertyChanged -= OnDutPropertyChanged;
                    }
                }
            }

            if (_workflowOptionsDebounceTimer != null)
            {
                _workflowOptionsDebounceTimer.Stop();
                _workflowOptionsDebounceTimer.Tick -= OnWorkflowOptionsDebounceTimerTick;
                _workflowOptionsDebounceTimer = null;
            }

            if (_solutionsWatcher != null)
            {
                ReleaseSolutionsWatcher();
            }

            if (_baseDirectoryWatcher != null)
            {
                _baseDirectoryWatcher.EnableRaisingEvents = false;
                _baseDirectoryWatcher.Created -= OnBaseDirectoryChanged;
                _baseDirectoryWatcher.Deleted -= OnBaseDirectoryChanged;
                _baseDirectoryWatcher.Renamed -= OnBaseDirectoryRenamed;
                _baseDirectoryWatcher.Error -= OnSolutionsFolderWatcherError;
                _baseDirectoryWatcher.Dispose();
                _baseDirectoryWatcher = null;
            }

            GC.SuppressFinalize(this);
        }

        private void ReleaseSolutionsWatcher()
        {
            if (_solutionsWatcher == null)
                return;

            _solutionsWatcher.EnableRaisingEvents = false;
            _solutionsWatcher.Created -= OnSolutionsFolderChanged;
            _solutionsWatcher.Deleted -= OnSolutionsFolderChanged;
            _solutionsWatcher.Changed -= OnSolutionsFolderChanged;
            _solutionsWatcher.Renamed -= OnSolutionsFolderRenamed;
            _solutionsWatcher.Error -= OnSolutionsFolderWatcherError;
            _solutionsWatcher.Dispose();
            _solutionsWatcher = null;
        }

        private async Task LoadTriggerOptionsAsync()
        {
            try
            {
                if (_configurationManager == null)
                    return;

                var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
                if (all?.Success != true || all.Data == null)
                    return;

                var triggerRows = all.Data
                    .OfType<TriggerBaseConfig>()
                    .Select(t => new SelectionOption
                    {
                        Id = t.ConfigId,
                        Name = string.IsNullOrWhiteSpace(t.ConfigName)
                            ? (t is ConfigBase cb ? cb.GetDisplayName() : t.ConfigId)
                            : t.ConfigName
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
                    .OrderBy(x => x.Name)
                    .ToList();

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                await dispatcher.InvokeAsync(() =>
                {
                    // 重建 ItemsSource 前先设置刷新守卫：阻止 WPF TwoWay 绑定
                    // 在 TriggerOptions.Clear() 后把 SelectedItem=null 回写到 TriggerConfigId。
                    foreach (var vm in DutViewModels) vm.BeginRefresh();
                    try
                    {
                        TriggerOptions.Clear();
                        TriggerOptions.Add(SelectionOption.Empty("未选择"));
                        foreach (var t in triggerRows)
                            TriggerOptions.Add(t);
                    }
                    finally
                    {
                        foreach (var vm in DutViewModels) vm.EndRefresh();
                    }

                    // ItemsSource 重建完毕，TriggerConfigId 已被守卫保护；
                    // 通过直接写 backing field 再触发 PropertyChanged 强制回显。
                    var orphanCleared = false;
                    foreach (var vm in DutViewModels)
                    {
                        var prevId = vm.Dut.TriggerConfigId?.Trim() ?? string.Empty;
                        vm.ApplyTriggerSelection(TriggerOptions);
                        if (!string.IsNullOrWhiteSpace(prevId) && string.IsNullOrWhiteSpace(vm.Dut.TriggerConfigId))
                            orphanCleared = true;
                    }

                    if (orphanCleared)
                        _ = PublishSoftwareConfigUpdatedAsync();
                });
            }
            catch
            {
                // 忽略：仅用于提供下拉选项，不应阻塞配置编辑
            }
        }
    }

    public sealed class SelectionOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static SelectionOption Empty(string name) => new SelectionOption { Id = string.Empty, Name = name };
    }

    /// <summary>
    /// DutConfig 的视图包装层。触发器下拉使用 SelectedItem 绑定，
    /// 避免 SelectedValue+SelectedValuePath 在 ItemsSource 重建后无法回显的问题。
    /// </summary>
    public sealed class DutViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private SelectionOption? _selectedTriggerOption;
        private bool _isRefreshing;

        public DutViewModel(DutConfig dut)
        {
            Dut = dut ?? throw new ArgumentNullException(nameof(dut));
        }

        /// <summary>
        /// 在重建 ItemsSource 前调用，阻止 WPF TwoWay 绑定把 SelectedItem=null 回写到 TriggerConfigId。
        /// </summary>
        internal void BeginRefresh() => _isRefreshing = true;

        /// <summary>在 ItemsSource 重建完毕后调用，解除守卫，随后可安全调用 ApplyTriggerSelection。</summary>
        internal void EndRefresh() => _isRefreshing = false;

        public DutConfig Dut { get; }

        public int Index => Dut.Index;

        public string WorkflowId
        {
            get => Dut.WorkflowId;
            set { if (!string.Equals(Dut.WorkflowId, value, StringComparison.Ordinal)) Dut.WorkflowId = value; }
        }

        public string WorkflowName
        {
            get => Dut.WorkflowName;
            set { if (!string.Equals(Dut.WorkflowName, value, StringComparison.Ordinal)) Dut.WorkflowName = value; }
        }

        /// <summary>
        /// 绑定到触发器 ComboBox.SelectedItem。setter 同步写入 DutConfig.TriggerConfigId / TriggerName。
        /// </summary>
        public SelectionOption? SelectedTriggerOption
        {
            get => _selectedTriggerOption;
            set
            {
                if (_isRefreshing) return;   // ItemsSource 重建中，忽略 WPF 的 null 回写
                if (ReferenceEquals(_selectedTriggerOption, value)) return;
                _selectedTriggerOption = value;
                OnPropertyChanged();
                Dut.TriggerConfigId = value?.Id ?? string.Empty;
                Dut.TriggerName = value?.Name ?? string.Empty;
            }
        }

        /// <summary>
        /// 数据源刷新后强制回显：直接写 backing field 再触发 PropertyChanged，
        /// 无论值是否变化都能让 WPF 重新匹配 SelectedItem。
        /// 若原 TriggerConfigId 在新选项中找不到对应项，则视为孤立引用并清除。
        /// </summary>
        internal void ApplyTriggerSelection(IEnumerable<SelectionOption> options)
        {
            var id = Dut.TriggerConfigId?.Trim() ?? string.Empty;
            var matched = string.IsNullOrWhiteSpace(id)
                ? null
                : options.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

            _selectedTriggerOption = matched;
            OnPropertyChanged(nameof(SelectedTriggerOption));

            if (!string.IsNullOrWhiteSpace(id) && matched == null)
            {
                Dut.TriggerConfigId = string.Empty;
                Dut.TriggerName = string.Empty;
            }
            else
            {
                var finalName = matched?.Name ?? string.Empty;
                if (!string.Equals(Dut.TriggerName, finalName, StringComparison.Ordinal))
                    Dut.TriggerName = finalName;
            }
        }
    }
}


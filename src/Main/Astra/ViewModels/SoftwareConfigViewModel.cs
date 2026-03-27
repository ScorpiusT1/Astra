using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Engine.Triggers;
using CommunityToolkit.Mvvm.ComponentModel;

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
        private bool _isDisposed;

        [ObservableProperty]
        private SoftwareConfig _config;

        [ObservableProperty]
        private ObservableCollection<SelectionOption> _workflowOptions = new ObservableCollection<SelectionOption>();

        [ObservableProperty]
        private ObservableCollection<SelectionOption> _triggerOptions = new ObservableCollection<SelectionOption>();

        public SoftwareConfigViewModel(SoftwareConfig config, IConfigurationManager configurationManager)
        {
            _config = config ?? new SoftwareConfig();
            _configurationManager = configurationManager;

            HookDutEvents();
            LoadWorkflowOptionsFromSolutions();
            InitializeWorkflowOptionsWatcher();
            _ = LoadTriggerOptionsAsync();
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
                var trigger = TriggerOptions.FirstOrDefault(x => string.Equals(x.Id, dut.TriggerConfigId, StringComparison.OrdinalIgnoreCase));
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

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

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
                TriggerOptions.Clear();
                TriggerOptions.Add(SelectionOption.Empty("未选择"));

                if (_configurationManager == null)
                    return;

                var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
                if (all?.Success != true || all.Data == null)
                    return;

                var triggers = all.Data
                    .OfType<TriggerConfig>()
                    .Select(t => new SelectionOption
                    {
                        Id = t.ConfigId,
                        Name = string.IsNullOrWhiteSpace(t.ConfigName) ? t.GetDisplayName() : t.ConfigName
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
                    .OrderBy(x => x.Name)
                    .ToList();

                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    foreach (var t in triggers)
                        TriggerOptions.Add(t);

                    if (Config?.Duts == null)
                        return;

                    foreach (var dut in Config.Duts)
                    {
                        if (dut == null)
                            continue;

                        var trigger = TriggerOptions.FirstOrDefault(x => string.Equals(x.Id, dut.TriggerConfigId, StringComparison.OrdinalIgnoreCase));
                        dut.TriggerName = trigger?.Name ?? string.Empty;
                    }
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
}


using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Engine.Triggers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.ViewModels
{
    public partial class SoftwareConfigViewModel : ObservableObject
    {
        private const string SolutionsFolderName = "Solutions";
        private const string SolutionFilePattern = "*.sol";
        private readonly IConfigurationManager? _configurationManager;

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
                var workflow = WorkflowOptions.FirstOrDefault(x => string.Equals(x.Id, dut.WorkflowId, StringComparison.OrdinalIgnoreCase));
                dut.WorkflowName = workflow?.Name ?? string.Empty;
                Config.CurrentWorkflowId = dut.WorkflowId ?? string.Empty;
                Config.CurrentWorkflowName = dut.WorkflowName ?? string.Empty;
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


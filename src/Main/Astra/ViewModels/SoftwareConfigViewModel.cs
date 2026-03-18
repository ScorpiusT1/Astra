using System;
using System.Collections.ObjectModel;
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

            BuildWorkflowOptionsFromConfig();
            _ = LoadTriggerOptionsAsync();
        }

        /// <summary>DUT 集合，便于 XAML 绑定</summary>
        public ObservableCollection<DutConfig> Duts => Config?.Duts;

        private void BuildWorkflowOptionsFromConfig()
        {
            WorkflowOptions.Clear();
            WorkflowOptions.Add(SelectionOption.Empty("未选择"));

            var names = Config?.Duts?
                .Select(d => d?.WorkflowName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList() ?? [];

            foreach (var name in names)
            {
                WorkflowOptions.Add(new SelectionOption { Id = name!, Name = name! });
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


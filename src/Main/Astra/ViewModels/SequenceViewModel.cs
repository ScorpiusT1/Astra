using Astra.Core.Nodes.Models;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.Models;
using Astra.Services;
using Astra.UI.Controls;
using Astra.UI.Services;
using Astra.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using Astra.Core.Nodes.Geometry;
using Astra.Core.Configuration.Abstractions;
using Astra.Configuration;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astra.UI.Helpers;
using System.Threading.Tasks;
using Astra.Core.Configuration;
using Astra.Core.Triggers;
using NavStack.Core;

namespace Astra.ViewModels
{
    public partial class SequenceViewModel : ObservableObject
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly Action<SoftwareConfig, ConfigChangeType> _softwareConfigChangedHandler;
        private string? _lastLoadedScriptPath;
      
        [ObservableProperty]
        private string _title = "序列配置";

        [ObservableProperty]
        private bool _isNavigating = false;

        /// <summary>
        /// 多流程编辑器 ViewModel（组合模式）
        /// </summary>
        [ObservableProperty]
        private MultiFlowEditorViewModel _multiFlowEditor;

        public SequenceViewModel(
            IFrameNavigationService navigationService,
            IPluginHost pluginHost,
            IEnumerable<IManifestSerializer> manifestSerializers,
            IWorkflowExecutionSessionService workflowExecutionSessionService,
            IConfigurationManager configurationManager,
            IManualBarcodeContext manualBarcodeContext,
            IScanModeState scanModeState)
        {
            _ = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _ = manualBarcodeContext ?? throw new ArgumentNullException(nameof(manualBarcodeContext));
            _ = scanModeState ?? throw new ArgumentNullException(nameof(scanModeState));

            // 创建 MultiFlowEditorViewModel 实例
            MultiFlowEditor = new MultiFlowEditorViewModel(pluginHost, manifestSerializers, workflowExecutionSessionService, manualBarcodeContext, scanModeState);
            
            Debug.WriteLine("[SequenceViewModel] 已创建 MultiFlowEditorViewModel");

            // 首次进入时加载一次当前保存脚本
            InitializeSequencePage();

            // 工业化事件驱动：软件配置一旦保存，序列编辑器立即切换到当前保存脚本
            _softwareConfigChangedHandler = OnSoftwareConfigChanged;
            _configurationManager.Subscribe(_softwareConfigChangedHandler);
        }

        #region 私有方法

        /// <summary>
        /// 初始化序列页面
        /// </summary>
        private void InitializeSequencePage()
        {
            _ = AutoLoadSequenceFromSoftwareConfigAsync();
        }

        private async Task AutoLoadSequenceFromSoftwareConfigAsync()
        {
            try
            {
                string? preferredScriptPath = null;

                var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
                if (all?.Success == true && all.Data != null)
                {
                    var latestSoftwareConfig = all.Data
                        .OfType<SoftwareConfig>()
                        .OrderByDescending(cfg => cfg.UpdatedAt ?? DateTime.MinValue)
                        .ThenByDescending(cfg => cfg.CreatedAt)
                        .FirstOrDefault();

                    preferredScriptPath = latestSoftwareConfig?.CurrentWorkflowId;
                    if (string.IsNullOrWhiteSpace(preferredScriptPath) || !File.Exists(preferredScriptPath))
                    {
                        preferredScriptPath = (latestSoftwareConfig?.Duts ?? [])
                            .Select(dut => dut?.WorkflowId)
                            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && File.Exists(id));
                    }

                    // 兜底：如果最新配置未选脚本，则退回全局任意可用脚本
                    if (string.IsNullOrWhiteSpace(preferredScriptPath))
                    {
                        preferredScriptPath = all.Data
                            .OfType<SoftwareConfig>()
                            .SelectMany(cfg => cfg.Duts ?? [])
                            .Select(dut => dut?.WorkflowId)
                            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && File.Exists(id));
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!ShouldReloadScript(preferredScriptPath))
                        return;
                    MultiFlowEditor?.TryAutoLoadSequence(preferredScriptPath);
                    _lastLoadedScriptPath = NormalizePath(preferredScriptPath);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 根据软件配置自动加载序列失败: {ex.Message}");
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    MultiFlowEditor?.TryAutoLoadSequence();
                });
            }
        }

        private void OnSoftwareConfigChanged(SoftwareConfig config, ConfigChangeType changeType)
        {
            if (changeType != ConfigChangeType.Updated || config == null)
                return;

            var preferredScriptPath = ResolvePreferredScriptPath(config);
            if (!ShouldReloadScript(preferredScriptPath))
                return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                MultiFlowEditor?.TryAutoLoadSequence(preferredScriptPath);
                _lastLoadedScriptPath = NormalizePath(preferredScriptPath);
            });
        }

        private static string? ResolvePreferredScriptPath(SoftwareConfig? config)
        {
            if (config == null)
                return null;

            if (!string.IsNullOrWhiteSpace(config.CurrentWorkflowId) && File.Exists(config.CurrentWorkflowId))
                return config.CurrentWorkflowId;

            return (config.Duts ?? [])
                .Select(dut => dut?.WorkflowId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && File.Exists(id));
        }

        private bool ShouldReloadScript(string? preferredScriptPath)
        {
            var target = NormalizePath(preferredScriptPath);
            if (string.IsNullOrWhiteSpace(target))
                return false;

            var current = NormalizePath(MultiFlowEditor?.CurrentFilePath);
            var loaded = NormalizePath(_lastLoadedScriptPath);

            return !string.Equals(target, current, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(target, loaded, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _configurationManager.Unsubscribe(_softwareConfigChangedHandler);
        }

        #endregion
    }
}

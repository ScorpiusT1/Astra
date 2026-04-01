using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.UI.Helpers;
using NAudio.Gui;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Astra.Plugins.DataAcquisition.Views
{
    /// <summary>
    /// DataAcquisitionDeviceDebugView.xaml 的交互逻辑
    /// </summary>
    public partial class DataAcquisitionDeviceDebugView : UserControl
    {
        private DataAcquisitionDeviceDebugViewModel? viewModel => DataContext as DataAcquisitionDeviceDebugViewModel;
        private readonly Dictionary<int, Signal> _signalsByChannelId = new();

        public DataAcquisitionDeviceDebugView()
        {
            InitializeComponent();

            ApplyScottPlotStyle();
            DataContextChanged -= OnDataContextChanged;
            DataContextChanged += OnDataContextChanged;
            Unloaded -= OnUnloaded;
            Unloaded += OnUnloaded;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DataAcquisitionDeviceDebugViewModel oldVm)
            {
                oldVm.WaveformUpdated -= OnWaveformUpdated;
                oldVm.ChannelVisibilityChanged -= OnChannelVisibilityChanged;
                oldVm.DisableDebugOverrideMode();
            }

            if (e.NewValue is DataAcquisitionDeviceDebugViewModel newVm)
            {
                newVm.WaveformUpdated += OnWaveformUpdated;
                newVm.ChannelVisibilityChanged += OnChannelVisibilityChanged;
                newVm.EnableDebugOverrideMode();
            }
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.DisableDebugOverrideMode();
            }
        }

        private void OnWaveformUpdated(Dictionary<int, double[]> dataByChannel)
        {
            if (WaveformPlot == null)
                return;

            var plt = WaveformPlot.Plot;
            plt.Clear();
            _signalsByChannelId.Clear();
            ApplyScottPlotStyle();

            plt.Axes.Left.Label.Text = "幅值";
            plt.Axes.Bottom.Label.Text = "时间 (s)";
            plt.Axes.Margins(bottom: 0.05, left: 0.05, right: 0.02, top: 0.02);

            foreach (var kv in dataByChannel)
            {
                var channelId = kv.Key;
                var ys = kv.Value;
                if (ys == null || ys.Length == 0)
                    continue;

                double sampleRate = viewModel?.DebugSampleRate > 0
                    ? viewModel.DebugSampleRate
                    : (viewModel?.SampleRate > 0 ? viewModel.SampleRate : 1.0);
                string channelName = $"CH{channelId}";
                var vmChannel = viewModel?.Channels?.FirstOrDefault(c => c.ChannelId == channelId);
                if (vmChannel != null && !string.IsNullOrWhiteSpace(vmChannel.Name))
                    channelName = vmChannel.Name.Trim();

                Signal signal = plt.Add.Signal(ys, 1.0 / sampleRate);
                signal.LegendText = channelName;
                signal.IsVisible = vmChannel?.IsEnabled ?? true;

                if(vmChannel != null)
                {
                    signal.Color = vmChannel.Color;
                }
              
                _signalsByChannelId[channelId] = signal;
            }

            if (dataByChannel.Count > 0)
            {
                plt.Axes.AutoScale();
                plt.Legend.Alignment = Alignment.UpperRight;
            }

            WaveformPlot.Refresh();
        }

        private void OnChannelVisibilityChanged(int channelId, bool isEnabled)
        {
            if (WaveformPlot == null)
                return;

            if (_signalsByChannelId.TryGetValue(channelId, out var signal))
            {
                signal.IsVisible = isEnabled;
                WaveformPlot.Plot.Axes.AutoScale();
                WaveformPlot.Refresh();
            }
        }

        private void ApplyScottPlotStyle()
        {
            var styleOptions = ScottPlotStyleHelper.CreateThemeStyleOptions();
            ScottPlotStyleHelper.ApplyToPlotAndSubplots(WaveformPlot.Plot, WaveformPlot.Multiplot, styleOptions);
        }
    }
}

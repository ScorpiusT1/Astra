using Astra.Plugins.DataAcquisition.ViewModels;
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
        private DataAcquisitionDeviceDebugViewModel? ViewModel => DataContext as DataAcquisitionDeviceDebugViewModel;

        public DataAcquisitionDeviceDebugView()
        {
            InitializeComponent();

            SetFont(WaveformPlot);

            DataContextChanged -= OnDataContextChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DataAcquisitionDeviceDebugViewModel oldVm)
            {
                oldVm.WaveformUpdated -= OnWaveformUpdated;
            }

            if (e.NewValue is DataAcquisitionDeviceDebugViewModel newVm)
            {
                newVm.WaveformUpdated += OnWaveformUpdated;
            }
        }

        private void OnWaveformUpdated(Dictionary<int, double[]> dataByChannel)
        {
            if (WaveformPlot == null)
                return;

            var plt = WaveformPlot.Plot;
            plt.Clear();

            // 美化 ScottPlot 样式
            // 使用 UI 主题中的颜色
            var surfaceColor = (System.Windows.Media.Color)System.Windows.Application.Current.Resources["SurfaceColor"];
            var regionColor = (System.Windows.Media.Color)System.Windows.Application.Current.Resources["SecondaryRegionColor"];
            var borderColor = (System.Windows.Media.Color)System.Windows.Application.Current.Resources["BorderColor"];

            //plt.FigureBackground.Color = ScottPlot.Color.FromARGB(surfaceColor.A, surfaceColor.R, surfaceColor.G, surfaceColor.B);
            //plt.DataBackground.Color   = ScottPlot.Color.FromARGB(regionColor.A, regionColor.R, regionColor.G, regionColor.B);

            //plt.Grid.MajorLineColor = ScottPlot.Color.FromARGB(borderColor.A, borderColor.R, borderColor.G, borderColor.B);
            //plt.Grid.MajorLineWidth = 1;
            //// 次网格线使用更浅的背景色
            //plt.Grid.MinorLineColor = plt.Grid.MajorLineColor.WithAlpha(100);
            //plt.Grid.MinorLineWidth = 0.5f;
            //plt.Grid.MinorLineStyle = LineStyle.Dot;

            plt.Axes.Left.Label.Text = "幅值";
            plt.Axes.Bottom.Label.Text = "时间 (s)";
            plt.Axes.Margins(bottom: 0.05, left: 0.05, right: 0.02, top: 0.02);

            foreach (var kv in dataByChannel)
            {
                var channelId = kv.Key;
                var ys = kv.Value;
                if (ys == null || ys.Length == 0)
                    continue;

                double sampleRate = ViewModel?.DebugSampleRate > 0
                    ? ViewModel.DebugSampleRate
                    : (ViewModel?.SampleRate > 0 ? ViewModel.SampleRate : 1.0);
                string channelName = $"CH{channelId}";
                var vmChannel = ViewModel?.Channels?.FirstOrDefault(c => c.ChannelId == channelId);
                if (vmChannel != null && !string.IsNullOrWhiteSpace(vmChannel.Name))
                    channelName = vmChannel.Name;

                Signal signal = plt.Add.Signal(ys, 1.0 / sampleRate);
                signal.LegendText = channelName;
            }

            if (dataByChannel.Count > 0)
            {
                plt.Axes.AutoScale();
                plt.Legend.Alignment = Alignment.UpperRight;
            }

            WaveformPlot.Refresh();
        }

        private void SetFont(WpfPlot wpfPlot, string font = "微软雅黑")
        {
            var multiPlot = wpfPlot.Multiplot;

            if (multiPlot == null || multiPlot.Subplots == null)
            {
                return;
            }

            int count = multiPlot.Subplots.Count;

            for (int i = 0; i < count; i++)
            {
                var plot = multiPlot.GetPlot(i);
                plot.Font.Set(font);
            }
        }
    }
}

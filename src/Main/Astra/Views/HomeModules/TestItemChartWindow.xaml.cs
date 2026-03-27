using System.Windows;
using Astra.ViewModels.HomeModules;
using Astra.UI.Helpers;
using ScottPlot;
using ScottPlot.Plottables;

namespace Astra.Views.HomeModules
{
    public partial class TestItemChartWindow : Window
    {
        private readonly Crosshair _actualCrosshair;
        private readonly HorizontalLine _lowerLimitLine;
        private readonly HorizontalLine _upperLimitLine;

        public TestItemChartWindow()
        {
            InitializeComponent();

            ItemPlot.Plot.Axes.Bottom.Label.Text = "样本";
            ItemPlot.Plot.Axes.Left.Label.Text = "数值";
            ApplyItemPlotStyleToAllPlots();

            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RenderChart();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RenderChart();
        }

        private void RenderChart()
        {
            if (DataContext is not TestTreeNodeItem item)
                return;
        }

        private void ApplyItemPlotStyleToAllPlots()
        {
            var styleOptions = ScottPlotStyleHelper.CreateThemeStyleOptions();
            ScottPlotStyleHelper.ApplyToPlotAndSubplots(ItemPlot.Plot, ItemPlot.Multiplot, styleOptions);
        }
    }
}

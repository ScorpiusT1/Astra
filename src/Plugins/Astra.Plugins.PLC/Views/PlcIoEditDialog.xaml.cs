using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration.Enums;
using Astra.Plugins.PLC;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Providers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Astra.Plugins.PLC.Views
{
    public partial class PlcIoEditDialog : Window, INotifyPropertyChanged
    {
        private string _validationMessage = string.Empty;
        private DispatcherTimer? _heightRecalcTimer;
        private Action<PlcDeviceConfig?, ConfigChangeType>? _plcDeviceConfigHandler;

        public PlcIoEditDialog(IoPointModel source, string[] outputKeyOptions)
        {
            InitializeComponent();
            Draft = source.CreateSnapshot();
            OutputKeyOptions = outputKeyOptions ?? Array.Empty<string>();
            IoDataTypeValues = Enum.GetValues(typeof(PlcIODataType));
            DataContext = this;

            RefreshPlcDeviceNames();

            Draft.PropertyChanged += Draft_PropertyChanged;
            Closed += PlcIoEditDialog_OnClosed;
        }

        /// <summary>当前已加载 PLC 设备名称列表（与设备管理器中的设备一致；含当前 IO 已选但暂不在列表中的名称）。</summary>
        public ObservableCollection<string> PlcDeviceNames { get; } = new();

        public IoPointModel Draft { get; }

        public IoPointModel EditedIo => Draft;

        public Array IoDataTypeValues { get; }

        public string[] OutputKeyOptions { get; }

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void PlcIoEditDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            var workH = SystemParameters.WorkArea.Height;
            MaxHeight = workH * 0.92;
            ContentScroll.MaxHeight = Math.Max(220, workH * 0.86);

            _plcDeviceConfigHandler = OnPlcDeviceConfigChanged;
            var cm = PlcPlugin.GetConfigurationManager();
            if (cm != null)
            {
                cm.Subscribe<PlcDeviceConfig>(_plcDeviceConfigHandler);
            }
        }

        private void OnPlcDeviceConfigChanged(PlcDeviceConfig? cfg, ConfigChangeType changeType)
        {
            Dispatcher.BeginInvoke(new Action(RefreshPlcDeviceNames), DispatcherPriority.Normal);
        }

        private void RefreshPlcDeviceNames()
        {
            PlcDeviceNames.Clear();
            foreach (var n in PlcDeviceProvider.GetPlcDeviceNames())
            {
                PlcDeviceNames.Add(n);
            }

            var current = Draft.PlcDeviceName?.Trim();
            if (!string.IsNullOrEmpty(current) &&
                !PlcDeviceNames.Any(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)))
            {
                PlcDeviceNames.Add(current);
            }
        }

        private void Draft_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ScheduleWindowHeightRecalc();
        }

        private void ScheduleWindowHeightRecalc()
        {
            _heightRecalcTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            _heightRecalcTimer.Stop();
            _heightRecalcTimer.Tick -= HeightRecalcTimer_OnTick;
            _heightRecalcTimer.Tick += HeightRecalcTimer_OnTick;
            _heightRecalcTimer.Start();
        }

        private void HeightRecalcTimer_OnTick(object? sender, EventArgs e)
        {
            if (_heightRecalcTimer != null)
            {
                _heightRecalcTimer.Stop();
                _heightRecalcTimer.Tick -= HeightRecalcTimer_OnTick;
            }
            if (!IsLoaded)
            {
                return;
            }

            // 区块显示/隐藏后让窗口按内容重算高度（WPF 默认不总会收缩）
            Height = double.NaN;
            SizeToContent = SizeToContent.Height;
        }

        private void PlcIoEditDialog_OnClosed(object? sender, EventArgs e)
        {
            var cm = PlcPlugin.GetConfigurationManager();
            if (cm != null && _plcDeviceConfigHandler != null)
            {
                cm.Unsubscribe<PlcDeviceConfig>(_plcDeviceConfigHandler);
            }

            _plcDeviceConfigHandler = null;

            Draft.PropertyChanged -= Draft_PropertyChanged;
            if (_heightRecalcTimer != null)
            {
                _heightRecalcTimer.Stop();
                _heightRecalcTimer.Tick -= HeightRecalcTimer_OnTick;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var result = Draft.Validate();
            if (!result.Success)
            {
                ValidationMessage = result.Message ?? "保存失败，请检查输入。";
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Cancel_Click(sender, e);
        }

        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

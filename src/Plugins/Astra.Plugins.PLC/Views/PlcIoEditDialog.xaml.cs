using Astra.Plugins.PLC.Configs;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Astra.Plugins.PLC.Views
{
    public partial class PlcIoEditDialog : Window, INotifyPropertyChanged
    {
        private string _validationMessage = string.Empty;

        public PlcIoEditDialog(IoPointModel source, string[] outputKeyOptions)
        {
            InitializeComponent();
            Draft = source.CreateSnapshot();
            OutputKeyOptions = outputKeyOptions ?? Array.Empty<string>();
            IoDataTypeValues = Enum.GetValues(typeof(PlcIODataType));
            DataContext = this;
        }

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

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

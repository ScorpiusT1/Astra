using NavStack.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Astra.ViewModels
{
    public class TreeNodeViewModel : INotifyPropertyChanged
    {
        private string _header;
        private bool _isExpanded;
        private bool _isSelected;
        private string _icon;
        private object _tag;
        private string _navigationKey;
        private NavigationParameters _navigationParameters;
        private string _ownerKey;
        private string _nodeId;
        private bool _showAddButton;
        private string _addDeviceType;

        public string Header
        {
            get => _header;
            set { _header = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public object Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(); }
        }

        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public string NavigationKey
        {
            get => _navigationKey;
            set { _navigationKey = value; OnPropertyChanged(); }
        }

        public NavigationParameters NavigationParameters
        {
            get => _navigationParameters;
            set { _navigationParameters = value; OnPropertyChanged(); }
        }

        public string OwnerKey
        {
            get => _ownerKey;
            set { _ownerKey = value; OnPropertyChanged(); }
        }

        public string NodeId
        {
            get => _nodeId;
            set { _nodeId = value; OnPropertyChanged(); }
        }

        public bool ShowAddButton
        {
            get => _showAddButton;
            set { _showAddButton = value; OnPropertyChanged(); }
        }

        public string AddDeviceType
        {
            get => _addDeviceType;
            set { _addDeviceType = value; OnPropertyChanged(); }
        }

        public bool HasNavigation => !string.IsNullOrWhiteSpace(NavigationKey);

        public ObservableCollection<TreeNodeViewModel> Children { get; set; }

        public TreeNodeViewModel()
        {
            Children = new ObservableCollection<TreeNodeViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

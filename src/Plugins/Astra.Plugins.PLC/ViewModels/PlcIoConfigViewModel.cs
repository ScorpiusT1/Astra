using Astra.Core.Configuration.Abstractions;
using Astra.Core.Constants;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Views;
using Astra.UI.Behaviors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Astra.Plugins.PLC.ViewModels
{
    public class PlcIoConfigViewModel : ObservableObject
    {
        private readonly IOConfig _config;
        private readonly RelayCommand _removeSelectedIoCommand;
        private readonly RelayCommand _copySelectedIoCommand;
        private readonly RelayCommand _pasteToSelectedIoCommand;
        private IoPointModel? _selectedIo;
        private IoPointModel? _copiedIoSnapshot;
        private string _searchText = string.Empty;
        private string _saveStatus = "就绪";
        private bool _isEditing;

        public PlcIoConfigViewModel(IConfig config)
        {
            _config = config as IOConfig ?? throw new ArgumentException("配置类型必须为 IOConfig", nameof(config));
            _removeSelectedIoCommand = new RelayCommand(RemoveSelectedIo, () => HasSelection);
            _copySelectedIoCommand = new RelayCommand(CopySelectedIo, () => HasSelection);
            _pasteToSelectedIoCommand = new RelayCommand(PasteToSelectedIo, () => _copiedIoSnapshot != null);
            IoListView = CollectionViewSource.GetDefaultView(_config.IOs);
            IoListView.Filter = FilterIo;
            _config.IOs.CollectionChanged += (_, _) =>
            {
                SafeRefreshIoList();
                OnPropertyChanged(nameof(HasSelection));
            };
        }

        public string ConfigName
        {
            get => _config.ConfigName ?? string.Empty;
            set
            {
                if (_config.ConfigName != value)
                {
                    _config.ConfigName = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<IoPointModel> IOs => _config.IOs;
        public ICollectionView IoListView { get; }

        public IoPointModel? SelectedIo
        {
            get => _selectedIo;
            set
            {
                if (SetProperty(ref _selectedIo, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    _removeSelectedIoCommand.NotifyCanExecuteChanged();
                    _copySelectedIoCommand.NotifyCanExecuteChanged();
                    _pasteToSelectedIoCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedIo != null;
        public bool HasCopiedIo => _copiedIoSnapshot != null;

        public bool EnableDragReorder
        {
            get => _config.EnableDragReorder;
            set
            {
                if (_config.EnableDragReorder != value)
                {
                    _config.EnableDragReorder = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    SafeRefreshIoList();
                }
            }
        }

        public string SaveStatus
        {
            get => _saveStatus;
            private set => SetProperty(ref _saveStatus, value);
        }

        public string[] OutputKeyOptions { get; } = { "Value", "Result", "RawValue", "State" };

        public IRelayCommand AddIoCommand => new RelayCommand(AddIo);
        public IRelayCommand RemoveSelectedIoCommand => _removeSelectedIoCommand;
        public IRelayCommand<IoPointModel> EditIoCommand => new RelayCommand<IoPointModel>(EditIo);
        public IRelayCommand<IoPointModel> DeleteIoCommand => new RelayCommand<IoPointModel>(DeleteIo);
        public IRelayCommand CopySelectedIoCommand => _copySelectedIoCommand;
        public IRelayCommand PasteToSelectedIoCommand => _pasteToSelectedIoCommand;
        public IRelayCommand<DataGridRowReorderInfo> ReorderIoCommand => new RelayCommand<DataGridRowReorderInfo>(ReorderIo);
        public IRelayCommand CloseEditorCommand => new RelayCommand(() => { });
        public IRelayCommand SaveIoCommand => new RelayCommand(() => { });
        public IRelayCommand CancelEditCommand => new RelayCommand(() => { });

        private void AddIo()
        {
            var io = new IoPointModel
            {
                Name = GenerateNextIoName(),
                DataType = PlcIODataType.Auto,
                IsEnabled = true,
                MonitorOnHome = false,
                Scale = 1.0,
                Offset = 0.0
            };
            _config.IOs.Add(io);
            SelectedIo = io;
            SaveStatus = "已新增，待编辑";
            SafeRefreshIoList();
        }

        private void RemoveSelectedIo()
        {
            if (SelectedIo == null)
            {
                return;
            }

            _config.IOs.Remove(SelectedIo);
            SelectedIo = _config.IOs.Count > 0 ? _config.IOs[^1] : null;
            SaveStatus = "已删除";
            SafeRefreshIoList();
        }

        private void EditIo(IoPointModel? io)
        {
            if (io == null)
            {
                return;
            }

            SelectedIo = io;

            // 表格内 CheckBox 等绑定在部分时机未提交到模型；打开对话框前先冲刷 UI 绑定
            Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var dialog = new PlcIoEditDialog(io, OutputKeyOptions)
            {
                Owner = Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                io.RestoreFrom(dialog.EditedIo);
                SaveStatus = $"已保存 ✓ {DateTime.Now:HH:mm:ss}";
                SafeRefreshIoList();
            }
        }

        private void DeleteIo(IoPointModel? io)
        {
            if (io == null)
            {
                return;
            }

            if (ReferenceEquals(SelectedIo, io))
            {
                SelectedIo = null;
            }

            _config.IOs.Remove(io);
            SafeRefreshIoList();
        }

        private bool FilterIo(object obj)
        {
            if (obj is not IoPointModel io)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            var keyword = SearchText.Trim();
            return (io.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (io.Address?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (io.Tag?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (io.PipeLabel?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void CopySelectedIo()
        {
            if (SelectedIo == null)
            {
                return;
            }

            _copiedIoSnapshot = SelectedIo.CreateSnapshot();
            SaveStatus = "已复制";
            OnPropertyChanged(nameof(HasCopiedIo));
            _pasteToSelectedIoCommand.NotifyCanExecuteChanged();
        }

        private void PasteToSelectedIo()
        {
            if (_copiedIoSnapshot == null)
            {
                SaveStatus = "请先复制一个IO配置";
                return;
            }

            var snapshot = _copiedIoSnapshot.CreateSnapshot();
            snapshot.Name = GenerateUniqueName(snapshot.Name);
            _config.IOs.Add(snapshot);
            SelectedIo = snapshot;
            SaveStatus = "已粘贴并新增 IO";
            SafeRefreshIoList();
        }

        private string GenerateUniqueName(string baseName)
        {
            var seed = string.IsNullOrWhiteSpace(baseName) ? "NewIO" : baseName.Trim();
            var used = _config.IOs
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => i.Name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!used.Contains(seed))
            {
                return seed;
            }

            var index = 1;
            string candidate;
            do
            {
                candidate = $"{seed} - Copy {index}";
                index++;
            } while (used.Contains(candidate));

            return candidate;
        }

        public bool TryMoveIo(IoPointModel source, IoPointModel? target)
        {
            if (!EnableDragReorder)
            {
                return false;
            }

            if (source == null || !_config.IOs.Contains(source))
            {
                return false;
            }

            var sourceIndex = _config.IOs.IndexOf(source);
            if (sourceIndex < 0)
            {
                return false;
            }

            if (target == null)
            {
                var lastIndex = _config.IOs.Count - 1;
                if (sourceIndex == lastIndex)
                {
                    return false;
                }

                _config.IOs.Move(sourceIndex, lastIndex);
                SafeRefreshIoList();
                return true;
            }

            if (!_config.IOs.Contains(target))
            {
                return false;
            }

            var targetIndex = _config.IOs.IndexOf(target);
            if (targetIndex < 0 || targetIndex == sourceIndex)
            {
                return false;
            }

            _config.IOs.Move(sourceIndex, targetIndex);
            SafeRefreshIoList();
            return true;
        }

        private void ReorderIo(DataGridRowReorderInfo? info)
        {
            if (info?.SourceItem is not IoPointModel source)
            {
                return;
            }

            var target = info.TargetItem as IoPointModel;
            TryMoveIo(source, target);
        }

        private void SafeRefreshIoList()
        {
            if (IoListView == null)
            {
                return;
            }

            if (IoListView is IEditableCollectionView editableView &&
                (editableView.IsAddingNew || editableView.IsEditingItem))
            {
                Application.Current?.Dispatcher.BeginInvoke(
                    new Action(SafeRefreshIoList),
                    DispatcherPriority.Background);
                return;
            }

            IoListView.Refresh();
        }

        private string GenerateNextIoName()
        {
            const string prefix = AstraSharedConstants.PlcDefaults.NewIoNamePrefix;
            var used = _config.IOs
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => i.Name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var index = 1;
            while (used.Contains($"{prefix}{index}"))
            {
                index++;
            }

            return $"{prefix}{index}";
        }
    }
}


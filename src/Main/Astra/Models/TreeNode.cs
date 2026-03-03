using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Models
{
    public partial class TreeNode : ObservableObject
    {
        [ObservableProperty]
        private string _id;

        [ObservableProperty]
        private string _header;

        [ObservableProperty]
        private string? _icon;

        [ObservableProperty]
        private int _order;

        [ObservableProperty]
        private Type _viewModelType;

        [ObservableProperty]
        private Type _viewType;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _canExpanded;

        [ObservableProperty]
        private object _tag;

        [ObservableProperty]
        private bool _showAddButton;

        [ObservableProperty]
        private bool _showDeleteButton;

        [ObservableProperty]
        private bool _isDragging;

        [ObservableProperty]
        private bool _canDrop;

        /// <summary>
        /// 调试树中使用：表示设备是否在线，用于状态指示灯。
        /// 对于非设备节点可以保持默认值。
        /// </summary>
        [ObservableProperty]
        private bool _isOnline;

        public Type? ConfigType { get; set; }
        
        public IConfig? Config { get; set; }

        public TreeNode Parent { get; set; }    

        [ObservableProperty]
        public ObservableCollection<TreeNode> _children = new ObservableCollection<TreeNode>();

        public TreeNode()
        {
            Id = Guid.NewGuid().ToString();
        }

    }
}

﻿﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NavStack.Modularity
{
    /// <summary>
    /// 导航菜单项
    /// </summary>
    public class NavigationMenuItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isVisible = true;

        public string Title { get; set; }
        public string Icon { get; set; }
        public string NavigationKey { get; set; }
        public int Order { get; set; }
        public string Group { get; set; }
        public string ModuleName { get; set; }
        public string Description { get; set; }
        
        /// <summary>
        /// 访问此菜单项所需的最低权限级别（0=所有用户，1=操作员，2=工程师，3=管理员）
        /// </summary>
        public int RequiredPermissionLevel { get; set; } = 0;
        
        /// <summary>
        /// 权限验证失败时的提示消息
        /// </summary>
        public string PermissionDeniedMessage { get; set; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// 菜单项是否可见（用于权限控制）
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public List<NavigationMenuItem> SubItems { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

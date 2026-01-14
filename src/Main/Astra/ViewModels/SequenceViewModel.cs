using Astra.Core.Nodes.Models;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.Models;
using Astra.Services;
using Astra.UI.Controls;
using Astra.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NavStack.Core;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Geometry;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astra.UI.Helpers;

namespace Astra.ViewModels
{
    public partial class SequenceViewModel : ObservableObject
    {
        private readonly IFrameNavigationService _navigationService;
      
        [ObservableProperty]
        private string _title = "序列配置";

        [ObservableProperty]
        private bool _isNavigating = false;

        /// <summary>
        /// 多流程编辑器 ViewModel（组合模式）
        /// </summary>
        [ObservableProperty]
        private MultiFlowEditorViewModel _multiFlowEditor;

        public SequenceViewModel(IFrameNavigationService navigationService, IPluginHost pluginHost, IManifestSerializer manifestSerializer)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            
            // 创建 MultiFlowEditorViewModel 实例
            MultiFlowEditor = new MultiFlowEditorViewModel(pluginHost, manifestSerializer);
            
            Debug.WriteLine("[SequenceViewModel] 已创建 MultiFlowEditorViewModel");
        }

       
        /// <summary>
        /// 订阅导航事件
        /// </summary>
        private void SubscribeToNavigationEvents()
        {
            // 订阅导航开始事件
            _navigationService.Navigating += OnNavigationStarted;
            
            // 订阅导航完成事件
            _navigationService.Navigated += OnNavigationCompleted;
            
            // 订阅导航失败事件
            _navigationService.NavigationFailed += OnNavigationFailed;
            
            Debug.WriteLine("[SequenceViewModel] 已订阅导航事件");
        }

        /// <summary>
        /// 取消订阅导航事件
        /// </summary>
        public void UnsubscribeFromNavigationEvents()
        {
            if (_navigationService != null)
            {
                _navigationService.Navigating -= OnNavigationStarted;
                _navigationService.Navigated -= OnNavigationCompleted;
                _navigationService.NavigationFailed -= OnNavigationFailed;
                
                Debug.WriteLine("[SequenceViewModel] 已取消订阅导航事件");
            }
        }

        #region 导航事件处理

        /// <summary>
        /// 导航开始事件处理
        /// </summary>
        private void OnNavigationStarted(object sender, NavigationEventArgs e)
        {
            Debug.WriteLine($"[SequenceViewModel] 导航开始事件触发");
            Debug.WriteLine($"[SequenceViewModel] 目标页面: {e.Context?.NavigationUri}");
            Debug.WriteLine($"[SequenceViewModel] 导航模式: {e.Context?.NavigationMode}");
            
            // 设置导航状态
            IsNavigating = true;
            
            // 这里可以添加导航开始时的逻辑
            // 例如：显示加载指示器、禁用某些操作等
            
            // 示例：如果是导航到当前页面，可以做一些特殊处理
            if (e.Context?.NavigationUri == NavigationKeys.Sequence)
            {
                Debug.WriteLine("[SequenceViewModel] 正在导航到序列编辑页面");
                // 可以在这里做一些准备工作
            }
        }

        /// <summary>
        /// 导航完成事件处理
        /// </summary>
        private void OnNavigationCompleted(object sender, NavigationEventArgs e)
        {
            Debug.WriteLine($"[SequenceViewModel] 导航完成事件触发");
            Debug.WriteLine($"[SequenceViewModel] 当前页面: {e.Context?.NavigationUri}");
            Debug.WriteLine($"[SequenceViewModel] 导航模式: {e.Context?.NavigationMode}");
            
            // 清除导航状态
            IsNavigating = false;
            
            // 这里可以添加导航完成时的逻辑
            // 例如：隐藏加载指示器、启用操作、刷新数据等
            
            // 示例：如果导航到了当前页面，可以做一些初始化工作
            if (e.Context?.NavigationUri == NavigationKeys.Sequence)
            {
                Debug.WriteLine("[SequenceViewModel] 已成功导航到序列编辑页面");
                // 可以在这里做一些页面初始化工作
                InitializeSequencePage();
            }
        }

        /// <summary>
        /// 导航失败事件处理
        /// </summary>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            Debug.WriteLine($"[SequenceViewModel] 导航失败事件触发");
            Debug.WriteLine($"[SequenceViewModel] 失败原因: {e.Exception?.Message}");
            Debug.WriteLine($"[SequenceViewModel] 目标页面: {e.Context?.NavigationUri}");
            
            // 清除导航状态
            IsNavigating = false;
            
            // 这里可以添加导航失败时的逻辑
            // 例如：显示错误消息、记录日志等
            
            // 示例：处理导航失败
            HandleNavigationFailure(e.Exception, e.Context?.NavigationUri);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化序列页面
        /// </summary>
        private void InitializeSequencePage()
        {
            
        }

        /// <summary>
        /// 处理导航失败
        /// </summary>
        private void HandleNavigationFailure(Exception exception, string targetPage)
        {
            Debug.WriteLine($"[SequenceViewModel] 处理导航失败: {targetPage}");
            
            // 这里可以添加失败处理逻辑
            // 例如：显示错误消息、尝试重新导航等
            
            if (exception != null)
            {
                Debug.WriteLine($"[SequenceViewModel] 异常详情: {exception}");
            }
        }

        #endregion


        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            UnsubscribeFromNavigationEvents();
        }
    }
}

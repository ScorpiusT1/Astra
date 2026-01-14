using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Astra.Core.Nodes.Models;
using System.Diagnostics;
using System.Linq;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 自定义 ItemsControl，确保 ItemTemplateSelector 在容器生成时正确应用
    /// 解决初始加载时模板选择器未正确应用的问题
    /// </summary>
    public class TemplateSelectorItemsControl : ItemsControl
    {
        protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            
            // 当 ItemsSource 变化时，监听容器生成完成事件
            if (newValue != null)
            {
                ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
            }
        }
        
        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                // 容器生成完成，强制应用模板
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyTemplatesToAllContainers();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            
            // 如果设置了 ItemTemplateSelector，确保 ContentPresenter 正确应用
            if (element is ContentPresenter contentPresenter && ItemTemplateSelector != null)
            {
                // 立即尝试应用模板
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyTemplateToContainer(contentPresenter, item);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // 也监听 Loaded 事件，确保在容器完全加载后再次应用
                contentPresenter.Loaded += (s, e) =>
                {
                    ApplyTemplateToContainer(contentPresenter, item);
                };
            }
        }
        
        /// <summary>
        /// 为所有容器应用模板
        /// </summary>
        private void ApplyTemplatesToAllContainers()
        {
            if (ItemTemplateSelector == null || ItemsSource == null)
                return;
            
            foreach (var item in ItemsSource)
            {
                var container = ItemContainerGenerator.ContainerFromItem(item);
                if (container is ContentPresenter contentPresenter)
                {
                    ApplyTemplateToContainer(contentPresenter, item);
                }
            }
        }
        
        /// <summary>
        /// 应用模板到容器
        /// </summary>
        private void ApplyTemplateToContainer(ContentPresenter contentPresenter, object item)
        {
            DataTemplate template = null;
            
            // 优先使用 ItemTemplateSelector
            if (ItemTemplateSelector != null)
            {
                template = ItemTemplateSelector.SelectTemplate(item, contentPresenter);
                if (template != null)
                {
                    // 清除可能存在的 ContentTemplateSelector，避免冲突
                    contentPresenter.ContentTemplateSelector = null;
                    
                    // 设置 ContentTemplate
                    contentPresenter.ContentTemplate = template;
                    
                    Debug.WriteLine($"[TemplateSelectorItemsControl] 为节点应用模板（来自选择器）: {item?.GetType().Name ?? "null"}, NodeType: {(item as Node)?.NodeType ?? "null"}, 模板: {template.GetType().Name}");
                }
                else
                {
                    Debug.WriteLine($"[TemplateSelectorItemsControl] 警告: 模板选择器返回 null，节点: {item?.GetType().Name ?? "null"}, NodeType: {(item as Node)?.NodeType ?? "null"}");
                }
            }
            else if (ItemTemplate != null)
            {
                // 如果没有选择器，回退到使用 ItemTemplate
                contentPresenter.ContentTemplateSelector = null;
                contentPresenter.ContentTemplate = ItemTemplate;
                
                Debug.WriteLine($"[TemplateSelectorItemsControl] 为节点应用默认模板: {item?.GetType().Name ?? "null"}, NodeType: {(item as Node)?.NodeType ?? "null"}");
            }
        }
    }
}


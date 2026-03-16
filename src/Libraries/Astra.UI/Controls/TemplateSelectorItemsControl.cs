using Astra.Core.Nodes.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 自定义 ItemsControl，确保 ItemTemplateSelector 在容器生成时正确应用
    /// 解决初始加载时模板选择器未正确应用的问题
    /// </summary>
    /// <summary>
    /// 自定义 ItemsControl，确保 ItemTemplateSelector 在容器生成时正确应用
    /// 解决初始加载时模板选择器未正确应用的问题
    /// </summary>
    public class TemplateSelectorItemsControl : ItemsControl
    {
        // 记录已经应用过模板的容器，防止重复应用
        private readonly HashSet<ContentPresenter> _appliedContainers = new HashSet<ContentPresenter>();

        protected override void OnItemsSourceChanged(
            System.Collections.IEnumerable oldValue,
            System.Collections.IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            // 重置已应用集合
            _appliedContainers.Clear();

            // 先取消旧订阅，防止重复注册累积
            ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;

            if (newValue != null)
            {
                ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
            }
        }

        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                // 容器全部生成完毕后取消订阅，避免反复触发
                ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;

                Dispatcher.BeginInvoke(new Action(ApplyTemplatesToAllContainers),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (element is ContentPresenter contentPresenter && ItemTemplateSelector != null)
            {
                // 只用 Loaded 事件，去掉 Dispatcher.BeginInvoke，避免双重触发
                contentPresenter.Loaded += OnContentPresenterLoaded;
            }
        }

        private void OnContentPresenterLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ContentPresenter contentPresenter)
                return;

            // 立即取消订阅，确保只执行一次
            contentPresenter.Loaded -= OnContentPresenterLoaded;

            // 防止重复应用
            if (_appliedContainers.Contains(contentPresenter))
                return;

            var item = contentPresenter.Content;
            ApplyTemplateToContainer(contentPresenter, item);
        }

        /// <summary>
        /// 为所有容器应用模板（由 StatusChanged 触发）
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
                    // 跳过已经由 Loaded 事件处理过的容器
                    if (!_appliedContainers.Contains(contentPresenter))
                    {
                        ApplyTemplateToContainer(contentPresenter, item);
                    }
                }
            }
        }

        /// <summary>
        /// 应用模板到容器（带重复应用保护）
        /// </summary>
        private void ApplyTemplateToContainer(ContentPresenter contentPresenter, object item)
        {
            DataTemplate template = null;

            if (ItemTemplateSelector != null)
            {
                template = ItemTemplateSelector.SelectTemplate(item, contentPresenter);
            }
            else if (ItemTemplate != null)
            {
                template = ItemTemplate;
            }

            if (template == null)
            {
                Debug.WriteLine($"[TemplateSelectorItemsControl] 警告: 模板选择器返回 null，节点: " +
                                $"{item?.GetType().Name ?? "null"}, " +
                                $"NodeType: {(item as Node)?.NodeType ?? "null"}");
                return;
            }

            // 如果当前模板已经是目标模板，跳过，避免触发不必要的视觉树重建
            if (ReferenceEquals(contentPresenter.ContentTemplate, template))
            {
                _appliedContainers.Add(contentPresenter);
                return;
            }

            // ⚠️ 关键修复：不要先 set ContentTemplateSelector = null
            // 直接设置 ContentTemplate，WPF 会自动处理冲突
            // 多余的 ContentTemplateSelector = null 会额外触发一次模板重建
            contentPresenter.ContentTemplate = template;

            // 标记为已应用
            _appliedContainers.Add(contentPresenter);

            Debug.WriteLine($"[TemplateSelectorItemsControl] 为节点应用模板: " +
                            $"{item?.GetType().Name ?? "null"}, " +
                            $"NodeType: {(item as Node)?.NodeType ?? "null"}, " +
                            $"模板: {template.GetType().Name}");
        }
    }
}


using System.Windows;
using System.Windows.Controls;
using Astra.Core.Nodes.Models;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 根据节点类型选择不同的 DataTemplate
    /// WorkflowReferenceNode 使用特殊样式，其他节点使用默认样式
    /// </summary>
    public class NodeTypeToDataTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// WorkflowReferenceNode 的模板
        /// </summary>
        public DataTemplate WorkflowReferenceNodeTemplate { get; set; }

        /// <summary>
        /// 默认节点模板
        /// </summary>
        public DataTemplate DefaultNodeTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is Node node)
            {
                // 如果是 WorkflowReferenceNode，使用特殊模板
                bool isWorkflowReference = node.NodeType == "WorkflowReferenceNode" || item is WorkflowReferenceNode;
                
                if (isWorkflowReference)
                {
                    if (WorkflowReferenceNodeTemplate != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NodeTypeToDataTemplateSelector] 选择 WorkflowReferenceNode 模板，节点: {node.Name}, NodeType: {node.NodeType}");
                        return WorkflowReferenceNodeTemplate;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[NodeTypeToDataTemplateSelector] WorkflowReferenceNodeTemplate 为 null，使用默认模板，节点: {node.Name}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[NodeTypeToDataTemplateSelector] 选择默认模板，节点: {node.Name}, NodeType: {node.NodeType}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NodeTypeToDataTemplateSelector] 项目不是 Node 类型: {item?.GetType().Name}");
            }

            // 其他情况使用默认模板
            if (DefaultNodeTemplate == null)
            {
                System.Diagnostics.Debug.WriteLine("[NodeTypeToDataTemplateSelector] 警告: DefaultNodeTemplate 为 null");
            }
            return DefaultNodeTemplate;
        }
    }
}


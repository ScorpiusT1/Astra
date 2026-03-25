using Astra.Core.Nodes.Models;
using System.Windows;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 节点工厂抽象：将工具项转换为可落盘的 Node 实例。
    /// </summary>
    public interface INodeFactory
    {
        bool TryCreate(IToolItem toolItem, Point position, out Node node);
    }
}

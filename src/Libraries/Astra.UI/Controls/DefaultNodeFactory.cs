using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
using Astra.UI.Models;
using System;
using System.Collections.Concurrent;
using System.Windows;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 默认节点工厂：封装类型解析与实例化细节，避免在控件层散落反射逻辑。
    /// </summary>
    public sealed class DefaultNodeFactory : INodeFactory
    {
        private readonly ConcurrentDictionary<Type, Func<Node>> _activatorCache = new ConcurrentDictionary<Type, Func<Node>>();
        private readonly ConcurrentDictionary<string, Type> _resolvedTypeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        public bool TryCreate(IToolItem toolItem, Point position, out Node node)
        {
            node = null;
            if (toolItem?.NodeType == null)
            {
                return false;
            }

            var nodeType = ResolveNodeType(toolItem.NodeType);
            if (nodeType == null || !typeof(Node).IsAssignableFrom(nodeType) || nodeType.IsAbstract)
            {
                return false;
            }

            var creator = _activatorCache.GetOrAdd(nodeType, BuildActivator);
            if (creator == null)
            {
                return false;
            }

            node = creator.Invoke();
            if (node == null)
            {
                return false;
            }

            node.Name = toolItem.Name ?? "未命名节点";
            node.NodeType = nodeType.Name;
            node.Position = new Point2D(position.X, position.Y);

            if (!string.IsNullOrWhiteSpace(toolItem.Description))
            {
                node.Description = toolItem.Description;
            }

            if (toolItem is ToolItem typedTool && !string.IsNullOrWhiteSpace(typedTool.IconCode))
            {
                node.Icon = typedTool.IconCode;
            }

            node.OnPlacedFromToolbox();

            return true;
        }

        private Type ResolveNodeType(object nodeTypeValue)
        {
            if (nodeTypeValue is Type type)
            {
                return type;
            }

            if (nodeTypeValue is not string typeName || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return _resolvedTypeCache.GetOrAdd(typeName, ResolveNodeTypeByName);
        }

        private static Type ResolveNodeTypeByName(string typeName)
        {
            var resolved = Type.GetType(typeName, false);
            if (resolved != null)
            {
                return resolved;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(typeName, false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static Func<Node> BuildActivator(Type nodeType)
        {
            try
            {
                var ctor = nodeType.GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                {
                    return null;
                }

                var newExpr = System.Linq.Expressions.Expression.New(ctor);
                var castExpr = System.Linq.Expressions.Expression.Convert(newExpr, typeof(Node));
                return System.Linq.Expressions.Expression.Lambda<Func<Node>>(castExpr).Compile();
            }
            catch
            {
                return null;
            }
        }
    }
}

using Astra.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Utilities
{
    /// <summary>
    /// 树节点操作类
    /// </summary>
    public class TreeNodeHelper
    {

        /// <summary>
        /// 获取所有树的叶子节点
        /// </summary>
        /// <param name="nodes">树节点集合</param>
        /// <returns>所有叶子节点列表</returns>
        public static List<TreeNode> GetAllLeafNodes(IEnumerable<TreeNode> nodes)
        {
            if (nodes == null)
                return new List<TreeNode>();

            var leafNodes = new List<TreeNode>();

            foreach (var node in nodes)
            {
                TraverseForLeaves(node, leafNodes);
            }

            return leafNodes;
        }

        /// <summary>
        /// 获取第一个叶子节点（Children为空）
        /// </summary>
        /// <param name="nodes">树节点集合</param>
        /// <returns>第一个叶子节点，其Children为空列表</returns>
        public static TreeNode? GetFirstLeafNode(IEnumerable<TreeNode> nodes)
        {
            if (nodes == null)
                return null;

            foreach (var node in nodes)
            {
                var result = FindFirstLeaf(node);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 递归遍历收集叶子节点
        /// </summary>
        private static void TraverseForLeaves(TreeNode node, List<TreeNode> leafNodes)
        {
            if (node == null)
                return;

            // 如果没有子节点或子节点列表为空，则为叶子节点
            if (node.Children == null || node.Children.Count == 0)
            {
                leafNodes.Add(node);
                return;
            }

            // 递归遍历所有子节点
            foreach (var child in node.Children)
            {
                TraverseForLeaves(child, leafNodes);
            }
        }

        /// <summary>
        /// 获取树的所有叶子节点
        /// </summary>
        /// <param name="root">树的根节点</param>
        /// <returns>所有叶子节点列表</returns>
        public static IEnumerable<TreeNode> GetAllLeafNodes(TreeNode root)
        {
            if (root == null)
                return new List<TreeNode>();

            var leafNodes = new List<TreeNode>();
            TraverseForLeaves(root, leafNodes);
            return leafNodes;
        }

        /// <summary>
        /// 获取展开后的第一个叶子节点（深度优先）
        /// </summary>
        /// <param name="root">树的根节点</param>
        /// <returns>第一个叶子节点，其Children为空列表</returns>
        public static TreeNode? GetFirstLeafNode(TreeNode root)
        {
            if (root == null)
                return null;

            return FindFirstLeaf(root);
        }

        /// <summary>
        /// 递归查找第一个叶子节点
        /// </summary>
        private static TreeNode FindFirstLeaf(TreeNode node)
        {
            // 如果没有子节点或子节点列表为空，则为叶子节点
            if (node.Children == null || node.Children.Count == 0)
            {
                // 确保Children为空列表而不是null
                node.Children = new ObservableCollection<TreeNode>();
                return node;
            }

            // 深度优先：从第一个子节点开始查找
            foreach (var child in node.Children)
            {
                var result = FindFirstLeaf(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }

}

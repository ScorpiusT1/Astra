using Astra.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Utilities
{
    public class NodeAutoNaming
    {
        private const string BASE_NAME = "新建节点";

        public string GenerateUniqueName(TreeNode parent)
        {
            // ✅ 使用父节点的名称作为基础名称
            string baseName = !string.IsNullOrWhiteSpace(parent?.Header) 
                ? parent.Header 
                : BASE_NAME;

            var siblings = parent?.Children?.ToList() ?? new List<TreeNode>();

            if (siblings.Count == 0)
            {
                return $"{baseName}1";
            }

            // 提取所有已使用的编号（基于父节点名称）
            var usedNumbers = ExtractUsedNumbers(baseName, siblings.Select(n => n.Header));

            // 查找最小可用编号
            int availableNumber = FindSmallestAvailableNumber(usedNumbers);

            return $"{baseName}{availableNumber}";
        }

        public string GenerateRootName(ObservableCollection<TreeNode> rootNodes)
        {
            if (rootNodes == null || rootNodes.Count == 0)
            {
                return $"{BASE_NAME}1";
            }

            var usedNumbers = ExtractUsedNumbers(BASE_NAME, rootNodes.Select(n => n.Header));
            int availableNumber = FindSmallestAvailableNumber(usedNumbers);

            return $"{BASE_NAME}{availableNumber}";
        }

        /// <summary>
        /// 基于已存在的名称列表生成唯一名称
        /// </summary>
        public string GenerateUniqueNameFromList(IEnumerable<string> existingNames)
        {
            return GenerateUniqueNameFromList(BASE_NAME, existingNames);
        }

        /// <summary>
        /// 基于已存在的名称列表和基础名称生成唯一名称
        /// </summary>
        public string GenerateUniqueNameFromList(string baseName, IEnumerable<string> existingNames)
        {
            if (existingNames == null || !existingNames.Any())
            {
                return $"{baseName}1";
            }

            var usedNumbers = ExtractUsedNumbers(baseName, existingNames);
            int availableNumber = FindSmallestAvailableNumber(usedNumbers);

            return $"{baseName}{availableNumber}";
        }

        // 提取已使用的编号（基于基础名称）
        private HashSet<int> ExtractUsedNumbers(string baseName, IEnumerable<string> names)
        {
            var usedNumbers = new HashSet<int>();

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // 检查名称是否以基础名称开头
                if (name.StartsWith(baseName))
                {
                    string numberPart = name.Substring(baseName.Length);

                    if (int.TryParse(numberPart, out int number))
                    {
                        usedNumbers.Add(number);
                    }
                }
            }

            return usedNumbers;
        }

        // 查找最小可用编号（从1开始）
        private int FindSmallestAvailableNumber(HashSet<int> usedNumbers)
        {
            int number = 1;

            while (usedNumbers.Contains(number))
            {
                number++;
            }

            return number;
        }
    }
}

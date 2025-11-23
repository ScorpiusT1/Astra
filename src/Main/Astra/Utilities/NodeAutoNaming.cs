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
            var siblings = parent?.Children?.ToList() ?? new List<TreeNode>();

            if (siblings.Count == 0)
            {
                return $"{BASE_NAME}1";
            }

            // 提取所有已使用的编号
            var usedNumbers = ExtractUsedNumbers(siblings.Select(n => n.Header));

            // 查找最小可用编号
            int availableNumber = FindSmallestAvailableNumber(usedNumbers);

            return $"{BASE_NAME}{availableNumber}";
        }

        public string GenerateRootName(ObservableCollection<TreeNode> rootNodes)
        {
            if (rootNodes == null || rootNodes.Count == 0)
            {
                return $"{BASE_NAME}1";
            }

            var usedNumbers = ExtractUsedNumbers(rootNodes.Select(n => n.Header));
            int availableNumber = FindSmallestAvailableNumber(usedNumbers);

            return $"{BASE_NAME}{availableNumber}";
        }

        // 提取已使用的编号
        private HashSet<int> ExtractUsedNumbers(IEnumerable<string> names)
        {
            var usedNumbers = new HashSet<int>();

            foreach (var name in names)
            {
                if (name.StartsWith(BASE_NAME))
                {
                    string numberPart = name.Substring(BASE_NAME.Length);

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

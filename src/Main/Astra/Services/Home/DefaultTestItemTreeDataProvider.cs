using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Astra.ViewModels.HomeModules;

namespace Astra.Services.Home
{
    /// <summary>
    /// 默认示例数据（流程节点 + 子测试项），可替换为真实数据源。
    /// </summary>
    public sealed class DefaultTestItemTreeDataProvider : ITestItemTreeDataProvider
    {
        public Task<IReadOnlyList<TestTreeNodeItem>> LoadRootNodesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var flowNodeNames = new[]
            {
                "初始化节点",
                "采集节点",
                "分析节点",
                "结果判定节点"
            };

            var list = new List<TestTreeNodeItem>();

            foreach (var nodeName in flowNodeNames)
            {
                var root = new TestTreeNodeItem
                {
                    Name = nodeName,
                    Status = "Ready",
                    IsRoot = true
                };

                root.Children.Add(new TestTreeNodeItem
                {
                    Name = $"{nodeName}-电压测试",
                    Status = "Pass",
                    TestTime = DateTime.Now.AddSeconds(-20),
                    ActualValue = 12.15,
                    LowerLimit = 11.80,
                    UpperLimit = 12.30
                });
                root.Children.Add(new TestTreeNodeItem
                {
                    Name = $"{nodeName}-电流测试",
                    Status = "Running",
                    TestTime = DateTime.Now.AddSeconds(-8),
                    ActualValue = 0.82,
                    LowerLimit = 0.70,
                    UpperLimit = 0.90
                });
                root.Children.Add(new TestTreeNodeItem
                {
                    Name = $"{nodeName}-通讯测试",
                    Status = "Ready",
                    TestTime = DateTime.Now,
                    ActualValue = 1.00,
                    LowerLimit = 1.00,
                    UpperLimit = 1.00
                });

                list.Add(root);
            }

            return Task.FromResult<IReadOnlyList<TestTreeNodeItem>>(list);
        }
    }
}

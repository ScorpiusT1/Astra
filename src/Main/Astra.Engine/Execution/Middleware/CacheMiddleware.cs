using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 缓存中间件
    /// 缓存节点执行结果，避免重复执行相同输入
    /// </summary>
    public class CacheMiddleware : INodeMiddleware
    {
        private readonly Dictionary<string, (ExecutionResult result, DateTime expiry)> _cache
            = new Dictionary<string, (ExecutionResult, DateTime)>();
        private readonly int _cacheSeconds;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheSeconds">缓存有效期（秒）</param>
        public CacheMiddleware(int cacheSeconds = 60)
        {
            _cacheSeconds = cacheSeconds;
        }

        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public async Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next)
        {
            var cacheKey = GenerateCacheKey(node, context);

            // 检查缓存
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now < cached.expiry)
                {
                    Console.WriteLine($"💾 [缓存命中] 节点 {node.Name}");
                    return cached.result;
                }
                else
                {
                    _cache.Remove(cacheKey);
                }
            }

            // 执行并缓存
            var result = await next(cancellationToken);

            if (result.Success)
            {
                _cache[cacheKey] = (result, DateTime.Now.AddSeconds(_cacheSeconds));
                Console.WriteLine($"💾 [缓存保存] 节点 {node.Name}, 有效期: {_cacheSeconds}秒");
            }

            return result;
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        private string GenerateCacheKey(Node node, NodeContext context)
        {
            // 简单实现：使用节点ID和输入数据的哈希
            return $"{node.Id}_{GetHashCode(context.InputData)}";
        }

        /// <summary>
        /// 计算字典的哈希码
        /// </summary>
        private int GetHashCode(Dictionary<string, object> data)
        {
            return data.Aggregate(0, (hash, kvp) => hash ^ kvp.Key.GetHashCode() ^ (kvp.Value?.GetHashCode() ?? 0));
        }
    }
}


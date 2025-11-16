using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.NodeExecutor
{
    /// <summary>
    /// 默认节点执行器 - 支持中间件和拦截器
    /// 负责执行节点的核心逻辑，通过中间件管道和拦截器提供横切关注点支持
    /// </summary>
    public class DefaultNodeExecutor : INodeExecutor
    {
        private readonly List<INodeMiddleware> _middlewares = new List<INodeMiddleware>();
        private readonly List<INodeInterceptor> _interceptors = new List<INodeInterceptor>();

        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        /// <returns>当前执行器实例，支持链式调用</returns>
        public INodeExecutor Use(INodeMiddleware middleware)
        {
            _middlewares.Add(middleware);
            return this;
        }

        /// <summary>
        /// 添加拦截器
        /// </summary>
        /// <param name="interceptor">拦截器实例</param>
        /// <returns>当前执行器实例，支持链式调用</returns>
        public INodeExecutor AddInterceptor(INodeInterceptor interceptor)
        {
            _interceptors.Add(interceptor);
            return this;
        }

        /// <summary>
        /// 执行节点
        /// </summary>
        /// <param name="node">要执行的节点</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public async Task<ExecutionResult> ExecuteAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            // 核心执行委托，允许中间件替换 CancellationToken
            Func<CancellationToken, Task<ExecutionResult>> core = async (CancellationToken token) =>
            {
                // 执行拦截器：前置
                foreach (var interceptor in _interceptors)
                {
                    await interceptor.OnBeforeExecuteAsync(node, context, token);
                }

                ExecutionResult result;
                try
                {
                    // 执行节点核心逻辑
                    result = await node.InvokeExecuteCoreAsync(context, token);
                }
                catch (Exception ex)
                {
                    // 执行拦截器：异常
                    foreach (var interceptor in _interceptors)
                    {
                        await interceptor.OnExceptionAsync(node, ex, token);
                    }
                    throw;
                }

                // 执行拦截器：后置
                foreach (var interceptor in _interceptors)
                {
                    await interceptor.OnAfterExecuteAsync(node, result, token);
                }

                return result;
            };

            // 反向构建中间件链（可替换令牌）
            Func<CancellationToken, Task<ExecutionResult>> pipeline = core;
            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = _middlewares[i];
                var next = pipeline;
                pipeline = (tok) => middleware.InvokeAsync(node, context, tok, next);
            }

            // 执行管道
            return await pipeline(cancellationToken);
        }
    }
}


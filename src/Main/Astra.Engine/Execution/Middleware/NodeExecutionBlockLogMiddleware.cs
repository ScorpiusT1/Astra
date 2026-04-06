using Astra.Core.Logs;
using Astra.Core.Nodes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 将单次执行内的节点日志缓冲为一块，与 <see cref="Logging.ExecutionRunLogSession"/> 配合。
    /// 须作为管道最外层（在 <see cref="ExecutorBuilder"/> 中最后注册）。
    /// </summary>
    public sealed class NodeExecutionBlockLogMiddleware : INodeMiddleware
    {
        public async Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next)
        {
            if (node == null || context == null)
                return await next(cancellationToken).ConfigureAwait(false);

            var session = context.GetMetadata<IExecutionRunLogSession>(ExecutionContextMetadataKeys.ExecutionRunLogSession, null);
            if (session == null)
                return await next(cancellationToken).ConfigureAwait(false);

            session.PushNodeScope(node);
            try
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                session.PopNodeScope();
            }
        }
    }
}

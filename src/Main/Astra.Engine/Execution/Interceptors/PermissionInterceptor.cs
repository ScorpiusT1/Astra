using Astra.Core.Nodes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Interceptors
{
    /// <summary>
    /// 权限检查拦截器
    /// 在执行节点前检查用户权限，确保只有有权限的用户才能执行节点
    /// </summary>
    public class PermissionInterceptor : INodeInterceptor
    {
        private readonly IPermissionService _permissionService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="permissionService">权限服务实例</param>
        public PermissionInterceptor(IPermissionService permissionService)
        {
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        }

        /// <summary>
        /// 节点执行前调用，检查权限
        /// </summary>
        public async Task OnBeforeExecuteAsync(Node node, NodeContext context, CancellationToken cancellationToken)
        {
            // 检查当前用户是否有权限执行此节点
            var userId = context.GlobalVariables.TryGetValue("userId", out var id) ? id.ToString() : null;

            if (!await _permissionService.HasPermissionAsync(userId, node.NodeType))
            {
                throw new UnauthorizedAccessException($"用户 {userId} 无权执行节点 {node.Name}");
            }
        }

        /// <summary>
        /// 节点执行后调用
        /// </summary>
        public Task OnAfterExecuteAsync(Node node, ExecutionResult result, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 节点执行异常时调用
        /// </summary>
        public Task OnExceptionAsync(Node node, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 权限服务接口
    /// 定义权限检查的抽象接口
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// 检查用户是否有权限执行指定类型的节点
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="nodeType">节点类型</param>
        /// <returns>如果有权限返回true，否则返回false</returns>
        Task<bool> HasPermissionAsync(string userId, string nodeType);
    }
}


namespace Astra.Core.Access.Models
{
    /// <summary>
    /// 用户角色枚举
    /// 定义系统中可用的用户角色类型
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// 管理员角色 - 拥有所有权限，包括用户管理、权限管理等
        /// </summary>
        Administrator,

        /// <summary>
        /// 工程师角色 - 拥有设备配置、测试执行等操作权限
        /// </summary>
        Engineer,

        /// <summary>
        /// 操作员角色 - 拥有基本的测试执行和查看权限
        /// </summary>
        Operator,
    }
}


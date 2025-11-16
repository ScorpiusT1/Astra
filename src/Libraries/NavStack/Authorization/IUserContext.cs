using System.Collections.Generic;
using System.Threading.Tasks;

namespace NavStack.Authorization
{
	/// <summary>
	/// 用户上下文
	/// </summary>
	public interface IUserContext
	{
		/// <summary>
		/// 用户唯一标识
		/// </summary>
		string UserId { get; }

		/// <summary>
		/// 用户所属角色列表（只读）
		/// </summary>
		IReadOnlyCollection<string> Roles { get; }

		/// <summary>
		/// 用户声明（只读）
		/// </summary>
		IReadOnlyCollection<string> Claims { get; }

		/// <summary>
		/// 判断用户是否属于指定角色
		/// </summary>
		/// <param name="role">角色名称</param>
		/// <returns>是否在该角色中</returns>
		bool IsInRole(string role);
	}
}



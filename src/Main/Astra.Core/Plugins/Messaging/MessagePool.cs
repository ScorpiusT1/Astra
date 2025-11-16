using System;
using System.Collections.Concurrent;

namespace Astra.Core.Plugins.Messaging
{
	/// <summary>
	/// 轻量对象池，用于复用 RPC 请求对象，减少 GC 压力。
	/// </summary>
	internal static class MessagePool
	{
		private static readonly ConcurrentBag<object> _rpcPool = new();

		/// <summary>
		/// 从池中租用一个 <see cref="RpcRequest{T}"/> 实例（或创建新实例）。
		/// </summary>
		public static RpcRequest<T> RentRpc<T>()
		{
			if (_rpcPool.TryTake(out var obj) && obj is RpcRequest<T> reused)
			{
				reused.RequestId = null;
				reused.Data = default;
				return reused;
			}
			return new RpcRequest<T>();
		}

		/// <summary>
		/// 归还 <see cref="RpcRequest{T}"/> 实例到对象池。
		/// </summary>
		public static void ReturnRpc<T>(RpcRequest<T> request)
		{
			if (request == null) return;
			request.RequestId = null;
			request.Data = default;
			_rpcPool.Add(request);
		}
	}
}


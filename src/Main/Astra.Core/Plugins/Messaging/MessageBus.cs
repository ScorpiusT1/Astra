using System.Collections.Concurrent;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Concurrency;
using Astra.Core.Plugins.Performance;
using Astra.Core.Plugins.Services;

namespace Astra.Core.Plugins.Messaging
{
	/// <summary>
	/// 轻量级消息总线实现。
	/// - 支持基于主题与泛型消息类型的发布/订阅；
	/// - 可选接入 <c>IConcurrencyManager</c> 控制并发；
	/// - 通过 <c>IPerformanceMonitor</c> 记录发布/订阅等指标。
	/// </summary>
	public class MessageBus : IMessageBus
    {
        private readonly ConcurrentDictionary<string, List<Delegate>> _subscribers = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests = new();
		private readonly IConcurrencyManager _concurrency;
		private readonly IPerformanceMonitor _metrics;

		/// <summary>
		/// 默认构造（不启用并发与指标），用于简化集成/测试。
		/// </summary>
		public MessageBus()
		{
		}

		/// <summary>
		/// 带依赖解析的构造，可自动获取并发管理器与性能监控器（可选）。
		/// </summary>
		public MessageBus(IServiceRegistry services)
		{
			// 可选依赖
			_concurrency = services?.ResolveOrDefault<IConcurrencyManager>();
			_metrics = services?.ResolveOrDefault<IPerformanceMonitor>();
		}

		public void Subscribe<T>(string topic, Action<T> handler)
        {
            var handlers = _subscribers.GetOrAdd(topic, _ => new List<Delegate>());
            lock (handlers)
            {
                handlers.Add(handler);
            }

			// 指标：订阅计数事件
			_metrics?.RecordOperation($"subscribe:{topic}:{typeof(T).Name}", TimeSpan.FromMilliseconds(0.1));
        }

		/// <summary>
		/// 取消订阅指定主题的处理器。
		/// </summary>
		/// <typeparam name="T">消息类型</typeparam>
		/// <param name="topic">主题</param>
		/// <param name="handler">处理委托</param>
		public void Unsubscribe<T>(string topic, Action<T> handler)
        {
            if (_subscribers.TryGetValue(topic, out var handlers))
            {
                lock (handlers)
                {
                    handlers.Remove(handler);
                }
            }

			_metrics?.RecordOperation($"unsubscribe:{topic}:{typeof(T).Name}", TimeSpan.FromMilliseconds(0.1));
        }

		/// <summary>
		/// 向指定主题发布消息（并发执行所有订阅处理器）。
		/// </summary>
		/// <typeparam name="T">消息类型</typeparam>
		/// <param name="topic">主题</param>
		/// <param name="message">消息实例</param>
		/// <returns>异步任务</returns>
		public async Task PublishAsync<T>(string topic, T message)
        {
            if (!_subscribers.TryGetValue(topic, out var handlers))
                return;

            List<Delegate> handlersCopy;
            lock (handlers)
            {
                handlersCopy = new List<Delegate>(handlers);
            }

			var tasks = new List<Task>();
            foreach (var handler in handlersCopy)
            {
                if (handler is Action<T> action)
                {
					if (_concurrency == null)
					{
						tasks.Add(Task.Run(() => action(message)));
					}
					else
					{
						tasks.Add(_concurrency.ExecuteWithConcurrencyControl(
							() => Task.Run(() => action(message)),
							operationName: $"msg:{topic}"));
					}
                }
            }

			var sw = System.Diagnostics.Stopwatch.StartNew();
			await Task.WhenAll(tasks);
			sw.Stop();
			_metrics?.RecordOperation($"publish:{topic}:{typeof(T).Name}", sw.Elapsed);
        }

		/// <summary>
		/// 发送请求消息并等待响应（内部使用对象池复用请求对象）。
		/// </summary>
		/// <typeparam name="TRequest">请求类型</typeparam>
		/// <typeparam name="TResponse">响应类型</typeparam>
		/// <param name="topic">主题</param>
		/// <param name="request">请求对象</param>
		/// <param name="timeout">超时时间</param>
		/// <returns>响应对象</returns>
		public async Task<TResponse> RequestAsync<TRequest, TResponse>(
			string topic,
			TRequest request,
			TimeSpan timeout)
		{
			var requestId = Guid.NewGuid().ToString();
			var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

			_pendingRequests[requestId] = tcs;

			// 使用对象池复用 RpcRequest<T>
			var pooled = MessagePool.RentRpc<TRequest>();
			pooled.RequestId = requestId;
			pooled.Data = request;
			try
			{
				await PublishAsync(topic, pooled);
			}
			finally
			{
				MessagePool.ReturnRpc(pooled);
			}

			var timeoutTask = Task.Delay(timeout);
			var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

			if (completedTask == timeoutTask)
			{
				_pendingRequests.TryRemove(requestId, out _);
				throw new TimeoutException($"RPC request timed out after {timeout}");
			}

			return (TResponse)await tcs.Task.ConfigureAwait(false);
		}

		/// <summary>
		/// 完成一次 RPC 请求，将响应设置给等待方。
		/// </summary>
		/// <param name="requestId">请求标识</param>
		/// <param name="response">响应对象</param>
        public void CompleteRequest(string requestId, object response)
        {
            if (_pendingRequests.TryRemove(requestId, out var tcs))
            {
                tcs.SetResult(response);
            }
        }
    }
}

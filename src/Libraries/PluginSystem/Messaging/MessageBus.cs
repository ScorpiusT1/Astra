using System.Collections.Concurrent;

namespace Addins.Messaging
{
    public class MessageBus : IMessageBus
    {
        private readonly ConcurrentDictionary<string, List<Delegate>> _subscribers = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests = new();

        public void Subscribe<T>(string topic, Action<T> handler)
        {
            var handlers = _subscribers.GetOrAdd(topic, _ => new List<Delegate>());
            lock (handlers)
            {
                handlers.Add(handler);
            }
        }

        public void Unsubscribe<T>(string topic, Action<T> handler)
        {
            if (_subscribers.TryGetValue(topic, out var handlers))
            {
                lock (handlers)
                {
                    handlers.Remove(handler);
                }
            }
        }

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
                    tasks.Add(Task.Run(() => action(message)));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(
            string topic,
            TRequest request,
            TimeSpan timeout)
        {
            var requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<object>();

            _pendingRequests[requestId] = tcs;

            await PublishAsync(topic, new RpcRequest<TRequest>
            {
                RequestId = requestId,
                Data = request
            });

            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingRequests.TryRemove(requestId, out _);
                throw new TimeoutException($"RPC request timed out after {timeout}");
            }

            return (TResponse)await tcs.Task;
        }

        public void CompleteRequest(string requestId, object response)
        {
            if (_pendingRequests.TryRemove(requestId, out var tcs))
            {
                tcs.SetResult(response);
            }
        }
    }
}

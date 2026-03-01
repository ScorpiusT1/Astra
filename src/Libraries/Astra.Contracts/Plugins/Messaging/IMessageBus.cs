namespace Astra.Core.Plugins.Messaging
{
    /// <summary>
    /// 消息总线接口（共享契约：小而稳定）
    /// </summary>
    public interface IMessageBus
    {
        void Subscribe<T>(string topic, Action<T> handler);
        void Unsubscribe<T>(string topic, Action<T> handler);
        Task PublishAsync<T>(string topic, T message);
        Task<TResponse> RequestAsync<TRequest, TResponse>(string topic, TRequest request, TimeSpan timeout);
    }

    public class RpcRequest<T>
    {
        public string RequestId { get; set; }
        public T Data { get; set; }
    }
}


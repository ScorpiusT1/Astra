using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Messaging
{
    /// <summary>
    /// 消息总线实现 - 开放封闭原则
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

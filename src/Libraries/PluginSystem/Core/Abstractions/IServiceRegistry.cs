
using Addins.Services;

namespace Addins.Core.Abstractions
{
    public interface IServiceRegistry
    {
        void AddDecorator<TService, TDecorator>() where TDecorator : TService;
        void AddPostProcessor<TService>(Func<TService, TService> processor);
        IEnumerable<Type> GetServicesByMetadata(string key, object value);
        void RegisterNamed<TService>(string name, TService instance);
        void RegisterOpenGeneric(Type openGenericServiceType, Type openGenericImplementationType, ServiceLifetime lifetime);
        void RegisterScoped<TService, TImplementation>() where TImplementation : TService;
        void RegisterSingleton<TService, TImplementation>() where TImplementation : TService;
        void RegisterSingleton<TService>(TService instance);
        void RegisterTransient<TService, TImplementation>() where TImplementation : TService;
        void RegisterWithMetadata<TService, TImplementation>(ServiceLifetime lifetime, Dictionary<string, object> metadata) where TImplementation : TService;
        object Resolve(Type serviceType);
        TService Resolve<TService>();
        IEnumerable<TService> ResolveAll<TService>();
        TService ResolveNamed<TService>(string name);
        void Unregister<TService>();
        void UnregisterByType(Type serviceType);
    }
}
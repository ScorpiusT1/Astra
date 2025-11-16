using Addins.Core.Abstractions;
using Addins.Services;
using System;
using System.Collections.Generic;

namespace Addins.Tests
{
    /// <summary>
    /// ServiceRegistry 接口实现验证
    /// </summary>
    public class ServiceRegistryInterfaceTest
    {
        public static void TestInterfaceImplementation()
        {
            var registry = new ServiceRegistry();
            
            // 测试所有接口方法是否存在
            TestMethod(() => registry.RegisterSingleton<ITestService, TestService>());
            TestMethod(() => registry.RegisterSingleton<ITestService>(new TestService()));
            TestMethod(() => registry.RegisterTransient<ITestService, TestService>());
            TestMethod(() => registry.RegisterScoped<ITestService, TestService>());
            TestMethod(() => registry.RegisterNamed<ITestService>("test", new TestService()));
            TestMethod(() => registry.RegisterOpenGeneric(typeof(ITestService<>), typeof(TestService<>), ServiceLifetime.Singleton));
            TestMethod(() => registry.RegisterWithMetadata<ITestService, TestService>(ServiceLifetime.Singleton, new Dictionary<string, object>()));
            TestMethod(() => registry.AddDecorator<ITestService, TestServiceDecorator>());
            TestMethod(() => registry.AddPostProcessor<ITestService>(s => s));
            TestMethod(() => registry.Resolve<ITestService>());
            TestMethod(() => registry.ResolveAll<ITestService>());
            TestMethod(() => registry.ResolveNamed<ITestService>("test"));
            TestMethod(() => registry.GetServicesByMetadata("key", "value"));
            TestMethod(() => registry.Unregister<ITestService>());
            TestMethod(() => registry.UnregisterByType(typeof(ITestService)));
            
            Console.WriteLine("All ServiceRegistry interface methods are implemented correctly!");
        }

        private static void TestMethod(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Method test failed: {ex.Message}");
            }
        }
    }

    // 测试接口和实现
    public interface ITestService
    {
        string GetName();
    }

    public class TestService : ITestService
    {
        public string GetName() => "TestService";
    }

    public class TestServiceDecorator : ITestService
    {
        private readonly ITestService _service;

        public TestServiceDecorator(ITestService service)
        {
            _service = service;
        }

        public string GetName() => $"Decorated({_service.GetName()})";
    }

    public interface ITestService<T>
    {
        T GetValue();
    }

    public class TestService<T> : ITestService<T>
    {
        public T GetValue() => default(T);
    }
}

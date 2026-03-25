using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Astra.UI.Services
{
    /// <summary>
    /// 默认 ItemsSource 解析器：集中管理解析策略，并缓存反射元数据。
    /// </summary>
    public sealed class DefaultItemsSourceResolver : IItemsSourceResolver
    {
        public static DefaultItemsSourceResolver Instance { get; } = new DefaultItemsSourceResolver();

        private readonly ConcurrentDictionary<string, MemberInfo> _memberCache = new ConcurrentDictionary<string, MemberInfo>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<Type, Func<IItemsSourceProvider>> _providerFactoryCache = new ConcurrentDictionary<Type, Func<IItemsSourceProvider>>();

        private DefaultItemsSourceResolver()
        {
        }

        public bool TryResolve(ItemsSourceAttribute attribute, object targetObject, out IEnumerable itemsSource)
        {
            itemsSource = null;
            if (attribute == null)
            {
                return false;
            }

            if (attribute.ItemsSourceType?.IsEnum == true)
            {
                itemsSource = Enum.GetValues(attribute.ItemsSourceType);
                return itemsSource != null;
            }

            if (attribute.ItemsSourceType != null && !string.IsNullOrEmpty(attribute.Path))
            {
                var member = ResolveMember(attribute.ItemsSourceType, attribute.Path, true);
                itemsSource = ReadMemberValue(member, null) as IEnumerable;
                if (itemsSource != null)
                {
                    return true;
                }
            }

            if (attribute.ItemsSourceType != null && typeof(IItemsSourceProvider).IsAssignableFrom(attribute.ItemsSourceType))
            {
                var factory = _providerFactoryCache.GetOrAdd(attribute.ItemsSourceType, BuildProviderFactory);
                var provider = factory?.Invoke();
                itemsSource = provider?.GetItemsSource();
                if (itemsSource != null)
                {
                    return true;
                }
            }

            if (attribute.StaticType != null)
            {
                if (!string.IsNullOrEmpty(attribute.MethodName))
                {
                    var member = ResolveMember(attribute.StaticType, attribute.MethodName, true);
                    itemsSource = ReadMemberValue(member, null) as IEnumerable;
                    if (itemsSource != null)
                    {
                        return true;
                    }
                }
                else if (!string.IsNullOrEmpty(attribute.PropertyName))
                {
                    var member = ResolveMember(attribute.StaticType, attribute.PropertyName, true);
                    itemsSource = ReadMemberValue(member, null) as IEnumerable;
                    if (itemsSource != null)
                    {
                        return true;
                    }
                }
            }

            if (targetObject != null)
            {
                var targetType = targetObject.GetType();
                if (!string.IsNullOrEmpty(attribute.MethodName))
                {
                    var member = ResolveMember(targetType, attribute.MethodName, false);
                    itemsSource = ReadMemberValue(member, targetObject) as IEnumerable;
                    if (itemsSource != null)
                    {
                        return true;
                    }
                }
                else if (!string.IsNullOrEmpty(attribute.PropertyName))
                {
                    var member = ResolveMember(targetType, attribute.PropertyName, false);
                    itemsSource = ReadMemberValue(member, targetObject) as IEnumerable;
                    if (itemsSource != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Func<IItemsSourceProvider> BuildProviderFactory(Type providerType)
        {
            try
            {
                var ctor = providerType.GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                {
                    return null;
                }

                return () => ctor.Invoke(null) as IItemsSourceProvider;
            }
            catch
            {
                return null;
            }
        }

        private MemberInfo ResolveMember(Type type, string memberName, bool isStatic)
        {
            if (type == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            var key = $"{type.AssemblyQualifiedName}|{memberName}|{isStatic}";
            return _memberCache.GetOrAdd(key, _ =>
            {
                var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                var method = type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    return method;
                }

                return type.GetProperty(memberName, flags);
            });
        }

        private static object ReadMemberValue(MemberInfo member, object target)
        {
            try
            {
                return member switch
                {
                    MethodInfo methodInfo => methodInfo.Invoke(target, null),
                    PropertyInfo propertyInfo => propertyInfo.GetValue(target),
                    FieldInfo fieldInfo => fieldInfo.GetValue(target),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }
    }
}

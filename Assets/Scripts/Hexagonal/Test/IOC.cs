// IoCContainer.cs
using System;
using System.Collections.Generic;

public class IoCContainer
{
    // 单例实例存储
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
    
    // 类型工厂存储（避免反射的核心）
    private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();
    
    // 类型映射关系
    private readonly Dictionary<Type, Type> _mappings = new Dictionary<Type, Type>();

    // 注册类型映射（泛型方法避免反射）
    public void Register<TInterface, TImplementation>() 
        where TImplementation : TInterface, new()
    {
        _mappings[typeof(TInterface)] = typeof(TImplementation);
        
        // 预编译工厂方法
        _factories[typeof(TInterface)] = () => new TImplementation();
    }

    // 注册单例实例
    public void RegisterSingleton<T>(T instance)
    {
        _singletons[typeof(T)] = instance;
    }

    // 注册自定义工厂方法
    public void RegisterFactory<T>(Func<T> factory)
    {
        _factories[typeof(T)] = () => factory();
    }

    // 解析依赖
    public T Resolve<T>()
    {
        return (T)Resolve(typeof(T));
    }

    private object Resolve(Type type)
    {
        // 1. 检查单例
        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        // 2. 检查工厂方法
        if (_factories.TryGetValue(type, out var factory))
            return factory();

        // 3. 检查类型映射
        if (_mappings.TryGetValue(type, out var implType))
        {
            // 如果映射类型有工厂则使用
            if (_factories.TryGetValue(implType, out var implFactory))
                return implFactory();
            
            // 否则创建新实例
            return Activator.CreateInstance(implType);
        }

        // 4. 尝试直接创建
        return Activator.CreateInstance(type);
    }
}
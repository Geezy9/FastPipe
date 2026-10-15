using System;
using System.Collections.Generic;
using System.Text;
using KingShell.Models;
using System.Collections.Concurrent;
using KingShell.utils;

namespace KingShell;

internal static class Runtime
{
    internal static ConcurrentDictionary<long, PipeNode<int>> Nodes = new ConcurrentDictionary<long, PipeNode<int>>();  // Key: Source.Id. Pipenode is of tye int only because it needs to have a type. The int is not used for anything else.
    private static ConcurrentDictionary<Type, Object> _pools = new ConcurrentDictionary<Type, Object>(); // Key: Type of the object. Value: Object pool for that type.

    static Runtime()
    {
        Nodes = new ConcurrentDictionary<long, PipeNode<int>>();
        _pools = new ConcurrentDictionary<Type, Object>();

        foreach (var type in Config.Types)
        {
            // Create BaseObj<T>
            Type baseObjType = typeof(BaseObj<>).MakeGenericType(type);

            // Create Stack<BaseObj<T>>
            Type stackType = typeof(ConcurrentStack<>).MakeGenericType(baseObjType);

            // Instantiate Stack<BaseObj<T>>
            object stackInstance = Activator.CreateInstance(stackType)!;

            _pools[type] = stackInstance;
        }
    }

    internal static BaseObj<T> Rent<T>(T value)
    {
        if (_pools.TryGetValue(typeof(T), out object? poolObj))
        {
            var pool = (ConcurrentStack<BaseObj<T>>)poolObj;

            if (pool.TryPop(out BaseObj<T>? obj))
            {
                obj.Value = value;
                return obj;
            }
        }

        return new BaseObj<T>(value);
    }


    internal static void ReturnToFree<T>(BaseObj<T> obj)
    {
        obj.Value = default!;

        var type = typeof(T);

        if (!_pools.TryGetValue(type, out object? poolObj))
        {
            // Create pool on demand
            Type baseObjType = typeof(BaseObj<>).MakeGenericType(type);
            Type stackType = typeof(ConcurrentStack<>).MakeGenericType(baseObjType);
            poolObj = Activator.CreateInstance(stackType)!;
            _pools[type] = poolObj;
        }

        var pool = (ConcurrentStack<BaseObj<T>>)poolObj;
        pool.Push(obj);
    }






}


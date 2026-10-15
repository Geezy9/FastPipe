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

    // One lazily created, type-safe pool per value type. No reflection or casting required:
    // the runtime instantiates Pool<T> on first use of each T.
    private static class Pool<T>
    {
        internal static readonly ConcurrentStack<BaseObj<T>> Items = new();
    }

    internal static BaseObj<T> Rent<T>(T value)
    {
        if (Pool<T>.Items.TryPop(out BaseObj<T>? obj))
        {
            obj.Value = value;
            return obj;
        }

        return new BaseObj<T>(value);
    }

    // Returns an object to its pool without the caller needing to know its value type.
    internal static void Return(BaseObj obj) => obj.ReturnToPool();

    internal static void ReturnToFree<T>(BaseObj<T> obj)
    {
        obj.Reset();
        Pool<T>.Items.Push(obj);
    }

}


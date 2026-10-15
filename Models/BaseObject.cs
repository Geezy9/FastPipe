using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using KingShell.utils;
using KingShell.Interfaces;

namespace KingShell.Models;
public class BaseObj : IPoolable
{

    public BaseObj()
    {
    }

    public virtual void Reset()
    {
    }

    public virtual object? UntypedValue => null;

    // Non-generic hook so callers can recycle objects without knowing T.
    internal virtual void ReturnToPool()
    {
    }
}

public class BaseObj<T> : BaseObj, IPoolable
{
    public T Value;
    public BaseObj(T value)
    {
        Value = value;
    }
    public override object? UntypedValue => Value;
    public override void Reset()
    {
        Value = default!;
    }

    internal override void ReturnToPool() => Runtime.ReturnToFree(this);
}

internal class Result<T> : BaseObj
{
    internal long ParentKey;

    public Result(long parentKey)
    {
        ParentKey = parentKey;
    }
}

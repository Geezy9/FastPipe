using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using KingShell.utils;
using KingShell.Interfaces;

namespace KingShell.Models;
internal class BaseObj : IPoolable
{

    internal BaseObj()
    {
    }

    public void Reset()
    {
    }

    internal virtual object? UntypedValue => null;
}

internal class BaseObj<T> : BaseObj, IPoolable
{
    internal T Value;
    public BaseObj(T value)
    {
        Value = value;
    }
    internal override object? UntypedValue => Value;
    public void Reset()
    {
        Value = default!;
    }
}

internal class Result<T> : BaseObj
{
    internal long ParentKey;

    public Result(long parentKey)
    {
        ParentKey = parentKey;
    }
}

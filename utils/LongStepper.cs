using System;
using System.Collections.Generic;
using System.Text;

namespace FastPipe.utils;

internal static class LongStepper
{
    private static long Current
    {
        get; set;
    }

    public static long GetId()
    {
        Current++;
        return Current;
    }
}

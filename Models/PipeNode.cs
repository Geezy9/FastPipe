using System;
using System.Collections.Generic;
using System.Text;

namespace KingShell.Models;

internal class PipeNode<T>
{
    internal Source Source;
    internal BaseObj<T>? Input;
    internal Result<T>? Output;
}

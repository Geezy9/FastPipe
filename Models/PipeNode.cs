namespace FastPipe.Models;

internal class PipeNode<T>
{
    internal Source Source = new();
    internal BaseObj<T>? Input;
    internal Result<T>? Output;
}

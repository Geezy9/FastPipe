using FastPipe.Models;

namespace FastPipe.Extensions;

public sealed class ShellSession
{
    public string WorkingDirectory { get; private set; } = Environment.CurrentDirectory;

    public bool ExitRequested { get; private set; }

    public bool ClearRequested { get; private set; }

    public void SetWorkingDirectory(string path) => WorkingDirectory = path;

    public void RequestExit() => ExitRequested = true;

    public void RequestClear() => ClearRequested = true;

    internal bool ConsumeClearRequest()
    {
        var requested = ClearRequested;
        ClearRequested = false;
        return requested;
    }
}

public sealed class CommandContext
{
    private readonly List<BaseObj> _output = [];
    private readonly HashSet<BaseObj> _rented = [];

    public CommandContext(ShellSession session) => Session = session;

    public ShellSession Session { get; }

    public IReadOnlyList<BaseObj> Output => _output;

    public void Write(BaseObj value) => _output.Add(value);

    public void Write<T>(T value) => _output.Add(Runtime.Rent(value));

    internal void TrackRented(BaseObj value) => _rented.Add(value);

    internal void TrackRented(IEnumerable<BaseObj> values)
    {
        foreach (var value in values)
            _rented.Add(value);
    }

    // Returns every object produced or consumed by this pipeline to the pool.
    // The set deduplicates instances that appear as both argument and output.
    internal void Release()
    {
        foreach (var item in _output)
            _rented.Add(item);

        _output.Clear();

        foreach (var item in _rented)
            Runtime.Return(item);

        _rented.Clear();
    }
}
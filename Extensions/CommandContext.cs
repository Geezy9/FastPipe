using KingShell.Models;

namespace KingShell.Extensions;

internal sealed class ShellSession
{
    internal string WorkingDirectory { get; private set; } = Environment.CurrentDirectory;

    internal bool ExitRequested { get; private set; }

    internal bool ClearRequested { get; private set; }

    internal void SetWorkingDirectory(string path) => WorkingDirectory = path;

    internal void RequestExit() => ExitRequested = true;

    internal void RequestClear() => ClearRequested = true;

    internal bool ConsumeClearRequest()
    {
        var requested = ClearRequested;
        ClearRequested = false;
        return requested;
    }
}

internal sealed class CommandContext
{
    private readonly List<BaseObj> _output = [];

    internal CommandContext(ShellSession session) => Session = session;

    internal ShellSession Session { get; }

    internal IReadOnlyList<BaseObj> Output => _output;

    internal void Write(BaseObj value) => _output.Add(value);

    internal void Write<T>(T value) => _output.Add(new BaseObj<T>(value));
}
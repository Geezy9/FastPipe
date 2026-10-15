namespace KingShell.Models;

internal class Source
{
    internal long Id;            // Unique identity
    internal SourceType Type;    // Process, Script, Pipeline, Command
    internal string Name = string.Empty;        // Process name, script name, command name
    internal string Origin = string.Empty;      // File path, PID, stdin, etc.
}


internal enum SourceType
{
    File,
    Process,
    Pipe,
}

using System;
using System.Collections.Generic;
using System.Text;

namespace KingShell.Models;

internal class Source
{
    internal long Id;            // Unique identity
    internal SourceType Type;    // Process, Script, Pipeline, Command
    internal string Name;        // Process name, script name, command name
    internal string Origin;      // File path, PID, stdin, etc.
}


internal enum SourceType
{
    File,
    Process,
    Pipe,
}

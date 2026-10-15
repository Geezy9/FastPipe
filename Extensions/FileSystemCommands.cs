using KingShell.Interfaces;
using KingShell.Models;

namespace KingShell.Extensions;

internal sealed record FileSystemEntry(string Name, string Path, bool IsDirectory, long? Length, DateTimeOffset LastWriteTime) : IStructuredValue
{
    public bool TryGetMemberValue(string memberName, out object? value)
    {
        switch (memberName)
        {
            case "Name":
                value = Name;
                return true;
            case "Path":
                value = Path;
                return true;
            case "IsDirectory":
                value = IsDirectory;
                return true;
            case "Length":
                value = Length;
                return true;
            case "LastWriteTime":
                value = LastWriteTime;
                return true;
            default:
                value = null;
                return false;
        }
    }

    public IEnumerable<KeyValuePair<string, object?>> EnumerateMembers()
    {
        yield return new KeyValuePair<string, object?>("Name", Name);
        yield return new KeyValuePair<string, object?>("Path", Path);
        yield return new KeyValuePair<string, object?>("IsDirectory", IsDirectory);
        yield return new KeyValuePair<string, object?>("Length", Length);
        yield return new KeyValuePair<string, object?>("LastWriteTime", LastWriteTime);
    }
}

internal sealed class ListDirectoryCommand : IShellCmd
{
    public string Name => "ls";
    public IReadOnlyList<string> Aliases => ["dir"];
    public string Description => "Lists directory entries; -d lists only directories.";
    public string Usage => "ls [-d] [path]";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var directoriesOnly = false;
        BaseObj? pathArgument = null;
        foreach (var argument in arguments)
        {
            var value = CommandArguments.GetText(argument, "path or option");
            if (value.Equals("-d", StringComparison.OrdinalIgnoreCase) || value.Equals("--directory", StringComparison.OrdinalIgnoreCase))
                directoriesOnly = true;
            else if (pathArgument is null)
                pathArgument = argument;
            else
                throw new ArgumentException("Usage: ls [-d] [path]");
        }

        var path = pathArgument is null ? context.Session.WorkingDirectory : CommandArguments.ResolvePath(context, pathArgument, "path");
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isDirectory = entry is DirectoryInfo;
            if (directoriesOnly && !isDirectory)
                continue;

            context.Write(new FileSystemEntry(entry.Name, entry.FullName, isDirectory, isDirectory ? null : ((FileInfo)entry).Length, entry.LastWriteTimeUtc));
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class MakeDirectoryCommand : IShellCmd
{
    public string Name => "mkdir";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Creates one or more directories.";
    public string Usage => "mkdir <path...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException("Usage: mkdir <path...>");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(Directory.CreateDirectory(CommandArguments.ResolvePath(context, argument, "path")).FullName);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class GetContentCommand : IShellCmd
{
    public string Name => "get-content";
    public IReadOnlyList<string> Aliases => ["cat"];
    public string Description => "Asynchronously reads text files.";
    public string Usage => "get-content <path...>";

    public async ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException("Usage: get-content <path...>");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(await File.ReadAllTextAsync(CommandArguments.ResolvePath(context, argument, "path"), cancellationToken));
        }
    }
}

internal sealed class SetContentCommand : IShellCmd
{
    public string Name => "set-content";
    public IReadOnlyList<string> Aliases => ["write"];
    public string Description => "Asynchronously overwrites a text file.";
    public string Usage => "set-content <path> <text...>";

    public async ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var (path, text) = GetPathAndText(context, arguments, Name);
        await File.WriteAllTextAsync(path, text, cancellationToken);
        context.Write(path);
    }

    internal static (string Path, string Text) GetPathAndText(CommandContext context, BaseObj[] arguments, string commandName)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {commandName} <path> <text...>");

        return (CommandArguments.ResolvePath(context, arguments[0], "path"), string.Join(' ', arguments[1..].Select(argument => CommandArguments.GetText(argument, "text"))));
    }
}

internal sealed class AppendContentCommand : IShellCmd
{
    public string Name => "append";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Asynchronously appends text to a file.";
    public string Usage => "append <path> <text...>";

    public async ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var (path, text) = SetContentCommand.GetPathAndText(context, arguments, Name);
        await File.AppendAllTextAsync(path, text, cancellationToken);
        context.Write(path);
    }
}

internal sealed class CopyCommand : IShellCmd
{
    public string Name => "copy";
    public IReadOnlyList<string> Aliases => ["cp"];
    public string Description => "Copies a file; --force permits overwriting.";
    public string Usage => "copy <source> <destination> [--force]";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var (source, destination, overwrite) = GetTransferArguments(context, arguments, Name);
        cancellationToken.ThrowIfCancellationRequested();
        File.Copy(source, destination, overwrite);
        context.Write(destination);
        return ValueTask.CompletedTask;
    }

    internal static (string Source, string Destination, bool Overwrite) GetTransferArguments(CommandContext context, BaseObj[] arguments, string commandName)
    {
        if (arguments.Length is < 2 or > 3)
            throw new ArgumentException($"Usage: {commandName} <source> <destination> [--force]");

        var overwrite = arguments.Length == 3 && CommandArguments.GetText(arguments[2], "option").Equals("--force", StringComparison.OrdinalIgnoreCase);
        if (arguments.Length == 3 && !overwrite)
            throw new ArgumentException("The only supported option is --force.");

        return (CommandArguments.ResolvePath(context, arguments[0], "source"), CommandArguments.ResolvePath(context, arguments[1], "destination"), overwrite);
    }
}

internal sealed class MoveCommand : IShellCmd
{
    public string Name => "move";
    public IReadOnlyList<string> Aliases => ["mv"];
    public string Description => "Moves or renames a file; --force permits overwriting.";
    public string Usage => "move <source> <destination> [--force]";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var (source, destination, overwrite) = CopyCommand.GetTransferArguments(context, arguments, Name);
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(source, destination, overwrite);
        context.Write(destination);
        return ValueTask.CompletedTask;
    }
}

internal sealed class RemoveCommand : IShellCmd
{
    public string Name => "remove";
    public IReadOnlyList<string> Aliases => ["rm"];
    public string Description => "Deletes files; --recursive permits directory deletion and --force ignores missing paths.";
    public string Usage => "remove [--recursive] [--force] <path...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var recursive = false;
        var force = false;
        var paths = new List<BaseObj>();
        foreach (var argument in arguments)
        {
            var value = CommandArguments.GetText(argument, "path or option");
            if (value.Equals("--recursive", StringComparison.OrdinalIgnoreCase)) recursive = true;
            else if (value.Equals("--force", StringComparison.OrdinalIgnoreCase)) force = true;
            else paths.Add(argument);
        }

        if (paths.Count == 0)
            throw new ArgumentException("Usage: remove [--recursive] [--force] <path...>");

        foreach (var argument in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = CommandArguments.ResolvePath(context, argument, "path");
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path))
            {
                if (!recursive)
                    throw new IOException($"'{path}' is a directory. Use --recursive to delete it.");
                Directory.Delete(path, recursive: true);
            }
            else if (!force) throw new FileNotFoundException($"Path not found: {path}", path);

            context.Write(path);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class TouchCommand : IShellCmd
{
    public string Name => "touch";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Creates empty files or updates their last write time.";
    public string Usage => "touch <path...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException("Usage: touch <path...>");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = CommandArguments.ResolvePath(context, argument, "path");
            if (File.Exists(path))
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            else
                File.Create(path).Dispose();

            context.Write(path);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class ExistsCommand : IShellCmd
{
    public string Name => "exists";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Tests whether a path exists.";
    public string Usage => "exists <path>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException("Usage: exists <path>");

        cancellationToken.ThrowIfCancellationRequested();
        var path = CommandArguments.ResolvePath(context, arguments[0], "path");
        context.Write(File.Exists(path) || Directory.Exists(path));
        return ValueTask.CompletedTask;
    }
}
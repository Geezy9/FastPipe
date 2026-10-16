using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using FastPipe.Models;

namespace FastPipe.Extensions;

internal static class ExternalProcessCommandRunner
{
    internal static ValueTask ExecuteResolvedCommandAsync(
        string commandPath,
        string workingDirectory,
        IReadOnlyList<string> prefixArguments,
        CommandContext context,
        IReadOnlyList<BaseObj> explicitArguments,
        IReadOnlyList<BaseObj> pipedInput,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(commandPath, workingDirectory, prefixArguments, context, explicitArguments, pipedInput, cancellationToken);

    internal static async ValueTask<bool> TryExecuteAsync(
        string commandName,
        CommandContext context,
        IReadOnlyList<BaseObj> explicitArguments,
        IReadOnlyList<BaseObj> pipedInput,
        CancellationToken cancellationToken)
    {
        var commandPath = ResolveCommandPath(commandName, context.Session.WorkingDirectory);
        if (commandPath is null)
            return false;

        await ExecuteCommandAsync(commandPath, context.Session.WorkingDirectory, [], context, explicitArguments, pipedInput, cancellationToken);
        return true;
    }

    private static async ValueTask ExecuteCommandAsync(
        string commandPath,
        string workingDirectory,
        IReadOnlyList<string> prefixArguments,
        CommandContext context,
        IReadOnlyList<BaseObj> explicitArguments,
        IReadOnlyList<BaseObj> pipedInput,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(prefixArguments);
        var commandDisplayName = Path.GetFileName(commandPath);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(commandPath, workingDirectory, prefixArguments, explicitArguments, pipedInput.Count > 0)
        };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Unable to start external command '{commandDisplayName}'.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Unable to start external command '{commandDisplayName}': {exception.Message}", exception);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdinTask = WritePipedInputAsync(process, pipedInput, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask, stdinTask, process.WaitForExitAsync(cancellationToken));

        if (process.ExitCode != 0)
        {
            var error = stderrTask.Result.Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? $"External command '{commandDisplayName}' exited with code {process.ExitCode}."
                : error);
        }

        foreach (var line in EnumerateLines(stdoutTask.Result))
            context.Write(line);
    }

    internal static string? ResolveCommandPath(string commandName, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        if (commandName.Contains(Path.DirectorySeparatorChar) || commandName.Contains(Path.AltDirectorySeparatorChar) || Path.IsPathRooted(commandName))
            return ResolveFromDirectory(commandName, workingDirectory);

        return ResolveFromDirectory(commandName, workingDirectory) ?? ResolveFromPathEnvironment(commandName);
    }

    private static ProcessStartInfo CreateStartInfo(
        string commandPath,
        string workingDirectory,
        IReadOnlyList<string> prefixArguments,
        IReadOnlyList<BaseObj> explicitArguments,
        bool redirectStandardInput)
    {
        var launcher = GetLauncher(commandPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = launcher.FileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectStandardInput
        };

        foreach (var prefixArgument in launcher.PrefixArguments)
            startInfo.ArgumentList.Add(prefixArgument);

        foreach (var prefixArgument in prefixArguments)
            startInfo.ArgumentList.Add(prefixArgument);

        foreach (var argument in explicitArguments)
            startInfo.ArgumentList.Add(ConvertArgument(argument));

        return startInfo;
    }

    private static async Task WritePipedInputAsync(Process process, IReadOnlyList<BaseObj> pipedInput, CancellationToken cancellationToken)
    {
        if (pipedInput.Count == 0)
            return;

        foreach (var value in pipedInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = ConvertArgument(value);
            await process.StandardInput.WriteAsync(line.AsMemory(), cancellationToken);
            await process.StandardInput.WriteLineAsync();
        }

        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();
    }

    private static IEnumerable<string> EnumerateLines(string output)
    {
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static string ConvertArgument(BaseObj value) =>
        value.UntypedValue switch
        {
            null => string.Empty,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.UntypedValue.ToString() ?? string.Empty,
        };

    private static string? ResolveFromDirectory(string commandName, string workingDirectory)
    {
        var basePath = Path.IsPathRooted(commandName)
            ? commandName
            : Path.Combine(workingDirectory, commandName);

        foreach (var candidate in GetCandidatePaths(basePath))
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static string? ResolveFromPathEnvironment(string commandName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidate in GetCandidatePaths(Path.Combine(directory, commandName)))
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidatePaths(string basePath)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(basePath)))
        {
            yield return basePath;
            yield break;
        }

        yield return basePath;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            yield break;

        var extensions = Environment.GetEnvironmentVariable("PATHEXT");
        foreach (var extension in (string.IsNullOrWhiteSpace(extensions) ? ".EXE;.CMD;.BAT;.COM" : extensions)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return basePath + extension.ToLowerInvariant();
        }
    }

    private static (string FileName, IReadOnlyList<string> PrefixArguments) GetLauncher(string commandPath)
    {
        var extension = Path.GetExtension(commandPath);
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            return ("dotnet", [commandPath]);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && extension is not null &&
            (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            return (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", ["/c", commandPath]);
        }

        return (commandPath, []);
    }
}
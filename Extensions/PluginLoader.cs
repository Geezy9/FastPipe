using System.Text.Json;
using KingShell.Interfaces;
using KingShell.Models;

namespace KingShell.Extensions;

internal static class PluginLoader
{
    private const string PluginDirectoryName = "plugins";

    internal static void RegisterExternalCommands(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        foreach (var manifestPath in GetPluginManifestPaths())
        {
            try
            {
                var plugin = LoadPlugin(manifestPath);
                dispatcher.Register(new ExternalPluginCommand(plugin));
                ConsoleRenderer.WriteNotice($"Registered external plugin '{plugin.Name}' from {Path.GetFileName(manifestPath)}.");
            }
            catch (Exception exception)
            {
                ConsoleRenderer.WriteError($"Unable to register plugin manifest '{Path.GetFileName(manifestPath)}': {exception.Message}");
            }
        }
    }

    internal static ExternalPluginDefinition LoadPlugin(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Plugin manifest root must be a JSON object.");

        var name = GetRequiredString(root, "name");
        var description = GetRequiredString(root, "description");
        var usage = GetRequiredString(root, "usage");
        var executable = GetRequiredString(root, "executable");
        var aliases = GetOptionalStringArray(root, "aliases");
        var arguments = GetOptionalStringArray(root, "arguments");
        var pluginDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException("Plugin manifest path does not have a parent directory.");
        var executablePath = ExternalProcessCommandRunner.ResolveCommandPath(executable, pluginDirectory);

        if (executablePath is null)
            throw new FileNotFoundException($"Plugin executable '{executable}' could not be resolved.", executable);

        return new ExternalPluginDefinition(name, aliases, description, usage, executablePath, arguments, pluginDirectory);
    }

    private static IEnumerable<string> GetPluginManifestPaths()
    {
        var pluginDirectory = Path.Combine(AppContext.BaseDirectory, PluginDirectoryName);
        if (!Directory.Exists(pluginDirectory))
            yield break;

        foreach (var manifestPath in Directory.EnumerateFiles(pluginDirectory, "plugin.json", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.GetFullPath(manifestPath);
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Plugin manifest property '{propertyName}' is required and must be a string.");

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Plugin manifest property '{propertyName}' must not be empty.");

        return value;
    }

    private static string[] GetOptionalStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return [];

        if (property.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Plugin manifest property '{propertyName}' must be an array of strings.");

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"Plugin manifest property '{propertyName}' must contain only strings.");

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Plugin manifest property '{propertyName}' must not contain empty values.");

            values.Add(value);
        }

        return [.. values];
    }
}

internal interface IExternalShellCmd
{
    ValueTask ExecuteAsync(
        CommandContext context,
        IReadOnlyList<BaseObj> explicitArguments,
        IReadOnlyList<BaseObj> pipedInput,
        CancellationToken cancellationToken);
}

internal sealed class ExternalPluginCommand(ExternalPluginDefinition plugin) : IShellCmd, IExternalShellCmd
{
    public string Name => plugin.Name;

    public IReadOnlyList<string> Aliases => plugin.Aliases;

    public string Description => plugin.Description;

    public string Usage => plugin.Usage;

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken) =>
        ExecuteAsync(context, arguments, [], cancellationToken);

    public ValueTask ExecuteAsync(
        CommandContext context,
        IReadOnlyList<BaseObj> explicitArguments,
        IReadOnlyList<BaseObj> pipedInput,
        CancellationToken cancellationToken) =>
        ExternalProcessCommandRunner.ExecuteResolvedCommandAsync(
            plugin.ExecutablePath,
            plugin.WorkingDirectory,
            plugin.Arguments,
            context,
            explicitArguments,
            pipedInput,
            cancellationToken);
}

internal sealed record ExternalPluginDefinition(
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    string Usage,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);
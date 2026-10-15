using KingShell.Interfaces;
using KingShell.Models;

namespace KingShell.Extensions;

internal sealed class Dispatcher
{
    private readonly Dictionary<string, IShellCmd> _shellCommands =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Register(IShellCmd command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException(
                "A command must have a non-empty name.",
                nameof(command));
        }

        RegisterName(command.Name, command);

        foreach (var alias in command.Aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("A command alias must be non-empty.", nameof(command));

            RegisterName(alias, command);
        }
    }

    internal IReadOnlyList<IShellCmd> Commands => _shellCommands.Values
        .Distinct()
        .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal ValueTask DispatchAsync(
        string commandName,
        CommandContext context,
        BaseObj[] arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        if (!_shellCommands.TryGetValue(commandName, out IShellCmd? registeredCommand))
        {
            throw new InvalidOperationException(
                $"Unknown command '{commandName}'.");
        }

        return registeredCommand.ExecuteAsync(context, arguments, cancellationToken);
    }

    private void RegisterName(string name, IShellCmd command)
    {
        if (!_shellCommands.TryAdd(name, command))
            throw new InvalidOperationException($"A command named or aliased '{name}' is already registered.");
    }
}
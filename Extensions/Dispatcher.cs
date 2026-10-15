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

    internal async ValueTask DispatchAsync(
        string commandName,
        CommandContext context,
        IReadOnlyList<BaseObj> explicitArguments,
        IReadOnlyList<BaseObj> pipedInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        if (_shellCommands.TryGetValue(commandName, out IShellCmd? registeredCommand))
        {
            if (registeredCommand is IExternalShellCmd externalShellCommand)
            {
                await externalShellCommand.ExecuteAsync(context, explicitArguments, pipedInput, cancellationToken);
                return;
            }

            var arguments = new BaseObj[explicitArguments.Count + pipedInput.Count];

            for (var index = 0; index < explicitArguments.Count; index++)
                arguments[index] = explicitArguments[index];

            for (var index = 0; index < pipedInput.Count; index++)
                arguments[explicitArguments.Count + index] = pipedInput[index];

            await registeredCommand.ExecuteAsync(context, arguments, cancellationToken);
            return;
        }

        if (await ExternalProcessCommandRunner.TryExecuteAsync(commandName, context, explicitArguments, pipedInput, cancellationToken))
            return;

        throw new InvalidOperationException($"Unknown command '{commandName}'.");
    }

    private void RegisterName(string name, IShellCmd command)
    {
        if (!_shellCommands.TryAdd(name, command))
            throw new InvalidOperationException($"A command named or aliased '{name}' is already registered.");
    }
}
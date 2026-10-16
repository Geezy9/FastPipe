using FastPipe.Extensions.Collections;
using FastPipe.Extensions.Deserializers;
using FastPipe.Extensions.Serializers;
using FastPipe.Interfaces;
using FastPipe.Models;
using System.ComponentModel;
using System.Diagnostics;

namespace FastPipe.Extensions;

internal static class BuiltInCommands
{
    internal static void RegisterAll(Dispatcher dispatcher)
    {
        dispatcher.Register(new HelpCommand(dispatcher));
        // dispatcher.Register(new TimeCommand(dispatcher));
        dispatcher.Register(new EchoCommand());
        dispatcher.Register(new ClearCommand());
        dispatcher.Register(new ExitCommand());
        dispatcher.Register(new PresentWorkingDirectoryCommand());
        dispatcher.Register(new ChangeDirectoryCommand());
        dispatcher.Register(new ListDirectoryCommand());
        dispatcher.Register(new MakeDirectoryCommand());
        dispatcher.Register(new GetContentCommand());
        dispatcher.Register(new SetContentCommand());
        dispatcher.Register(new AppendContentCommand());
        dispatcher.Register(new CopyCommand());
        dispatcher.Register(new MoveCommand());
        dispatcher.Register(new RemoveCommand());
        dispatcher.Register(new ExistsCommand());
        dispatcher.Register(new TouchCommand());
        dispatcher.Register(new AddCommand());
        dispatcher.Register(new SubtractCommand());
        dispatcher.Register(new MultiplyCommand());
        dispatcher.Register(new DivideCommand());
        dispatcher.Register(new ModuloCommand());
        dispatcher.Register(new EqualsCommand());
        dispatcher.Register(new NotEqualsCommand());
        dispatcher.Register(new GreaterThanCommand());
        dispatcher.Register(new LessThanCommand());
        dispatcher.Register(new GreaterOrEqualCommand());
        dispatcher.Register(new LessOrEqualCommand());
        dispatcher.Register(new NotCommand());
        dispatcher.Register(new AndCommand());
        dispatcher.Register(new OrCommand());
        dispatcher.Register(new ToIntCommand());
        dispatcher.Register(new ToDoubleCommand());
        dispatcher.Register(new ToBoolCommand());
        dispatcher.Register(new ToStringCommand());
        dispatcher.Register(new PlainCommand());
        dispatcher.Register(new TypeCommand());
        dispatcher.Register(new LengthCommand());
        dispatcher.Register(new UpperCommand());
        dispatcher.Register(new LowerCommand());
        dispatcher.Register(new ConcatCommand());
        CollectionCommands.RegisterAll(dispatcher);
        SerializerCommands.RegisterAll(dispatcher);
        DeserializerCommands.RegisterAll(dispatcher);
    }
}

internal sealed class HelpCommand(Dispatcher dispatcher) : IShellCmd
{
    public string Name => "help";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Lists available commands or shows help for one command.";
    public string Usage => "help [command]";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (arguments.Length > 1)
            throw new ArgumentException("Usage: help [command]");

        if (arguments.Length == 1)
        {
            var name = CommandArguments.GetText(arguments[0], "command");
            var command = dispatcher.Commands.FirstOrDefault(item =>
                item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                item.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));

            if (command is null)
                throw new InvalidOperationException($"Unknown command '{name}'.");

            context.Write($"{command.Usage} - {command.Description}");
            return ValueTask.CompletedTask;
        }

        foreach (var command in dispatcher.Commands)
        {
            var aliases = command.Aliases.Count == 0 ? string.Empty : $" ({string.Join(", ", command.Aliases)})";
            context.Write($"{command.Name}{aliases} - {command.Description}");
        }


        return ValueTask.CompletedTask;
    }
}

internal sealed class EchoCommand : IShellCmd
{
    public string Name => "echo";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Writes each supplied typed value.";
    public string Usage => "echo [value...]";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(argument);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class ClearCommand : IShellCmd
{
    public string Name => "clear";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Clears the interactive terminal.";
    public string Usage => "clear";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        CommandArguments.RequireNone(arguments, Name);
        cancellationToken.ThrowIfCancellationRequested();
        context.Session.RequestClear();
        return ValueTask.CompletedTask;
    }
}



internal sealed class ExitCommand : IShellCmd
{
    public string Name => "exit";
    public IReadOnlyList<string> Aliases => ["quit"];
    public string Description => "Ends the current shell session.";
    public string Usage => "exit";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        CommandArguments.RequireNone(arguments, Name);
        cancellationToken.ThrowIfCancellationRequested();
        context.Session.RequestExit();
        return ValueTask.CompletedTask;
    }
}

internal sealed class PresentWorkingDirectoryCommand : IShellCmd
{
    public string Name => "pwd";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Writes the current shell working directory.";
    public string Usage => "pwd";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        CommandArguments.RequireNone(arguments, Name);
        cancellationToken.ThrowIfCancellationRequested();
        context.Write(context.Session.WorkingDirectory);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ChangeDirectoryCommand : IShellCmd
{
    public string Name => "cd";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Changes the current shell working directory.";
    public string Usage => "cd <path>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException("Usage: cd <path>");

        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(CommandArguments.GetText(arguments[0], "path"), context.Session.WorkingDirectory);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        context.Session.SetWorkingDirectory(path);
        context.Write(path);
        return ValueTask.CompletedTask;
    }
}


public static class CommandArguments
{
    public static void RequireNone(BaseObj[] arguments, string commandName)
    {
        if (arguments.Length != 0)
            throw new ArgumentException($"Usage: {commandName}");
    }

    public static string GetText(BaseObj argument, string argumentName) =>
        argument.UntypedValue as string ?? throw new ArgumentException($"{argumentName} must be a string.");

    public static string ResolvePath(CommandContext context, BaseObj argument, string argumentName) =>
        Path.GetFullPath(GetText(argument, argumentName), context.Session.WorkingDirectory);

    public static (double Value, bool IsIntegral) GetNumber(BaseObj argument, string argumentName) =>
        argument.UntypedValue switch
        {
            int value => (value, true),
            long value => (value, true),
            double value => (value, false),
            string text when long.TryParse(text, out var value) => (value, true),
            string text when double.TryParse(text, out var value) => (value, false),
            _ => throw new ArgumentException($"{argumentName} must be a number."),
        };

    public static bool GetBool(BaseObj argument, string argumentName) =>
        argument.UntypedValue switch
        {
            bool value => value,
            string text when bool.TryParse(text, out var value) => value,
            _ => throw new ArgumentException($"{argumentName} must be a boolean."),
        };
}
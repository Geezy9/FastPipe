# Command Development API

This guide describes the internal contract for built-in commands.

> KingShell extensions are process-based plugins. This API is for contributors working inside the shell codebase.

## `IShellCmd`

Built-in commands implement `IShellCmd` and provide:

- `Name`
- `Aliases`
- `Description`
- `Usage`
- `ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)`

## Command Rules

- Treat command names and aliases as case-insensitive.
- Validate args and throw `ArgumentException` with usage on invalid input.
- Call `cancellationToken.ThrowIfCancellationRequested()` in loops and long operations.
- Write output through `context.Write(...)`.
- Do not write to the console directly from commands.

## `CommandContext`

`CommandContext` provides:

- Session state access (`Session`)
- Output collection (`Output`)
- Typed and untyped write helpers (`Write(...)`)

Lifecycle pooling methods are managed by the host pipeline.

## `ShellSession`

Session state includes:

- `WorkingDirectory`
- Deferred control requests (`RequestExit`, `RequestClear`)

Use session paths for file operations instead of process-global current directory assumptions.

## Argument Helpers

Use `CommandArguments` helpers when applicable:

- `GetText`
- `GetNumber`
- `GetBool`
- `ResolvePath`
- `RequireNone`

## Registering a Command

1. Implement `IShellCmd`.
2. Register in `BuiltInCommands.RegisterAll`.
3. Add tests for parsing, execution, and error behavior.

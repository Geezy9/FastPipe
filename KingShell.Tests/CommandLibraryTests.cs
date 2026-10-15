using KingShell.Extensions;
using KingShell.Extensions.Collections;
using KingShell.Extensions.Serializers;
using KingShell.Interfaces;
using KingShell.Models;
using System.Text.Json;
using Xunit;

namespace KingShell.Tests;

public sealed class CommandLibraryTests
{
    [Fact]
    public async Task EchoPreservesTypedInputObject()
    {
        var context = new CommandContext(new ShellSession());
        var value = new BaseObj<int>(42);

        await new EchoCommand().ExecuteAsync(context, [value], CancellationToken.None);

        Assert.Same(value, Assert.Single(context.Output));
        Assert.Equal(42, context.Output[0].UntypedValue);
    }

    [Fact]
    public async Task PlainRemovesMarkupAndAnsiEscapeSequences()
    {
        var context = new CommandContext(new ShellSession());

        await new PlainCommand().ExecuteAsync(
            context,
            [new BaseObj<string>("\u001b[31m[bold red]Alert[/]\u001b[0m [[escaped]]")],
            CancellationToken.None);

        Assert.Equal("Alert [escaped]", Assert.Single(context.Output).UntypedValue);
    }

    [Fact]
    public async Task PlainPreservesNonMarkupBracketTextWhenMarkupIsMalformed()
    {
        var context = new CommandContext(new ShellSession());

        await new PlainCommand().ExecuteAsync(
            context,
            [new BaseObj<string>("[red]Alert [custom]")],
            CancellationToken.None);

        Assert.Equal("Alert [custom]", Assert.Single(context.Output).UntypedValue);
    }

    [Fact]
    public async Task ChangeDirectoryUpdatesOnlyTheShellSession()
    {
        var originalProcessDirectory = Environment.CurrentDirectory;
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var session = new ShellSession();
            var context = new CommandContext(session);

            await new ChangeDirectoryCommand().ExecuteAsync(context, [new BaseObj<string>(temporaryDirectory)], CancellationToken.None);

            Assert.Equal(Path.GetFullPath(temporaryDirectory), session.WorkingDirectory);
            Assert.Equal(originalProcessDirectory, Environment.CurrentDirectory);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveRejectsDirectoriesWithoutRecursiveOption()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var context = new CommandContext(new ShellSession());

            await Assert.ThrowsAsync<IOException>(() => new RemoveCommand().ExecuteAsync(context, [new BaseObj<string>(temporaryDirectory)], CancellationToken.None).AsTask());
            Assert.True(Directory.Exists(temporaryDirectory));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CopyRequiresForceToOverwrite()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var source = Path.Combine(temporaryDirectory, "source.txt");
            var destination = Path.Combine(temporaryDirectory, "destination.txt");
            await File.WriteAllTextAsync(source, "source");
            await File.WriteAllTextAsync(destination, "destination");
            var context = new CommandContext(new ShellSession());

            await Assert.ThrowsAnyAsync<IOException>(() => new CopyCommand().ExecuteAsync(context, [new BaseObj<string>(source), new BaseObj<string>(destination)], CancellationToken.None).AsTask());

            await new CopyCommand().ExecuteAsync(context, [new BaseObj<string>(source), new BaseObj<string>(destination), new BaseObj<string>("--force")], CancellationToken.None);
            Assert.Equal("source", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PluginManifestRegistersExternalExecutableCommand()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(temporaryDirectory, "sample-plugin");
        Directory.CreateDirectory(pluginDirectory);

        try
        {
            var commandName = OperatingSystem.IsWindows() ? "plugin-echo.cmd" : "plugin-echo";
            var commandPath = Path.Combine(pluginDirectory, commandName);

            if (OperatingSystem.IsWindows())
            {
                await File.WriteAllTextAsync(commandPath, "@echo off\r\necho arg:%1\r\nfor /f \"delims=\" %%i in ('more') do echo stdin:%%i\r\n");
            }
            else
            {
                await File.WriteAllTextAsync(commandPath, "#!/bin/sh\necho \"arg:$1\"\nwhile IFS= read -r line; do echo \"stdin:$line\"; done\n");
                File.SetUnixFileMode(
                    commandPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
            await File.WriteAllTextAsync(
                manifestPath,
                $$"""
                {
                  "name": "plugin-test",
                  "aliases": ["plugin-alias"],
                  "description": "Test plugin command.",
                  "usage": "plugin-test <value>",
                  "executable": "{{commandName}}"
                }
                """);

            var dispatcher = new Dispatcher();
            dispatcher.Register(new ExternalPluginCommand(PluginLoader.LoadPlugin(manifestPath)));

            var context = new CommandContext(new ShellSession());
            await dispatcher.DispatchAsync(
                "plugin-alias",
                context,
                [new BaseObj<string>("hello")],
                [new BaseObj<string>("pipe")],
                CancellationToken.None);

            Assert.Equal(
                ["arg:hello", "stdin:pipe"],
                context.Output.Select(output => output.UntypedValue).Cast<string>().ToArray());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DispatcherFallsBackToExternalExecutableCommands()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var commandName = OperatingSystem.IsWindows() ? "external-echo.cmd" : "external-echo";
            var commandPath = Path.Combine(temporaryDirectory, commandName);

            if (OperatingSystem.IsWindows())
            {
                await File.WriteAllTextAsync(commandPath, "@echo off\r\necho arg:%1\r\nfor /f \"delims=\" %%i in ('more') do echo stdin:%%i\r\n");
            }
            else
            {
                await File.WriteAllTextAsync(commandPath, "#!/bin/sh\necho \"arg:$1\"\nwhile IFS= read -r line; do echo \"stdin:$line\"; done\n");
                File.SetUnixFileMode(
                    commandPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            var session = new ShellSession();
            session.SetWorkingDirectory(temporaryDirectory);

            var context = new CommandContext(session);
            var dispatcher = new Dispatcher();

            await dispatcher.DispatchAsync(
                Path.GetFileNameWithoutExtension(commandName),
                context,
                [new BaseObj<string>("value")],
                [new BaseObj<string>("pipe")],
                CancellationToken.None);

            Assert.Equal(
                ["arg:value", "stdin:pipe"],
                context.Output.Select(output => output.UntypedValue).Cast<string>().ToArray());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WhereFiltersObjectsByPublicMember()
    {
        var directory = new FileSystemEntry("docs", "C:/docs", true, null, DateTimeOffset.UtcNow);
        var file = new FileSystemEntry("notes.txt", "C:/docs/notes.txt", false, 128, DateTimeOffset.UtcNow);
        var context = new CommandContext(new ShellSession());

        await new WhereCommand().ExecuteAsync(
            context,
            [
                new BaseObj<string>("IsDirectory"),
                new BaseObj<string>("eq"),
                new BaseObj<bool>(true),
                new BaseObj<FileSystemEntry>(directory),
                new BaseObj<FileSystemEntry>(file)
            ],
            CancellationToken.None);

        Assert.Single(context.Output);
        Assert.Same(directory, context.Output[0].UntypedValue);
    }

    [Fact]
    public async Task SelectProjectsRequestedPublicMember()
    {
        var file = new FileSystemEntry("notes.txt", "C:/docs/notes.txt", false, 128, DateTimeOffset.UtcNow);
        var context = new CommandContext(new ShellSession());

        await new SelectCommand().ExecuteAsync(
            context,
            [
                new BaseObj<string>("Name"),
                new BaseObj<FileSystemEntry>(file)
            ],
            CancellationToken.None);

        Assert.Equal("notes.txt", Assert.Single(context.Output).UntypedValue);
    }

    [Fact]
    public async Task SortOrdersObjectsByMemberDescending()
    {
        var smaller = new FileSystemEntry("small.txt", "C:/small.txt", false, 1, DateTimeOffset.UtcNow);
        var larger = new FileSystemEntry("large.txt", "C:/large.txt", false, 20, DateTimeOffset.UtcNow);
        var context = new CommandContext(new ShellSession());

        await new SortCommand().ExecuteAsync(
            context,
            [
                new BaseObj<string>("--by"),
                new BaseObj<string>("Length"),
                new BaseObj<string>("--desc"),
                new BaseObj<FileSystemEntry>(smaller),
                new BaseObj<FileSystemEntry>(larger)
            ],
            CancellationToken.None);

        Assert.Equal([larger, smaller], context.Output.Select(output => output.UntypedValue).Cast<FileSystemEntry>().ToArray());
    }

    [Fact]
    public async Task GroupByProducesGroupedResults()
    {
        var first = new FileSystemEntry("docs", "C:/docs", true, null, DateTimeOffset.UtcNow);
        var second = new FileSystemEntry("bin", "C:/bin", true, null, DateTimeOffset.UtcNow);
        var third = new FileSystemEntry("notes.txt", "C:/notes.txt", false, 5, DateTimeOffset.UtcNow);
        var context = new CommandContext(new ShellSession());

        await new GroupByCommand().ExecuteAsync(
            context,
            [
                new BaseObj<string>("IsDirectory"),
                new BaseObj<FileSystemEntry>(first),
                new BaseObj<FileSystemEntry>(second),
                new BaseObj<FileSystemEntry>(third)
            ],
            CancellationToken.None);

        var groups = context.Output.Select(output => Assert.IsType<GroupResult>(output.UntypedValue)).ToArray();
        Assert.Contains(groups, group => Equals(group.Key, true) && group.Count == 2);
        Assert.Contains(groups, group => Equals(group.Key, false) && group.Count == 1);
    }

    [Fact]
    public async Task JsonSerializesMultipleValuesAsArray()
    {
        var context = new CommandContext(new ShellSession());

        await new JsonCommand().ExecuteAsync(
            context,
            [new BaseObj<int>(1), new BaseObj<int>(2), new BaseObj<int>(3)],
            CancellationToken.None);

        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(context.Output).UntypedValue));
        Assert.Equal([1, 2, 3], document.RootElement.EnumerateArray().Select(element => element.GetInt32()).ToArray());
    }

    [Fact]
    public async Task DispatcherResolvesYamlAliasFromBuiltInSerializerRegistration()
    {
        var dispatcher = new Dispatcher();
        BuiltInCommands.RegisterAll(dispatcher);
        var context = new CommandContext(new ShellSession());
        var entry = new FileSystemEntry("notes.txt", "C:/notes.txt", false, 5, DateTimeOffset.Parse("2024-01-01T00:00:00+00:00"));

        await dispatcher.DispatchAsync(
            "yaml",
            context,
            [],
            [new BaseObj<FileSystemEntry>(entry)],
            CancellationToken.None);

        var yaml = Assert.IsType<string>(Assert.Single(context.Output).UntypedValue);
        Assert.Contains("Name: notes.txt", yaml, StringComparison.Ordinal);
        Assert.Contains("Length: 5", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CsvSerializesObjectMembersAsColumns()
    {
        var first = new FileSystemEntry("alpha.txt", "C:/alpha.txt", false, 1, DateTimeOffset.UtcNow);
        var second = new FileSystemEntry("beta.txt", "C:/beta.txt", false, 2, DateTimeOffset.UtcNow);
        var context = new CommandContext(new ShellSession());

        await new CsvCommand().ExecuteAsync(
            context,
            [new BaseObj<FileSystemEntry>(first), new BaseObj<FileSystemEntry>(second)],
            CancellationToken.None);

        var csv = Assert.IsType<string>(Assert.Single(context.Output).UntypedValue);
        Assert.Contains("Name", csv, StringComparison.Ordinal);
        Assert.Contains("Path", csv, StringComparison.Ordinal);
        Assert.Contains("alpha.txt", csv, StringComparison.Ordinal);
        Assert.Contains("beta.txt", csv, StringComparison.Ordinal);
    }
}

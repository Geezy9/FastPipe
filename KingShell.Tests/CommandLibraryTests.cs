using KingShell.Extensions;
using KingShell.Models;
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
}

using var sessionCancellation = new CancellationTokenSource();
var session = new FastPipe.Extensions.ShellSession();
FastPipe.UserFacingConsole.Initialize(session);
var cancellationLock = new object();
CancellationTokenSource? activeCommandCancellation = null;

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;

    lock (cancellationLock)
    {
        if (activeCommandCancellation is { IsCancellationRequested: false })
        {
            activeCommandCancellation.Cancel();
            return;
        }

        sessionCancellation.Cancel();
    }
};

if (args.Length > 0)
{
    var commandLine = string.Join(' ', args).Trim();
    if (commandLine.Length == 0)
    {
        FastPipe.ConsoleRenderer.WriteError("A command is required.");
        Environment.ExitCode = 1;
    }
    else if (!await ExecuteCommandAsync(commandLine))
    {
        Environment.ExitCode = 1;
    }

    return;
}

FastPipe.ConsoleRenderer.WriteBanner();

while (!sessionCancellation.IsCancellationRequested)
{
    FastPipe.ConsoleRenderer.WritePrompt(session.WorkingDirectory);

    string? input;
    try
    {
        input = await Console.In.ReadLineAsync(sessionCancellation.Token);
    }
    catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
    {
        break;
    }
    catch (IOException exception)
    {
        FastPipe.ConsoleRenderer.WriteError($"Unable to read input: {exception.Message}");
        break;
    }

    if (input is null)
        break;

    var commandLine = input.Trim();
    if (commandLine.Length == 0)
        continue;

    await ExecuteCommandAsync(commandLine);
}

FastPipe.ConsoleRenderer.WriteGoodbye();

async Task<bool> ExecuteCommandAsync(string commandLine)
{
    using var commandCancellation = new CancellationTokenSource();
    lock (cancellationLock)
        activeCommandCancellation = commandCancellation;

    try
    {
        var context = await FastPipe.UserFacingConsole.PostJobAsync(commandLine, commandCancellation.Token);
        try
        {
            FastPipe.ConsoleRenderer.Render(context.Output);
        }
        finally
        {
            context.Release();
        }

        if (session.ConsumeClearRequest())
        {
            try
            {
                Spectre.Console.AnsiConsole.Clear();
            }
            catch (IOException exception)
            {
                FastPipe.ConsoleRenderer.WriteError($"Unable to clear the console: {exception.Message}");
            }
        }

        if (session.ExitRequested)
            sessionCancellation.Cancel();

        return true;
    }
    catch (OperationCanceledException) when (commandCancellation.IsCancellationRequested)
    {
        FastPipe.ConsoleRenderer.WriteWarning("Command cancelled.");
        return false;
    }
    catch (Exception exception)
    {
        FastPipe.ConsoleRenderer.WriteError(exception.Message);
        return false;
    }
    finally
    {
        lock (cancellationLock)
            activeCommandCancellation = null;
    }
}

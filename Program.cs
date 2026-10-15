using var sessionCancellation = new CancellationTokenSource();
var session = new KingShell.Extensions.ShellSession();
KingShell.UserFacingConsole.Initialize(session);
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

KingShell.ConsoleRenderer.WriteBanner();

while (!sessionCancellation.IsCancellationRequested)
{
    KingShell.ConsoleRenderer.WritePrompt(session.WorkingDirectory);

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
        KingShell.ConsoleRenderer.WriteError($"Unable to read input: {exception.Message}");
        break;
    }

    if (input is null)
        break;

    var commandLine = input.Trim();
    if (commandLine.Length == 0)
        continue;

    using var commandCancellation = new CancellationTokenSource();
    lock (cancellationLock)
        activeCommandCancellation = commandCancellation;

    try
    {
        var context = await KingShell.UserFacingConsole.PostJobAsync(commandLine, commandCancellation.Token);
        KingShell.ConsoleRenderer.Render(context.Output);

        if (session.ConsumeClearRequest())
        {
            try
            {
                Spectre.Console.AnsiConsole.Clear();
            }
            catch (IOException exception)
            {
                KingShell.ConsoleRenderer.WriteError($"Unable to clear the console: {exception.Message}");
            }
        }

        if (session.ExitRequested)
            sessionCancellation.Cancel();
    }
    catch (OperationCanceledException) when (commandCancellation.IsCancellationRequested)
    {
        KingShell.ConsoleRenderer.WriteNotice("Command cancelled.");
    }
    catch (Exception exception)
    {
        KingShell.ConsoleRenderer.WriteError(exception.Message);
    }
    finally
    {
        lock (cancellationLock)
            activeCommandCancellation = null;
    }
}

KingShell.ConsoleRenderer.WriteGoodbye();

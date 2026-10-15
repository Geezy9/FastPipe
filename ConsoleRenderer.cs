using KingShell.Extensions;
using KingShell.Models;
using Spectre.Console;

namespace KingShell;

internal static class ConsoleRenderer
{
    internal static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("KingShell").Color(Color.Cyan1));
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        AnsiConsole.MarkupLine("[grey]Type [cyan]help[/] for usage or [cyan]exit[/] to quit.[/]");
        AnsiConsole.WriteLine();
    }

    internal static void WritePrompt(string workingDirectory)
    {
        var directory = Path.GetFileName(Path.TrimEndingDirectorySeparator(workingDirectory));
        if (string.IsNullOrEmpty(directory))
            directory = workingDirectory;

        AnsiConsole.Markup($"[cyan]{Markup.Escape(Environment.UserName)}[/][grey]@[/][cyan]{Markup.Escape(directory)}[/] [grey]>[/] ");
    }

    internal static void Render(IEnumerable<BaseObj> outputs)
    {
        var entries = new List<FileSystemEntry>();

        foreach (var output in outputs)
        {
            if (output.UntypedValue is FileSystemEntry entry)
            {
                entries.Add(entry);
                continue;
            }

            FlushEntries(entries);
            AnsiConsole.MarkupLineInterpolated($"[grey93]{output.UntypedValue}[/]");
        }

        FlushEntries(entries);
    }

    internal static void WriteError(string message) =>
        AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] [grey93]{message}[/]");

    internal static void WriteNotice(string message) =>
        AnsiConsole.MarkupLineInterpolated($"[grey dim]{message}[/]");

    internal static void WriteGoodbye() =>
        AnsiConsole.MarkupLine("[cyan]Goodbye.[/]");

    private static void FlushEntries(List<FileSystemEntry> entries)
    {
        if (entries.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(Color.Grey))
            .AddColumn(new TableColumn("[cyan]Name[/]"))
            .AddColumn(new TableColumn("[cyan]Type[/]"))
            .AddColumn(new TableColumn("[cyan]Size[/]").RightAligned())
            .AddColumn(new TableColumn("[cyan]Modified[/]"));

        foreach (var entry in entries)
        {
            var name = entry.IsDirectory
                ? $"[cyan]{Markup.Escape(entry.Name)}[/]"
                : $"[grey93]{Markup.Escape(entry.Name)}[/]";

            table.AddRow(
                name,
                entry.IsDirectory ? "[grey]dir[/]" : "[grey]file[/]",
                $"[grey93]{FormatSize(entry.Length)}[/]",
                $"[grey]{entry.LastWriteTime.ToLocalTime():yyyy-MM-dd HH:mm}[/]");
        }

        AnsiConsole.Write(table);
        entries.Clear();
    }

    private static string FormatSize(long? length)
    {
        if (length is not { } bytes)
            return "--";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.#} {units[unit]}";
    }
}

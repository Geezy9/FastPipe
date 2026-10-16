using FastPipe.Extensions;
using FastPipe.Models;
using Spectre.Console;

namespace FastPipe;

internal static class ConsoleRenderer
{
    private const string AccentStyle = "deepskyblue2";
    private const string TextStyle = "grey78";
    private const string MutedStyle = "grey50";
    private const string SuccessStyle = "springgreen3";
    private const string WarningStyle = "gold3";
    private const string ErrorStyle = "indianred1";

    internal static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("FastPipe").Color(Color.DeepSkyBlue2));
        AnsiConsole.Write(new Rule().RuleStyle(MutedStyle));
        AnsiConsole.MarkupLine($"[{MutedStyle}]Type [{AccentStyle}]help[/] for usage or [{AccentStyle}]exit[/] to quit.[/]");
        AnsiConsole.WriteLine();
    }

    internal static void WritePrompt(string workingDirectory)
    {
        var directory = Path.GetFileName(Path.TrimEndingDirectorySeparator(workingDirectory));
        if (string.IsNullOrEmpty(directory))
            directory = workingDirectory;

        AnsiConsole.Markup($"[{AccentStyle}]{Markup.Escape(Environment.UserName)}[/][{MutedStyle}]@[/][{AccentStyle}]{Markup.Escape(directory)}[/] [{MutedStyle}]>[/] ");
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
            AnsiConsole.MarkupLine(RenderValue(output.UntypedValue));
        }

        FlushEntries(entries);
    }

    internal static void WriteError(string message) =>
        AnsiConsole.MarkupLine($"[{ErrorStyle}]Error:[/] [{TextStyle}]{Markup.Escape(message)}[/]");

    internal static void WriteWarning(string message) =>
        AnsiConsole.MarkupLine($"[{WarningStyle}]Warning:[/] [{TextStyle}]{Markup.Escape(message)}[/]");

    internal static void WriteNotice(string message) =>
        AnsiConsole.MarkupLine($"[{MutedStyle}]{Markup.Escape(message)}[/]");

    internal static void WriteGoodbye() =>
        AnsiConsole.MarkupLine($"[{AccentStyle}]Goodbye.[/]");

    private static void FlushEntries(List<FileSystemEntry> entries)
    {
        if (entries.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(Color.Grey50))
            .AddColumn(new TableColumn($"[{AccentStyle}]Name[/]"))
            .AddColumn(new TableColumn($"[{AccentStyle}]Type[/]"))
            .AddColumn(new TableColumn($"[{AccentStyle}]Size[/]").RightAligned())
            .AddColumn(new TableColumn($"[{AccentStyle}]Modified[/]"));

        foreach (var entry in entries)
        {
            var name = entry.IsDirectory
                ? $"[{AccentStyle}]{Markup.Escape(entry.Name)}[/]"
                : $"[{TextStyle}]{Markup.Escape(entry.Name)}[/]";

            table.AddRow(
                name,
                entry.IsDirectory ? $"[{MutedStyle}]dir[/]" : $"[{MutedStyle}]file[/]",
                $"[{TextStyle}]{FormatSize(entry.Length)}[/]",
                $"[{MutedStyle}]{entry.LastWriteTime.ToLocalTime():yyyy-MM-dd HH:mm}[/]");
        }

        AnsiConsole.Write(table);
        entries.Clear();
    }

    private static string RenderValue(object? value)
    {
        return value switch
        {
            null => $"[{MutedStyle}]<null>[/]",
            bool boolean => boolean
                ? $"[{SuccessStyle}]true[/]"
                : $"[{WarningStyle}]false[/]",
            sbyte or byte or short or ushort or int or uint or long or ulong or
                nint or nuint or float or double or decimal =>
                $"[{AccentStyle}]{Markup.Escape(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}[/]",
            _ => $"[{TextStyle}]{Markup.Escape(value.ToString() ?? string.Empty)}[/]"
        };
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

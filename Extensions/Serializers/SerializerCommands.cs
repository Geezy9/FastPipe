using System.Buffers;
using System.Text;
using System.Text.Json;
using KingShell.Extensions;
using KingShell.Extensions.Collections;
using KingShell.Interfaces;
using KingShell.Models;

namespace KingShell.Extensions.Serializers;

internal static class SerializerCommands
{
    internal static void RegisterAll(Dispatcher dispatcher)
    {
        dispatcher.Register(new JsonCommand());
        dispatcher.Register(new YmlCommand());
        dispatcher.Register(new CsvCommand());
    }
}

internal sealed class JsonCommand : IShellCmd
{
    public string Name => "json";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Serializes values to JSON.";
    public string Usage => "json <value...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        using var payload = new SerializerPayload(arguments);
        var writer = new ArrayBufferWriter<byte>();
        using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { Indented = true });

        if (payload.Count == 1)
        {
            JsonValueWriter.WriteJsonValue(jsonWriter, payload.GetValue(0), cancellationToken);
        }
        else
        {
            jsonWriter.WriteStartArray();
            for (var index = 0; index < payload.Count; index++)
                JsonValueWriter.WriteJsonValue(jsonWriter, payload.GetValue(index), cancellationToken);

            jsonWriter.WriteEndArray();
        }

        jsonWriter.Flush();
        context.Write(Encoding.UTF8.GetString(writer.WrittenSpan));
        return ValueTask.CompletedTask;
    }
}

internal sealed class YmlCommand : IShellCmd
{
    public string Name => "yml";
    public IReadOnlyList<string> Aliases => ["yaml"];
    public string Description => "Serializes values to YAML.";
    public string Usage => "yml <value...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        using var payload = new SerializerPayload(arguments);
        var graph = payload.Count == 1 ? payload.GetValue(0) : payload.ToArray();
        context.Write(YamlTextSerializer.Serialize(graph, cancellationToken));
        return ValueTask.CompletedTask;
    }
}

internal sealed class CsvCommand : IShellCmd
{
    public string Name => "csv";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Serializes values to CSV.";
    public string Usage => "csv <value...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(CsvSerializer.Serialize(arguments, cancellationToken));
        return ValueTask.CompletedTask;
    }
}

internal ref struct SerializerPayload
{
    private object?[]? _buffer;

    internal SerializerPayload(BaseObj[] arguments)
    {
        Count = arguments.Length;
        if (arguments.Length <= 1)
        {
            _buffer = null;
            SingleValue = arguments[0].UntypedValue;
            return;
        }

        _buffer = ArrayPool<object?>.Shared.Rent(arguments.Length);
        for (var index = 0; index < arguments.Length; index++)
            _buffer[index] = arguments[index].UntypedValue;
    }

    internal int Count { get; }

    internal object? SingleValue { get; }

    internal object? GetValue(int index) => Count == 1 ? SingleValue : _buffer![index];

    internal object?[] ToArray()
    {
        var values = new object?[Count];
        for (var index = 0; index < Count; index++)
            values[index] = GetValue(index);
        return values;
    }

    internal void Dispose()
    {
        if (_buffer is null)
            return;

        Array.Clear(_buffer, 0, Count);
        ArrayPool<object?>.Shared.Return(_buffer);
        _buffer = null;
    }
}

internal static class CsvSerializer
{
    internal static string Serialize(BaseObj[] arguments, CancellationToken cancellationToken)
    {
        using var writerLease = PooledStringBuilder.Rent(256);
        var writer = writerLease.Builder;

        var mode = DetermineMode(arguments);
        if (mode == CsvMode.Scalar)
        {
            writer.AppendLine("Value");
            foreach (var argument in arguments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteCell(writer, argument.UntypedValue);
                writer.AppendLine();
            }

            return writer.ToString();
        }

        using var columns = new RentedBuffer<string>(4);
        var seenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendColumns(argument.UntypedValue, seenColumns, columns);
        }

        if (columns.Count == 0)
        {
            writer.AppendLine("Value");
            foreach (var argument in arguments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteCell(writer, argument.UntypedValue);
                writer.AppendLine();
            }

            return writer.ToString();
        }

        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
                writer.Append(',');

            WriteCell(writer, columns.Buffer[index]);
        }

        writer.AppendLine();

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                if (columnIndex > 0)
                    writer.Append(',');

                StructuredDataAccess.TryGetMemberValue(argument.UntypedValue, columns.Buffer[columnIndex], out var value);
                WriteCell(writer, value);
            }

            writer.AppendLine();
        }

        return writer.ToString();
    }

    private static CsvMode DetermineMode(BaseObj[] arguments)
    {
        foreach (var argument in arguments)
        {
            if (!StructuredDataAccess.IsScalar(argument.UntypedValue))
                return CsvMode.Object;
        }

        return CsvMode.Scalar;
    }

    private static void AppendColumns(object? value, HashSet<string> seenColumns, RentedBuffer<string> columns)
    {
        foreach (var member in StructuredDataAccess.EnumerateMembers(value))
        {
            if (seenColumns.Add(member.Key))
                columns.Add(member.Key);
        }
    }

    private static void WriteCell(StringBuilder writer, object? value)
    {
        var text = value is null ? string.Empty : StructuredDataAccess.FormatScalar(value);
        if (text.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            writer.Append(text);
            return;
        }

        writer.Append('"');
        foreach (var character in text)
        {
            if (character == '"')
                writer.Append('"');

            writer.Append(character);
        }

        writer.Append('"');
    }

    private enum CsvMode
    {
        Scalar,
        Object,
    }
}

internal static class JsonValueWriter
{
    internal static void WriteJsonValue(Utf8JsonWriter writer, object? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case char character:
                writer.WriteStringValue(character.ToString());
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case byte number:
                writer.WriteNumberValue(number);
                return;
            case sbyte number:
                writer.WriteNumberValue(number);
                return;
            case short number:
                writer.WriteNumberValue(number);
                return;
            case ushort number:
                writer.WriteNumberValue(number);
                return;
            case int number:
                writer.WriteNumberValue(number);
                return;
            case uint number:
                writer.WriteNumberValue(number);
                return;
            case long number:
                writer.WriteNumberValue(number);
                return;
            case ulong number:
                writer.WriteNumberValue(number);
                return;
            case float number:
                writer.WriteNumberValue(number);
                return;
            case double number:
                writer.WriteNumberValue(number);
                return;
            case decimal number:
                writer.WriteNumberValue(number);
                return;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset);
                return;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                return;
            case Guid guid:
                writer.WriteStringValue(guid);
                return;
            case TimeSpan timeSpan:
                writer.WriteStringValue(timeSpan.ToString("c"));
                return;
        }

        if (value.GetType().IsEnum)
        {
            writer.WriteStringValue(value.ToString());
            return;
        }

        var wroteObject = false;
        foreach (var member in StructuredDataAccess.EnumerateMembers(value))
        {
            if (!wroteObject)
            {
                writer.WriteStartObject();
                wroteObject = true;
            }

            writer.WritePropertyName(member.Key);
            WriteJsonValue(writer, member.Value, cancellationToken);
        }

        if (wroteObject)
        {
            writer.WriteEndObject();
            return;
        }

        if (StructuredDataAccess.TryEnumerateSequence(value, out var sequence))
        {
            writer.WriteStartArray();
            foreach (var item in sequence)
                WriteJsonValue(writer, item, cancellationToken);

            writer.WriteEndArray();
            return;
        }

        writer.WriteStringValue(value.ToString());
    }
}

internal static class YamlTextSerializer
{
    internal static string Serialize(object? value, CancellationToken cancellationToken)
    {
        using var writerLease = PooledStringBuilder.Rent(256);
        var writer = writerLease.Builder;
        WriteYamlValue(writer, value, 0, cancellationToken);
        return writer.ToString().TrimEnd();
    }

    private static void WriteYamlValue(StringBuilder writer, object? value, int indent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (StructuredDataAccess.IsScalar(value))
        {
            AppendIndent(writer, indent);
            writer.AppendLine(FormatYamlScalar(value));
            return;
        }

        var wroteObject = false;
        foreach (var member in StructuredDataAccess.EnumerateMembers(value))
        {
            wroteObject = true;
            AppendIndent(writer, indent);
            writer.Append(member.Key);
            writer.Append(':');

            if (StructuredDataAccess.IsComplex(member.Value))
            {
                writer.AppendLine();
                WriteYamlValue(writer, member.Value, indent + 2, cancellationToken);
            }
            else
            {
                writer.Append(' ');
                writer.AppendLine(FormatYamlScalar(member.Value));
            }
        }

        if (wroteObject)
            return;

        if (StructuredDataAccess.TryEnumerateSequence(value, out var sequence))
        {
            var hasItems = false;
            foreach (var item in sequence)
            {
                hasItems = true;
                AppendIndent(writer, indent);
                writer.Append('-');
                if (StructuredDataAccess.IsComplex(item))
                {
                    writer.AppendLine();
                    WriteYamlValue(writer, item, indent + 2, cancellationToken);
                }
                else
                {
                    writer.Append(' ');
                    writer.AppendLine(FormatYamlScalar(item));
                }
            }

            if (!hasItems)
            {
                AppendIndent(writer, indent);
                writer.AppendLine("[]");
            }

            return;
        }

        AppendIndent(writer, indent);
        writer.AppendLine(FormatYamlScalar(value));
    }

    private static string FormatYamlScalar(object? value)
    {
        if (value is null)
            return "null";

        var text = StructuredDataAccess.FormatScalar(value);
        if (text.Length == 0)
            return "''";

        return RequiresQuotedYaml(text)
            ? $"'{text.Replace("'", "''", StringComparison.Ordinal)}'"
            : text;
    }

    private static bool RequiresQuotedYaml(string text)
    {
        if (text.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return text.StartsWith([' ', '-', '?', ':', '@', '`']) ||
               text.EndsWith(' ') ||
               text.IndexOfAny([':', '#', '\r', '\n', '[', ']', '{', '}', ',']) >= 0;
    }

    private static void AppendIndent(StringBuilder writer, int indent)
    {
        if (indent > 0)
            writer.Append(' ', indent);
    }
}
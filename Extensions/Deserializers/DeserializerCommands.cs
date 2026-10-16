using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FastPipe.Extensions.Collections;
using FastPipe.Interfaces;
using FastPipe.Models;

namespace FastPipe.Extensions.Deserializers;

internal static class DeserializerCommands
{
    internal static void RegisterAll(Dispatcher dispatcher)
    {
        dispatcher.Register(new DJsonCommand());
        dispatcher.Register(new DYmlCommand());
        dispatcher.Register(new DCsvCommand());
    }
}

internal sealed class DJsonCommand : IShellCmd
{
    public string Name => "djson";
    public IReadOnlyList<string> Aliases => ["from-json", "as-json"];
    public string Description => "Deserializes JSON text into typed values.";
    public string Usage => "djson <json...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        using var payload = new TextPayload(arguments);

        for (var index = 0; index < payload.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(JsonTextDeserializer.Deserialize(payload.GetText(index), cancellationToken));
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class DYmlCommand : IShellCmd
{
    public string Name => "dyml";
    public IReadOnlyList<string> Aliases => ["from-yml", "as-yml"];
    public string Description => "Deserializes YAML text into typed values.";
    public string Usage => "dyml <yaml...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        using var payload = new TextPayload(arguments);

        for (var index = 0; index < payload.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(YamlTextDeserializer.Deserialize(payload.GetText(index), cancellationToken));
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class DCsvCommand : IShellCmd
{
    public string Name => "dcsv";
    public IReadOnlyList<string> Aliases => ["from-csv", "as-csv"];
    public string Description => "Deserializes CSV text into typed values.";
    public string Usage => "dcsv <csv...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        using var payload = new TextPayload(arguments);

        for (var index = 0; index < payload.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var value in CsvTextDeserializer.Deserialize(payload.GetText(index), cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Write(value);
            }
        }

        return ValueTask.CompletedTask;
    }
}

internal ref struct TextPayload
{
    private string[]? _buffer;

    internal TextPayload(BaseObj[] arguments)
    {
        Count = arguments.Length;
        if (arguments.Length <= 1)
        {
            _buffer = null;
            SingleText = CommandArguments.GetText(arguments[0], "text");
            return;
        }

        _buffer = ArrayPool<string>.Shared.Rent(arguments.Length);
        for (var index = 0; index < arguments.Length; index++)
            _buffer[index] = CommandArguments.GetText(arguments[index], "text");
    }

    internal int Count { get; }

    internal string SingleText { get; }

    internal string GetText(int index) => Count == 1 ? SingleText : _buffer![index];

    internal void Dispose()
    {
        if (_buffer is null)
            return;

        Array.Clear(_buffer, 0, Count);
        ArrayPool<string>.Shared.Return(_buffer);
        _buffer = null;
    }
}

internal static class JsonTextDeserializer
{
    internal static object? Deserialize(string text, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(text);
        return ConvertElement(document.RootElement, cancellationToken);
    }

    private static object? ConvertElement(JsonElement element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertNumber(element),
            JsonValueKind.Array => ConvertArray(element, cancellationToken),
            JsonValueKind.Object => ConvertObject(element, cancellationToken),
            _ => element.GetRawText(),
        };
    }

    private static object ConvertNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var int32Value))
            return int32Value;

        if (element.TryGetInt64(out var int64Value))
            return int64Value;

        if (element.TryGetDecimal(out var decimalValue))
            return decimalValue;

        return element.GetDouble();
    }

    private static List<object?> ConvertArray(JsonElement element, CancellationToken cancellationToken)
    {
        var values = new List<object?>();
        foreach (var item in element.EnumerateArray())
            values.Add(ConvertElement(item, cancellationToken));

        return values;
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement element, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            values[property.Name] = ConvertElement(property.Value, cancellationToken);

        return values;
    }
}

internal static class CsvTextDeserializer
{
    internal static IEnumerable<object?> Deserialize(string text, CancellationToken cancellationToken)
    {
        var rows = ParseRows(text, cancellationToken);
        if (rows.Count == 0)
            yield break;

        var headers = rows[0];
        if (headers.Count == 1 && headers[0].Equals("Value", StringComparison.OrdinalIgnoreCase))
        {
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[rowIndex];
                yield return row.Count == 0 ? null : ParseScalar(row[0]);
            }

            yield break;
        }

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[rowIndex];
            var value = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var columnName = headers[columnIndex];
                if (string.IsNullOrWhiteSpace(columnName))
                    continue;

                var cell = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                value[columnName] = ParseScalar(cell);
            }

            yield return value;
        }
    }

    private static List<List<string>> ParseRows(string text, CancellationToken cancellationToken)
    {
        var rows = new List<List<string>>();
        using var fieldLease = PooledStringBuilder.Rent(64);
        var fieldBuilder = fieldLease.Builder;
        var currentRow = new List<string>();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        fieldBuilder.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    fieldBuilder.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRow.Add(fieldBuilder.ToString());
                    fieldBuilder.Clear();
                    break;
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                        index++;

                    FinalizeRow(rows, currentRow, fieldBuilder);
                    break;
                case '\n':
                    FinalizeRow(rows, currentRow, fieldBuilder);
                    break;
                default:
                    fieldBuilder.Append(character);
                    break;
            }
        }

        if (inQuotes)
            throw new FormatException("CSV text contains an unterminated quoted field.");

        if (fieldBuilder.Length > 0 || currentRow.Count > 0 || text.Length > 0)
            FinalizeRow(rows, currentRow, fieldBuilder);

        return rows;
    }

    private static void FinalizeRow(List<List<string>> rows, List<string> row, StringBuilder fieldBuilder)
    {
        row.Add(fieldBuilder.ToString());
        fieldBuilder.Clear();
        rows.Add([.. row]);
        row.Clear();
    }

    private static object? ParseScalar(string text) => ScalarParser.Parse(text);
}

internal static class YamlTextDeserializer
{
    internal static object? Deserialize(string text, CancellationToken cancellationToken)
    {
        var parser = new YamlParser(text, cancellationToken);
        return parser.ParseDocument();
    }

    private sealed class YamlParser(string source, CancellationToken cancellationToken)
    {
        private readonly string[] _lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        private int _index;

        internal object? ParseDocument()
        {
            SkipEmpty();
            if (_index >= _lines.Length)
                return null;

            var indent = GetIndent(_lines[_index]);
            return ParseNode(indent);
        }

        private object? ParseNode(int indent)
        {
            SkipEmpty();
            if (_index >= _lines.Length)
                return null;

            var currentLine = _lines[_index];
            var currentIndent = GetIndent(currentLine);
            if (currentIndent < indent)
                return null;

            var content = currentLine.AsSpan(currentIndent).TrimStart();
            return content.StartsWith("-".AsSpan(), StringComparison.Ordinal)
                ? ParseSequence(indent)
                : ParseMappingOrScalar(indent);
        }

        private object? ParseMappingOrScalar(int indent)
        {
            var currentLine = _lines[_index];
            var currentIndent = GetIndent(currentLine);
            if (currentIndent != indent)
                throw new FormatException("Invalid YAML indentation.");

            var content = currentLine.AsSpan(currentIndent);
            if (!TrySplitKeyValue(content, out var firstKey, out var firstValue))
            {
                _index++;
                return ScalarParser.Parse(content.Trim().ToString());
            }

            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            AddMappedValue(map, firstKey, firstValue, indent);

            while (_index < _lines.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SkipEmpty();
                if (_index >= _lines.Length)
                    break;

                currentLine = _lines[_index];
                currentIndent = GetIndent(currentLine);
                if (currentIndent < indent)
                    break;

                if (currentIndent > indent)
                    throw new FormatException("Invalid YAML indentation.");

                content = currentLine.AsSpan(currentIndent);
                if (content.TrimStart().StartsWith("-".AsSpan(), StringComparison.Ordinal))
                    break;

                if (!TrySplitKeyValue(content, out var key, out var valueText))
                    throw new FormatException("YAML mapping entries must be key/value pairs.");

                AddMappedValue(map, key, valueText, indent);
            }

            return map;
        }

        private List<object?> ParseSequence(int indent)
        {
            var sequence = new List<object?>();

            while (_index < _lines.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SkipEmpty();
                if (_index >= _lines.Length)
                    break;

                var currentLine = _lines[_index];
                var currentIndent = GetIndent(currentLine);
                if (currentIndent < indent)
                    break;

                if (currentIndent > indent)
                    throw new FormatException("Invalid YAML indentation.");

                var content = currentLine.AsSpan(currentIndent).TrimStart();
                if (!content.StartsWith("-".AsSpan(), StringComparison.Ordinal))
                    break;

                var remainder = content[1..].TrimStart();
                _index++;

                if (remainder.Length == 0)
                {
                    sequence.Add(ParseNode(indent + 2));
                    continue;
                }

                if (TrySplitKeyValue(remainder, out var key, out var valueText))
                {
                    var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    map[key.ToString()] = valueText.Length == 0
                        ? ParseNode(indent + 2)
                        : ScalarParser.Parse(valueText.ToString());
                    sequence.Add(map);
                    continue;
                }

                sequence.Add(ScalarParser.Parse(remainder.ToString()));
            }

            return sequence;
        }

        private void AddMappedValue(Dictionary<string, object?> map, ReadOnlySpan<char> key, ReadOnlySpan<char> valueText, int indent)
        {
            var normalizedKey = key.Trim().ToString();
            _index++;

            if (valueText.Length == 0)
            {
                map[normalizedKey] = ParseNode(indent + 2);
                return;
            }

            map[normalizedKey] = ScalarParser.Parse(valueText.ToString());
        }

        private void SkipEmpty()
        {
            while (_index < _lines.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(_lines[_index]))
                    break;

                _index++;
            }
        }

        private static bool TrySplitKeyValue(ReadOnlySpan<char> content, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
        {
            var separator = content.IndexOf(':');
            if (separator <= 0)
            {
                key = default;
                value = default;
                return false;
            }

            key = content[..separator];
            value = content[(separator + 1)..].TrimStart();
            return true;
        }

        private static int GetIndent(string line)
        {
            var indent = 0;
            while (indent < line.Length && line[indent] == ' ')
                indent++;

            return indent;
        }
    }
}

internal static class ScalarParser
{
    internal static object? Parse(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
            return trimmed[1..^1].Replace("''", "'", StringComparison.Ordinal);

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            return intValue;

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            return longValue;

        if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            return decimalValue;

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
            return dateTimeOffset;

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
            return dateTime;

        return trimmed;
    }
}

using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using FastPipe.Extensions;
using FastPipe.Interfaces;
using FastPipe.Models;

namespace FastPipe.Extensions.Collections;

internal static class CollectionCommands
{
    internal static void RegisterAll(Dispatcher dispatcher)
    {
        dispatcher.Register(new WhereCommand());
        dispatcher.Register(new SelectCommand());
        dispatcher.Register(new MapCommand());
        dispatcher.Register(new TakeCommand());
        dispatcher.Register(new SortCommand());
        dispatcher.Register(new GroupByCommand());
        dispatcher.Register(new UniqCommand());
        dispatcher.Register(new SplitCommand());
        dispatcher.Register(new FlattenCommand());
        dispatcher.Register(new ContainsCommand());
        dispatcher.Register(new ReplaceCommand());
        dispatcher.Register(new MinCommand());
        dispatcher.Register(new MaxCommand());
        dispatcher.Register(new SumCommand());
        dispatcher.Register(new CountCommand());
        dispatcher.Register(new GetPropertiesCommand());
    }
}

internal sealed class WhereCommand : IShellCmd
{
    public string Name => "where";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Filters piped values by a public member comparison.";
    public string Usage => "where <member> <eq|ne|gt|ge|lt|le|contains|starts-with|ends-with> <value> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 4)
            throw new ArgumentException($"Usage: {Usage}");

        var memberName = CommandArguments.GetText(arguments[0], "member");
        var operation = CommandArguments.GetText(arguments[1], "operator");
        var expected = arguments[2].UntypedValue;

        foreach (var argument in arguments.AsSpan(3))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CollectionMemberAccess.TryGetMemberValue(argument.UntypedValue, memberName, out var actual))
                continue;

            if (CollectionComparers.Matches(actual, expected, operation))
                context.Write(argument);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class SelectCommand : IShellCmd
{
    public string Name => "select";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Projects a public member from each piped value.";
    public string Usage => "select <member> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        var memberName = CommandArguments.GetText(arguments[0], "member");
        foreach (var argument in arguments.AsSpan(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CollectionMemberAccess.TryGetMemberValue(argument.UntypedValue, memberName, out var value))
                context.Write(value);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class MapCommand : IShellCmd
{
    public string Name => "map";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Projects a public member from each piped value.";
    public string Usage => "map <member> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        var memberName = CommandArguments.GetText(arguments[0], "member");
        foreach (var argument in arguments.AsSpan(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CollectionMemberAccess.TryGetMemberValue(argument.UntypedValue, memberName, out var value))
                context.Write(value);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class UniqCommand : IShellCmd
{
    public string Name => "uniq";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Writes distinct piped values.";
    public string Usage => "uniq [--by <member>] <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var options = CollectionCommandArguments.ParseBy(arguments, Usage);
        if (options.InputStartIndex >= arguments.Length)
            throw new ArgumentException($"Usage: {Usage}");

        var seen = new List<object?>();
        for (var index = options.InputStartIndex; index < arguments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var argument = arguments[index];
            var key = argument.UntypedValue;
            if (options.MemberName is not null && !CollectionMemberAccess.TryGetMemberValue(key, options.MemberName, out key))
                continue;

            if (seen.Any(existing => CollectionComparers.AreEqual(existing, key)))
                continue;

            seen.Add(key);
            context.Write(argument);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class SplitCommand : IShellCmd
{
    public string Name => "split";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Splits string values using a delimiter.";
    public string Usage => "split <delimiter> <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        var delimiter = CommandArguments.GetText(arguments[0], "delimiter");
        if (delimiter.Length == 0)
            throw new ArgumentException("delimiter must not be empty.");

        foreach (var argument in arguments.AsSpan(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = CommandArguments.GetText(argument, "text");
            foreach (var part in text.Split([delimiter], StringSplitOptions.None))
                context.Write(part);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class FlattenCommand : IShellCmd
{
    public string Name => "flatten";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Flattens one level of sequence values.";
    public string Usage => "flatten <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!StructuredDataAccess.TryEnumerateSequence(argument.UntypedValue, out var sequence))
            {
                context.Write(argument);
                continue;
            }

            foreach (var item in sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Write(item);
            }
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class ContainsCommand : IShellCmd
{
    public string Name => "contains";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Tests whether piped values contain a target value.";
    public string Usage => "contains <value> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        var expected = arguments[0].UntypedValue;
        var found = false;
        for (var index = 1; index < arguments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CollectionComparers.AreEqual(arguments[index].UntypedValue, expected))
                continue;

            found = true;
            break;
        }

        context.Write(found);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ReplaceCommand : IShellCmd
{
    public string Name => "replace";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Replaces text in each string value.";
    public string Usage => "replace <old> <new> <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 3)
            throw new ArgumentException($"Usage: {Usage}");

        var oldValue = CommandArguments.GetText(arguments[0], "old");
        var newValue = CommandArguments.GetText(arguments[1], "new");
        foreach (var argument in arguments.AsSpan(2))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = CommandArguments.GetText(argument, "text");
            context.Write(text.Replace(oldValue, newValue, StringComparison.Ordinal));
        }

        return ValueTask.CompletedTask;
    }
}

internal abstract class AggregateCommandBase : IShellCmd
{
    public abstract string Name { get; }
    public IReadOnlyList<string> Aliases => [];
    public abstract string Description { get; }
    public virtual string Usage => $"{Name} [--by <member>] <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var options = CollectionCommandArguments.ParseBy(arguments, Usage);
        if (options.InputStartIndex >= arguments.Length)
            throw new ArgumentException($"Usage: {Usage}");

        var hasValue = false;
        object? selectedValue = null;
        BaseObj? selectedItem = null;

        for (var index = options.InputStartIndex; index < arguments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var argument = arguments[index];
            var value = argument.UntypedValue;
            if (options.MemberName is not null && !CollectionMemberAccess.TryGetMemberValue(value, options.MemberName, out value))
                continue;

            if (!hasValue)
            {
                hasValue = true;
                selectedValue = value;
                selectedItem = argument;
                continue;
            }

            if (ShouldReplace(value, selectedValue!))
            {
                selectedValue = value;
                selectedItem = argument;
            }
        }

        if (!hasValue)
            throw new ArgumentException($"Usage: {Usage}");

        context.Write(options.MemberName is null ? selectedValue : selectedItem!);
        return ValueTask.CompletedTask;
    }

    protected abstract bool ShouldReplace(object? candidate, object? current);
}

internal sealed class MinCommand : AggregateCommandBase
{
    public override string Name => "min";
    public override string Description => "Returns the minimum value or item by member.";

    protected override bool ShouldReplace(object? candidate, object? current) =>
        CollectionComparers.Compare(candidate, current) < 0;
}

internal sealed class MaxCommand : AggregateCommandBase
{
    public override string Name => "max";
    public override string Description => "Returns the maximum value or item by member.";

    protected override bool ShouldReplace(object? candidate, object? current) =>
        CollectionComparers.Compare(candidate, current) > 0;
}

internal sealed class SumCommand : IShellCmd
{
    public string Name => "sum";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Sums numeric values.";
    public string Usage => "sum <number> <number...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        decimal total = 0;
        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CollectionComparers.TryConvertValue(argument.UntypedValue, typeof(decimal), out var converted) || converted is not decimal number)
                throw new ArgumentException("number must be numeric.");

            total += number;
        }

        context.Write(total);
        return ValueTask.CompletedTask;
    }
}

internal sealed class CountCommand : IShellCmd
{
    public string Name => "count";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Counts piped values.";
    public string Usage => "count <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write((long)arguments.Length);
        return ValueTask.CompletedTask;
    }
}

internal sealed class GetPropertiesCommand : IShellCmd
{
    public string Name => "get-properties";
    public IReadOnlyList<string> Aliases => ["props"];
    public string Description => "Extracts object member keys, values, or key/value pairs.";
    public string Usage => "get-properties <keys|values|pairs> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        var mode = GetMode(CommandArguments.GetText(arguments[0], "mode"));
        foreach (var argument in arguments.AsSpan(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var member in StructuredDataAccess.EnumerateMembers(argument.UntypedValue))
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (mode)
                {
                    case PropertyProjectionMode.Keys:
                        context.Write(member.Key);
                        break;
                    case PropertyProjectionMode.Values:
                        context.Write(member.Value);
                        break;
                    case PropertyProjectionMode.Pairs:
                        context.Write(new PropertyPair(member.Key, member.Value));
                        break;
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    private static PropertyProjectionMode GetMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "keys" => PropertyProjectionMode.Keys,
            "values" => PropertyProjectionMode.Values,
            "pairs" => PropertyProjectionMode.Pairs,
            _ => throw new ArgumentException("mode must be one of keys, values, or pairs."),
        };
    }
}

internal enum PropertyProjectionMode
{
    Keys,
    Values,
    Pairs
}

internal sealed record PropertyPair(string Key, object? Value) : IStructuredValue
{
    public bool TryGetMemberValue(string memberName, out object? value)
    {
        switch (memberName)
        {
            case "Key":
                value = Key;
                return true;
            case "Value":
                value = Value;
                return true;
            default:
                value = null;
                return false;
        }
    }

    public IEnumerable<KeyValuePair<string, object?>> EnumerateMembers()
    {
        yield return new KeyValuePair<string, object?>("Key", Key);
        yield return new KeyValuePair<string, object?>("Value", Value);
    }
}

internal sealed class TakeCommand : IShellCmd
{
    public string Name => "take";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Takes the first N values from the pipeline.";
    public string Usage => "take <count> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        var count = CollectionCommandArguments.GetCount(arguments[0], "count");
        var available = Math.Min(count, arguments.Length - 1);
        for (var index = 0; index < available; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(arguments[index + 1]);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class SortCommand : IShellCmd
{
    public string Name => "sort";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Sorts piped values or a selected member.";
    public string Usage => "sort [--by <member>] [--desc] <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        var options = CollectionCommandArguments.ParseSort(arguments, Usage);
        using var buffer = new RentedBuffer<BaseObj>(Math.Max(options.InputStartIndex, arguments.Length - options.InputStartIndex));

        for (var index = options.InputStartIndex; index < arguments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            buffer.Add(arguments[index]);
        }

        if (buffer.Count == 0)
            throw new ArgumentException($"Usage: {Usage}");

        Array.Sort(buffer.Buffer, 0, buffer.Count, new BaseObjSortComparer(options.MemberName, options.Descending));
        for (var index = 0; index < buffer.Count; index++)
            context.Write(buffer.Buffer[index]);

        return ValueTask.CompletedTask;
    }
}

internal sealed class GroupByCommand : IShellCmd
{
    public string Name => "group-by";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Groups piped values by a public member.";
    public string Usage => "group-by <member> <input...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        var memberName = CommandArguments.GetText(arguments[0], "member");
        var groups = new Dictionary<GroupKey, List<object?>>(GroupKeyComparer.Instance);

        foreach (var argument in arguments.AsSpan(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CollectionMemberAccess.TryGetMemberValue(argument.UntypedValue, memberName, out var key))
                continue;

            var groupKey = new GroupKey(key);
            if (!groups.TryGetValue(groupKey, out var items))
            {
                items = [];
                groups.Add(groupKey, items);
            }

            items.Add(argument.UntypedValue);
        }

        foreach (var pair in groups)
            context.Write(new GroupResult(pair.Key.Value, pair.Value.Count, pair.Value.ToArray()));

        return ValueTask.CompletedTask;
    }
}

internal sealed record GroupResult(object? Key, int Count, IReadOnlyList<object?> Items) : IStructuredValue
{
    public override string ToString() => $"Key={CollectionFormatting.FormatValue(Key)} Count={Count}";

    public bool TryGetMemberValue(string memberName, out object? value)
    {
        switch (memberName)
        {
            case "Key":
                value = Key;
                return true;
            case "Count":
                value = Count;
                return true;
            case "Items":
                value = Items;
                return true;
            default:
                value = null;
                return false;
        }
    }

    public IEnumerable<KeyValuePair<string, object?>> EnumerateMembers()
    {
        yield return new KeyValuePair<string, object?>("Key", Key);
        yield return new KeyValuePair<string, object?>("Count", Count);
        yield return new KeyValuePair<string, object?>("Items", Items);
    }
}

internal static class CollectionCommandArguments
{
    internal static int GetCount(BaseObj argument, string argumentName)
    {
        var (value, isIntegral) = CommandArguments.GetNumber(argument, argumentName);
        if (!isIntegral || value < 0 || value > int.MaxValue)
            throw new ArgumentException($"{argumentName} must be a non-negative integer.");

        return (int)value;
    }

    internal static SortOptions ParseSort(BaseObj[] arguments, string usage)
    {
        string? memberName = null;
        var descending = false;
        var index = 0;

        while (index < arguments.Length)
        {
            var option = arguments[index].UntypedValue as string;
            if (string.IsNullOrEmpty(option))
                break;

            if (option.Equals("--desc", StringComparison.OrdinalIgnoreCase))
            {
                descending = true;
                index++;
                continue;
            }

            if (option.Equals("--by", StringComparison.OrdinalIgnoreCase))
            {
                if (memberName is not null || index + 1 >= arguments.Length)
                    throw new ArgumentException($"Usage: {usage}");

                memberName = CommandArguments.GetText(arguments[index + 1], "member");
                index += 2;
                continue;
            }

            break;
        }

        return new SortOptions(memberName, descending, index);
    }

    internal static ByOptions ParseBy(BaseObj[] arguments, string usage)
    {
        if (arguments.Length == 0)
            return new ByOptions(null, 0);

        var option = arguments[0].UntypedValue as string;
        if (!string.Equals(option, "--by", StringComparison.OrdinalIgnoreCase))
            return new ByOptions(null, 0);

        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {usage}");

        return new ByOptions(CommandArguments.GetText(arguments[1], "member"), 2);
    }

    internal readonly record struct SortOptions(string? MemberName, bool Descending, int InputStartIndex);
    internal readonly record struct ByOptions(string? MemberName, int InputStartIndex);
}

internal static class CollectionFormatting
{
    internal static string FormatValue(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
}

internal static class CollectionComparers
{
    internal static bool Matches(object? actual, object? expected, string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "eq" => Compare(actual, expected) == 0,
            "ne" => Compare(actual, expected) != 0,
            "gt" => Compare(actual, expected) > 0,
            "ge" => Compare(actual, expected) >= 0,
            "lt" => Compare(actual, expected) < 0,
            "le" => Compare(actual, expected) <= 0,
            "contains" => TextPredicate(actual, expected, static (left, right) => left.Contains(right, StringComparison.OrdinalIgnoreCase)),
            "starts-with" => TextPredicate(actual, expected, static (left, right) => left.StartsWith(right, StringComparison.OrdinalIgnoreCase)),
            "ends-with" => TextPredicate(actual, expected, static (left, right) => left.EndsWith(right, StringComparison.OrdinalIgnoreCase)),
            _ => throw new ArgumentException("operator must be one of eq, ne, gt, ge, lt, le, contains, starts-with, or ends-with."),
        };
    }

    internal static int Compare(object? left, object? right)
    {
        if (left is null)
            return right is null ? 0 : -1;

        if (right is null)
            return 1;

        if (TryConvertValue(right, left.GetType(), out var convertedRight))
            right = convertedRight;

        if (TryGetNumeric(left, out var leftNumber) && TryGetNumeric(right, out var rightNumber))
            return leftNumber.CompareTo(rightNumber);

        if (left is DateTimeOffset leftOffset && right is DateTimeOffset rightOffset)
            return leftOffset.CompareTo(rightOffset);

        if (left is DateTime leftDateTime && right is DateTime rightDateTime)
            return leftDateTime.CompareTo(rightDateTime);

        if (left is string leftText && right is string rightText)
            return string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);

        if (left is IComparable comparable && left.GetType().IsInstanceOfType(right))
            return comparable.CompareTo(right);

        return string.Compare(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool AreEqual(object? left, object? right) => Compare(left, right) == 0;

    private static bool TextPredicate(object? actual, object? expected, Func<string, string, bool> predicate)
    {
        if (actual is null || expected is null)
            return false;

        var actualText = Convert.ToString(actual, CultureInfo.InvariantCulture);
        var expectedText = Convert.ToString(expected, CultureInfo.InvariantCulture);
        return actualText is not null && expectedText is not null && predicate(actualText, expectedText);
    }

    internal static bool TryConvertValue(object? value, Type targetType, out object? converted)
    {
        if (value is null)
        {
            converted = null;
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
        }

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullableType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (value is string text)
        {
            if (nonNullableType == typeof(string))
            {
                converted = text;
                return true;
            }

            if (nonNullableType == typeof(bool) && bool.TryParse(text, out var boolean))
            {
                converted = boolean;
                return true;
            }

            if (nonNullableType == typeof(Guid) && Guid.TryParse(text, out var guid))
            {
                converted = guid;
                return true;
            }

            if (nonNullableType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
            {
                converted = dateTimeOffset;
                return true;
            }

            if (nonNullableType == typeof(DateTime) && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
            {
                converted = dateTime;
                return true;
            }

            if (nonNullableType.IsEnum)
            {
                try
                {
                    converted = Enum.Parse(nonNullableType, text, ignoreCase: true);
                    return true;
                }
                catch
                {
                }
            }
        }

        try
        {
            converted = Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            converted = value;
            return false;
        }
    }

    private static bool TryGetNumeric(object? value, out decimal number)
    {
        switch (value)
        {
            case byte typed:
                number = typed;
                return true;
            case sbyte typed:
                number = typed;
                return true;
            case short typed:
                number = typed;
                return true;
            case ushort typed:
                number = typed;
                return true;
            case int typed:
                number = typed;
                return true;
            case uint typed:
                number = typed;
                return true;
            case long typed:
                number = typed;
                return true;
            case ulong typed:
                number = typed;
                return true;
            case float typed:
                number = (decimal)typed;
                return true;
            case double typed:
                number = (decimal)typed;
                return true;
            case decimal typed:
                number = typed;
                return true;
            default:
                number = 0;
                return false;
        }
    }
}

internal static class CollectionMemberAccess
{
    internal static bool TryGetMemberValue(object? source, string memberName, out object? value) =>
        StructuredDataAccess.TryGetMemberValue(source, memberName, out value);
}

internal readonly record struct GroupKey(object? Value);

internal sealed class GroupKeyComparer : IEqualityComparer<GroupKey>
{
    internal static GroupKeyComparer Instance { get; } = new();

    public bool Equals(GroupKey x, GroupKey y) => CollectionComparers.AreEqual(x.Value, y.Value);

    public int GetHashCode(GroupKey obj)
    {
        if (obj.Value is null)
            return 0;

        return StringComparer.OrdinalIgnoreCase.GetHashCode(CollectionFormatting.FormatValue(obj.Value));
    }
}

internal sealed class BaseObjSortComparer(string? memberName, bool descending) : IComparer<BaseObj>
{
    public int Compare(BaseObj? left, BaseObj? right)
    {
        object? leftValue = left?.UntypedValue;
        object? rightValue = right?.UntypedValue;

        if (memberName is not null)
        {
            CollectionMemberAccess.TryGetMemberValue(leftValue, memberName, out leftValue);
            CollectionMemberAccess.TryGetMemberValue(rightValue, memberName, out rightValue);
        }

        var comparison = CollectionComparers.Compare(leftValue, rightValue);
        return descending ? -comparison : comparison;
    }
}

internal ref struct RentedBuffer<T>
{
    private T[] _buffer;

    internal RentedBuffer(int capacity)
    {
        _buffer = ArrayPool<T>.Shared.Rent(Math.Max(capacity, 4));
        Count = 0;
    }

    internal int Count { get; private set; }

    internal T[] Buffer => _buffer;

    internal void Add(T value)
    {
        if (Count == _buffer.Length)
            Grow();

        _buffer[Count++] = value;
    }

    internal void Dispose()
    {
        Array.Clear(_buffer, 0, Count);
        ArrayPool<T>.Shared.Return(_buffer);
        _buffer = [];
        Count = 0;
    }

    private void Grow()
    {
        var enlarged = ArrayPool<T>.Shared.Rent(_buffer.Length * 2);
        Array.Copy(_buffer, enlarged, Count);
        Array.Clear(_buffer, 0, Count);
        ArrayPool<T>.Shared.Return(_buffer);
        _buffer = enlarged;
    }
}

internal static class PooledStringBuilder
{
    private static readonly ConcurrentStack<StringBuilder> Pool = new();

    internal static PooledStringBuilderLease Rent(int capacity = 256)
    {
        if (!Pool.TryPop(out var builder))
            builder = new StringBuilder(capacity);
        else
            builder.EnsureCapacity(capacity);

        return new PooledStringBuilderLease(builder);
    }

    internal readonly ref struct PooledStringBuilderLease(StringBuilder builder)
    {
        internal StringBuilder Builder => builder;

        internal void Dispose()
        {
            builder.Clear();
            Pool.Push(builder);
        }
    }
}

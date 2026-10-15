using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using KingShell.Extensions;
using KingShell.Interfaces;
using KingShell.Models;

namespace KingShell.Extensions.Collections;

internal static class CollectionCommands
{
    internal static void RegisterAll(Dispatcher dispatcher)
    {
        dispatcher.Register(new WhereCommand());
        dispatcher.Register(new SelectCommand());
        dispatcher.Register(new TakeCommand());
        dispatcher.Register(new SortCommand());
        dispatcher.Register(new GroupByCommand());
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

    internal readonly record struct SortOptions(string? MemberName, bool Descending, int InputStartIndex);
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

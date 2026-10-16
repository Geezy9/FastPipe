using System.Collections;
using System.Globalization;

namespace FastPipe.Extensions;

internal interface IStructuredValue
{
    bool TryGetMemberValue(string memberName, out object? value);

    IEnumerable<KeyValuePair<string, object?>> EnumerateMembers();
}

internal static class StructuredDataAccess
{
    internal static bool TryGetMemberValue(object? source, string memberName, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        if (source is null)
        {
            value = null;
            return false;
        }

        if (source is IStructuredValue structuredValue)
            return structuredValue.TryGetMemberValue(memberName, out value);

        if (source is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var pair in readOnlyDictionary)
            {
                if (pair.Key.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        if (source is IDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary)
            {
                if (pair.Key.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    internal static IEnumerable<KeyValuePair<string, object?>> EnumerateMembers(object? value)
    {
        if (value is null)
            yield break;

        if (value is IStructuredValue structuredValue)
        {
            foreach (var member in structuredValue.EnumerateMembers())
                yield return member;

            yield break;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var pair in readOnlyDictionary)
                yield return pair;

            yield break;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary)
                yield return pair;
        }
    }

    internal static bool IsScalar(object? value) => value is null or string or char or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset or Guid or TimeSpan || value?.GetType().IsEnum == true;

    internal static bool IsComplex(object? value) => !IsScalar(value) && (HasMembers(value) || TryEnumerateSequence(value, out _));

    internal static bool HasMembers(object? value)
    {
        if (value is null)
            return false;

        using var enumerator = EnumerateMembers(value).GetEnumerator();
        return enumerator.MoveNext();
    }

    internal static bool TryEnumerateSequence(object? value, out IEnumerable<object?> sequence)
    {
        if (value is string or null)
        {
            sequence = [];
            return false;
        }

        if (value is IEnumerable<object?> typedSequence)
        {
            sequence = typedSequence;
            return true;
        }

        if (value is IEnumerable untypedSequence)
        {
            sequence = EnumerateUntypedSequence(untypedSequence);
            return true;
        }

        sequence = [];
        return false;
    }

    internal static string FormatScalar(object? value)
    {
        return value switch
        {
            null => "null",
            bool boolean => boolean ? "true" : "false",
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static IEnumerable<object?> EnumerateUntypedSequence(IEnumerable sequence)
    {
        foreach (var item in sequence)
            yield return item;
    }
}
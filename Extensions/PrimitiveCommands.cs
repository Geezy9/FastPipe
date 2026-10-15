using System.Globalization;
using System.Text;
using KingShell.Interfaces;
using KingShell.Models;
using Spectre.Console;

namespace KingShell.Extensions;

internal abstract class ArithmeticCommandBase : IShellCmd
{
    public abstract string Name { get; }
    public IReadOnlyList<string> Aliases => [];
    public abstract string Description { get; }
    public string Usage => $"{Name} <number> <number...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        var operands = arguments.Select(argument => CommandArguments.GetNumber(argument, "number")).ToArray();
        if (operands.All(operand => operand.IsIntegral))
        {
            var result = checked((long)operands[0].Value);
            foreach (var (value, _) in operands[1..])
                result = ApplyIntegral(result, checked((long)value));
            context.Write(result);
        }
        else
        {
            var result = operands[0].Value;
            foreach (var (value, _) in operands[1..])
                result = ApplyFloating(result, value);
            context.Write(result);
        }

        return ValueTask.CompletedTask;
    }

    protected abstract long ApplyIntegral(long left, long right);
    protected abstract double ApplyFloating(double left, double right);
}

internal sealed class AddCommand : ArithmeticCommandBase
{
    public override string Name => "add";
    public override string Description => "Adds numbers using checked arithmetic.";
    protected override long ApplyIntegral(long left, long right) => checked(left + right);
    protected override double ApplyFloating(double left, double right) => left + right;
}

internal sealed class SubtractCommand : ArithmeticCommandBase
{
    public override string Name => "sub";
    public override string Description => "Subtracts numbers using checked arithmetic.";
    protected override long ApplyIntegral(long left, long right) => checked(left - right);
    protected override double ApplyFloating(double left, double right) => left - right;
}

internal sealed class MultiplyCommand : ArithmeticCommandBase
{
    public override string Name => "mul";
    public override string Description => "Multiplies numbers using checked arithmetic.";
    protected override long ApplyIntegral(long left, long right) => checked(left * right);
    protected override double ApplyFloating(double left, double right) => left * right;
}

internal sealed class DivideCommand : ArithmeticCommandBase
{
    public override string Name => "div";
    public override string Description => "Divides numbers; integer division requires a non-zero divisor.";
    protected override long ApplyIntegral(long left, long right) =>
        right == 0 ? throw new ArgumentException("Division by zero.") : left / right;
    protected override double ApplyFloating(double left, double right) =>
        right == 0 ? throw new ArgumentException("Division by zero.") : left / right;
}

internal sealed class ModuloCommand : ArithmeticCommandBase
{
    public override string Name => "mod";
    public override string Description => "Computes the remainder; requires a non-zero divisor.";
    protected override long ApplyIntegral(long left, long right) =>
        right == 0 ? throw new ArgumentException("Division by zero.") : left % right;
    protected override double ApplyFloating(double left, double right) =>
        right == 0 ? throw new ArgumentException("Division by zero.") : left % right;
}

internal abstract class ComparisonCommandBase : IShellCmd
{
    public abstract string Name { get; }
    public IReadOnlyList<string> Aliases => [];
    public abstract string Description { get; }
    public string Usage => $"{Name} <value> <value>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 2)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(Evaluate(Compare(arguments[0], arguments[1])));
        return ValueTask.CompletedTask;
    }

    protected abstract bool Evaluate(int comparison);

    private static int Compare(BaseObj left, BaseObj right)
    {
        if (left.UntypedValue is string leftText && right.UntypedValue is string rightText)
            return string.CompareOrdinal(leftText, rightText);

        var (leftValue, _) = CommandArguments.GetNumber(left, "value");
        var (rightValue, _) = CommandArguments.GetNumber(right, "value");
        return leftValue.CompareTo(rightValue);
    }
}

internal sealed class EqualsCommand : ComparisonCommandBase
{
    public override string Name => "eq";
    public override string Description => "Tests whether two values are equal.";
    protected override bool Evaluate(int comparison) => comparison == 0;
}

internal sealed class NotEqualsCommand : ComparisonCommandBase
{
    public override string Name => "ne";
    public override string Description => "Tests whether two values differ.";
    protected override bool Evaluate(int comparison) => comparison != 0;
}

internal sealed class GreaterThanCommand : ComparisonCommandBase
{
    public override string Name => "gt";
    public override string Description => "Tests whether the first value is greater.";
    protected override bool Evaluate(int comparison) => comparison > 0;
}

internal sealed class LessThanCommand : ComparisonCommandBase
{
    public override string Name => "lt";
    public override string Description => "Tests whether the first value is smaller.";
    protected override bool Evaluate(int comparison) => comparison < 0;
}

internal sealed class GreaterOrEqualCommand : ComparisonCommandBase
{
    public override string Name => "ge";
    public override string Description => "Tests whether the first value is greater or equal.";
    protected override bool Evaluate(int comparison) => comparison >= 0;
}

internal sealed class LessOrEqualCommand : ComparisonCommandBase
{
    public override string Name => "le";
    public override string Description => "Tests whether the first value is smaller or equal.";
    protected override bool Evaluate(int comparison) => comparison <= 0;
}

internal sealed class NotCommand : IShellCmd
{
    public string Name => "not";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Negates a boolean value.";
    public string Usage => "not <bool>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(!CommandArguments.GetBool(arguments[0], "bool"));
        return ValueTask.CompletedTask;
    }
}

internal sealed class AndCommand : IShellCmd
{
    public string Name => "and";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Logical AND over boolean values.";
    public string Usage => "and <bool> <bool...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(arguments.All(argument => CommandArguments.GetBool(argument, "bool")));
        return ValueTask.CompletedTask;
    }
}

internal sealed class OrCommand : IShellCmd
{
    public string Name => "or";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Logical OR over boolean values.";
    public string Usage => "or <bool> <bool...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(arguments.Any(argument => CommandArguments.GetBool(argument, "bool")));
        return ValueTask.CompletedTask;
    }
}

internal sealed class ToIntCommand : IShellCmd
{
    public string Name => "to-int";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts a value to a 64-bit integer.";
    public string Usage => "to-int <value>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(arguments[0].UntypedValue switch
        {
            int value => (long)value,
            long value => value,
            double value => checked((long)value),
            bool value => value ? 1L : 0L,
            string text when long.TryParse(text, out var value) => value,
            _ => throw new ArgumentException("value cannot be converted to an integer."),
        });
        return ValueTask.CompletedTask;
    }
}

internal sealed class ToDoubleCommand : IShellCmd
{
    public string Name => "to-double";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts a value to a double.";
    public string Usage => "to-double <value>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(CommandArguments.GetNumber(arguments[0], "value").Value);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ToBoolCommand : IShellCmd
{
    public string Name => "to-bool";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts a value to a boolean.";
    public string Usage => "to-bool <value>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(arguments[0].UntypedValue switch
        {
            bool value => value,
            int value => value != 0,
            long value => value != 0,
            double value => value != 0,
            string text when bool.TryParse(text, out var value) => value,
            _ => throw new ArgumentException("value cannot be converted to a boolean."),
        });
        return ValueTask.CompletedTask;
    }
}

internal sealed class ToStringCommand : IShellCmd
{
    public string Name => "to-string";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts a value to its string representation.";
    public string Usage => "to-string <value>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(Convert.ToString(arguments[0].UntypedValue, CultureInfo.InvariantCulture) ?? string.Empty);
        return ValueTask.CompletedTask;
    }
}

internal sealed class PlainCommand : IShellCmd
{
    private static readonly HashSet<string> StyleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "bold", "dim", "italic", "underline", "slowblink", "blink", "reverse", "conceal", "strikethrough", "default",
        "black", "maroon", "green", "olive", "navy", "purple", "teal", "silver", "grey", "red", "lime", "yellow", "blue", "fuchsia", "aqua", "white", "orange"
    };

    public string Name => "plain";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts formatted text to plain text.";
    public string Usage => "plain <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = Convert.ToString(argument.UntypedValue, CultureInfo.InvariantCulture) ?? string.Empty;
            context.Write(ToPlainText(text));
        }

        return ValueTask.CompletedTask;
    }

    private static string ToPlainText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var withoutAnsi = StripAnsiEscapeSequences(value);
        try
        {
            return Markup.Remove(withoutAnsi);
        }
        catch (InvalidOperationException)
        {
            return RemoveMarkupTagsBestEffort(withoutAnsi);
        }
    }

    private static string StripAnsiEscapeSequences(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '\u001b' && index + 1 < value.Length && value[index + 1] == '[')
            {
                index += 2;
                while (index < value.Length)
                {
                    var terminator = value[index];
                    index++;
                    if (terminator is >= '@' and <= '~')
                        break;
                }

                continue;
            }

            builder.Append(value[index]);
            index++;
        }

        return builder.ToString();
    }

    private static string RemoveMarkupTagsBestEffort(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '[')
            {
                if (index + 1 < value.Length && value[index + 1] == '[')
                {
                    builder.Append('[');
                    index += 2;
                    continue;
                }

                var closeIndex = value.IndexOf(']', index + 1);
                if (closeIndex < 0)
                {
                    builder.Append('[');
                    index++;
                    continue;
                }

                var content = value.AsSpan(index + 1, closeIndex - index - 1);
                if (IsLikelyMarkupTag(content))
                {
                    index = closeIndex + 1;
                    continue;
                }

                builder.Append('[');
                builder.Append(content);
                builder.Append(']');
                index = closeIndex + 1;
                continue;
            }

            if (value[index] == ']' && index + 1 < value.Length && value[index + 1] == ']')
            {
                builder.Append(']');
                index += 2;
                continue;
            }

            builder.Append(value[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool IsLikelyMarkupTag(ReadOnlySpan<char> content)
    {
        var trimmed = content.Trim();
        if (trimmed.IsEmpty)
            return true;

        if (trimmed[0] == '/')
            return true;

        if (trimmed.StartsWith("link=".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;

        var index = 0;
        while (index < trimmed.Length)
        {
            while (index < trimmed.Length && trimmed[index] == ' ')
                index++;

            if (index >= trimmed.Length)
                break;

            var tokenStart = index;
            while (index < trimmed.Length && trimmed[index] != ' ')
                index++;

            var token = trimmed[tokenStart..index];
            if (token.Equals("on".AsSpan(), StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsStyleToken(token))
                return false;
        }

        return true;
    }

    private static bool IsStyleToken(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
            return false;

        if (token[0] == '#')
            return IsHexColorToken(token[1..]);

        if (token.StartsWith("rgb(".AsSpan(), StringComparison.OrdinalIgnoreCase) && token.EndsWith(")".AsSpan(), StringComparison.Ordinal))
            return true;

        return StyleTokens.Contains(token.ToString());
    }

    private static bool IsHexColorToken(ReadOnlySpan<char> token)
    {
        if (token.Length is not 3 and not 6 and not 8)
            return false;

        foreach (var character in token)
        {
            if (!char.IsAsciiHexDigit(character))
                return false;
        }

        return true;
    }
}

internal sealed class TypeCommand : IShellCmd
{
    public string Name => "type";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Writes the runtime type name of each value.";
    public string Usage => "type <value...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(argument.UntypedValue?.GetType().Name ?? "null");
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class LengthCommand : IShellCmd
{
    public string Name => "length";
    public IReadOnlyList<string> Aliases => ["len"];
    public string Description => "Writes the length of each string value.";
    public string Usage => "length <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write((long)CommandArguments.GetText(argument, "text").Length);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class UpperCommand : IShellCmd
{
    public string Name => "upper";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts string values to upper case.";
    public string Usage => "upper <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(CommandArguments.GetText(argument, "text").ToUpperInvariant());
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class LowerCommand : IShellCmd
{
    public string Name => "lower";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Converts string values to lower case.";
    public string Usage => "lower <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
            throw new ArgumentException($"Usage: {Usage}");

        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Write(CommandArguments.GetText(argument, "text").ToLowerInvariant());
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class ConcatCommand : IShellCmd
{
    public string Name => "concat";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Concatenates string values.";
    public string Usage => "concat <text> <text...>";

    public ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length < 2)
            throw new ArgumentException($"Usage: {Usage}");

        cancellationToken.ThrowIfCancellationRequested();
        context.Write(string.Concat(arguments.Select(argument => CommandArguments.GetText(argument, "text"))));
        return ValueTask.CompletedTask;
    }
}

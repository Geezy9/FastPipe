using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using KingShell.Extensions;
using KingShell.Interfaces;
using KingShell.Models;

namespace KingShell
{
    internal static class UserFacingConsole
    {
        internal static ConcurrentQueue<(BaseObj, long)> OutputQueue = new ConcurrentQueue<(BaseObj, long)>();
        internal static ConcurrentQueue<(BaseObj, long)> ReturnQueue = new ConcurrentQueue<(BaseObj, long)>();
        internal static ConcurrentDictionary<PipeNode<int>, BaseObj> Nodes = new ConcurrentDictionary<PipeNode<int>, BaseObj>();  
        private static readonly Dispatcher Dispatcher = new();
        private static ShellSession? _session;

        internal static void Register(IShellCmd command) => Dispatcher.Register(command);

        internal static void Initialize(ShellSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            if (_session is not null)
                throw new InvalidOperationException("The shell command library has already been initialized.");

            _session = session;
            BuiltInCommands.RegisterAll(Dispatcher);
        }

        internal static async ValueTask<CommandContext> PostJobAsync(
            string commandLine,
            CancellationToken cancellationToken = default)
        {
            var tokens = Tokenize(commandLine);
            if (tokens.Count == 0)
                throw new ArgumentException("A command is required.", nameof(commandLine));

            var arguments = new BaseObj[tokens.Count - 1];

            for (var index = 1; index < tokens.Count; index++)
            {
                arguments[index - 1] = ParseArgument(tokens[index]);
            }

            var context = new CommandContext(_session ?? throw new InvalidOperationException("The shell command library has not been initialized."));
            await Dispatcher.DispatchAsync(tokens[0], context, arguments, cancellationToken);
            return context;
        }

        internal static BaseObj[] ParseArgs(string arguments)
        {
            var tokens = Tokenize(arguments);
            var parsedArguments = new BaseObj[tokens.Count];

            for (var index = 0; index < tokens.Count; index++)
            {
                parsedArguments[index] = ParseArgument(tokens[index]);
            }

            return parsedArguments;
        }


        private static IReadOnlyList<string> Tokenize(string commandLine)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);

            var tokens = new List<string>();
            var token = new StringBuilder();
            char quote = '\0';
            var escaping = false;

            foreach (var character in commandLine)
            {
                if (escaping)
                {
                    token.Append(character);
                    escaping = false;
                    continue;
                }

                if (quote != '\0' && character == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (character is '\'' or '"')
                {
                    if (quote == '\0')
                    {
                        quote = character;
                    }
                    else if (quote == character)
                    {
                        quote = '\0';
                    }
                    else
                    {
                        token.Append(character);
                    }

                    continue;
                }

                if (quote == '\0' && char.IsWhiteSpace(character))
                {
                    if (token.Length > 0)
                    {
                        tokens.Add(token.ToString());
                        token.Clear();
                    }

                    continue;
                }

                token.Append(character);
            }

            if (escaping)
            {
                throw new FormatException("A quoted argument cannot end with an escape character.");
            }

            if (quote != '\0')
            {
                throw new FormatException("A quoted argument is missing its closing quote.");
            }

            if (token.Length > 0)
            {
                tokens.Add(token.ToString());
            }

            return tokens;
        }

        private static BaseObj ParseArgument(string value)
        {
            if (value.StartsWith("!"))
            {
                return Runtime.Rent(value.Substring(1));
            }

            if (bool.TryParse(value, out var boolean))
            {
                return Runtime.Rent(boolean);
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return Runtime.Rent(integer);
            }

            if (TryParseWithSuffix(value, 'L', NumberStyles.Integer, long.TryParse, out long integer64))
            {
                return Runtime.Rent(integer64);
            }

            if (TryParseWithSuffix(value, 'F', NumberStyles.Float, float.TryParse, out float single))
            {
                return Runtime.Rent(single);
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doublePrecision))
            {
                return Runtime.Rent(doublePrecision);
            }

            if (TryParseWithSuffix(value, 'M', NumberStyles.Float, decimal.TryParse, out decimal decimalValue))
            {
                return Runtime.Rent(decimalValue);
            }

            if (value.Length == 1)
            {
                return Runtime.Rent(value[0]);
            }

            return Runtime.Rent(value);
        }

        private delegate bool NumberParser<T>(
            string value,
            NumberStyles styles,
            IFormatProvider? provider,
            out T result);

        private static bool TryParseWithSuffix<T>(
            string value,
            char suffix,
            NumberStyles styles,
            NumberParser<T> parser,
            out T result)
        {
            if (value.Length > 1 && char.ToUpperInvariant(value[^1]) == suffix)
            {
                return parser(value[..^1], styles, CultureInfo.InvariantCulture, out result);
            }

            result = default!;
            return false;
        }






    }
}

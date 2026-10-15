using System.Threading;
using System.Threading.Tasks;
using KingShell.Extensions;
using KingShell.Models;

namespace KingShell.Interfaces;


internal interface IShellCmd
{
    string Name { get; }

    IReadOnlyList<string> Aliases { get; }

    string Description { get; }

    string Usage { get; }

    ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken);
}

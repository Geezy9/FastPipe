using System.Threading;
using System.Threading.Tasks;
using FastPipe.Extensions;
using FastPipe.Models;

namespace FastPipe.Interfaces;


public interface IShellCmd
{
    string Name { get; }

    IReadOnlyList<string> Aliases { get; }

    string Description { get; }

    string Usage { get; }

    ValueTask ExecuteAsync(CommandContext context, BaseObj[] arguments, CancellationToken cancellationToken);
}

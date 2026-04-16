using TaskMGR.Core.Models;
using TaskMGR.Core.Results;

namespace TaskMGR.Core.Interfaces;

public interface IProcessProvider
{
    Task<Result<IReadOnlyList<ProcessInfo>, string>> GetProcessesAsync(CancellationToken cancellationToken = default);
    Task<ProcessInfo?> GetProcessByIdAsync(int pid, CancellationToken cancellationToken = default);
    Task<Result<Unit, ProcessError>> KillProcessAsync(int pid, CancellationToken cancellationToken = default);
}

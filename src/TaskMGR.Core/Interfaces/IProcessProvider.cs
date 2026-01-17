using TaskMGR.Core.Models;

namespace TaskMGR.Core.Interfaces;

public interface IProcessProvider
{
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default);
    Task<ProcessInfo?> GetProcessByIdAsync(int pid, CancellationToken cancellationToken = default);
    Task<bool> KillProcessAsync(int pid, CancellationToken cancellationToken = default);
}

using TaskMGR.Core.Models;

namespace TaskMGR.Core.Interfaces;

public interface ISystemMetricsProvider
{
    Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default);
}

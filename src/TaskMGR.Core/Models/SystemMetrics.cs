namespace TaskMGR.Core.Models;

public record SystemMetrics
{
    public double CpuUsagePercent { get; init; }
    public long TotalMemoryBytes { get; init; }
    public long UsedMemoryBytes { get; init; }
    public long AvailableMemoryBytes { get; init; }
    public int ProcessCount { get; init; }
    public int ThreadCount { get; init; }
    public TimeSpan Uptime { get; init; }
}

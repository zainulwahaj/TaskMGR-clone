namespace TaskMGR.Core.Models;

public record ProcessInfo
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public double CpuPercent { get; init; }
    public long MemoryBytes { get; init; }
    public string Status { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
}

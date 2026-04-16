namespace TaskMGR.UI.ViewModels;

public abstract record StatusState
{
    public sealed record Idle : StatusState;

    public sealed record Loading : StatusState;

    public sealed record Error(string Message) : StatusState;

    public sealed record Refreshed(int ProcessCount, double CpuPercent) : StatusState;
}

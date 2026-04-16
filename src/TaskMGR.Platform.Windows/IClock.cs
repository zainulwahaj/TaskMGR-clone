namespace TaskMGR.Platform.Windows;

public interface IClock
{
    DateTime UtcNow { get; }
}

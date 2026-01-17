namespace TaskMGR.Core.Interfaces;

public interface IPlatformService : IProcessProvider, ISystemMetricsProvider
{
    string PlatformName { get; }
}

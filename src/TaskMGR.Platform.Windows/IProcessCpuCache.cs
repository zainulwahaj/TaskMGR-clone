using System.Collections.Generic;

namespace TaskMGR.Platform.Windows;

public interface IProcessCpuCache
{
    void Update(int pid, TimeSpan totalCpu);
    bool TryGetPercent(int pid, out double pct);
    void Cleanup(IReadOnlySet<int> activePids);
}

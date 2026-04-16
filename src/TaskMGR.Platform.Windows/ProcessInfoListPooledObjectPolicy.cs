using Microsoft.Extensions.ObjectPool;
using TaskMGR.Core.Models;

namespace TaskMGR.Platform.Windows;

internal sealed class ProcessInfoListPooledObjectPolicy : PooledObjectPolicy<List<ProcessInfo>>
{
    public override List<ProcessInfo> Create() => new();

    public override bool Return(List<ProcessInfo> obj)
    {
        obj.Clear();
        return true;
    }
}

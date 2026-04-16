using System;
using System.Collections.Generic;
using TaskMGR.Core.Models;

namespace TaskMGR.UI.Services;

public sealed record RefreshResult(
    IReadOnlyList<ProcessInfo> Processes,
    SystemMetrics SystemMetrics,
    string? ErrorMessage = null)
{
    public static RefreshResult FromError(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new RefreshResult(Array.Empty<ProcessInfo>(), new SystemMetrics(), errorMessage);
    }
}

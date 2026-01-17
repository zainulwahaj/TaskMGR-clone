using System.Diagnostics;
using System.Runtime.InteropServices;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;

namespace TaskMGR.Platform.Windows;

public class WindowsPlatformService : IPlatformService
{
    public string PlatformName => "Windows";

    private readonly Dictionary<int, (DateTime lastTime, TimeSpan lastTotalTime)> _cpuUsageCache = new();

    public Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var processes = new List<ProcessInfo>();
        var currentTime = DateTime.UtcNow;

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                double cpuPercent = 0;
                
                // Calculate CPU usage
                if (_cpuUsageCache.TryGetValue(proc.Id, out var cached))
                {
                    var timeDiff = (currentTime - cached.lastTime).TotalMilliseconds;
                    if (timeDiff > 0)
                    {
                        var cpuDiff = (proc.TotalProcessorTime - cached.lastTotalTime).TotalMilliseconds;
                        cpuPercent = (cpuDiff / timeDiff) * 100 / Environment.ProcessorCount;
                    }
                }
                
                _cpuUsageCache[proc.Id] = (currentTime, proc.TotalProcessorTime);

                processes.Add(new ProcessInfo
                {
                    Pid = proc.Id,
                    Name = proc.ProcessName,
                    CpuPercent = Math.Round(cpuPercent, 1),
                    MemoryBytes = proc.WorkingSet64,
                    Status = proc.Responding ? "Running" : "Not Responding",
                    User = GetProcessUser(proc),
                    StartTime = GetProcessStartTime(proc)
                });
            }
            catch (Exception)
            {
                // Process may have exited
            }
        }

        // Clean up stale cache entries
        var activeIds = processes.Select(p => p.Pid).ToHashSet();
        var staleIds = _cpuUsageCache.Keys.Where(id => !activeIds.Contains(id)).ToList();
        foreach (var id in staleIds)
            _cpuUsageCache.Remove(id);

        return Task.FromResult<IReadOnlyList<ProcessInfo>>(processes);
    }

    public async Task<ProcessInfo?> GetProcessByIdAsync(int pid, CancellationToken cancellationToken = default)
    {
        var processes = await GetProcessesAsync(cancellationToken);
        return processes.FirstOrDefault(p => p.Pid == pid);
    }

    public Task<bool> KillProcessAsync(int pid, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        var processes = await GetProcessesAsync(cancellationToken);
        
        // Get memory info using GC and Environment
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        
        long totalMemory = 0;
        long availableMemory = 0;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX { dwLength = 64 };
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    totalMemory = (long)memStatus.ullTotalPhys;
                    availableMemory = (long)memStatus.ullAvailPhys;
                }
            }
            catch { }
        }

        // Calculate total CPU from all processes
        double totalCpu = processes.Sum(p => p.CpuPercent);

        return new SystemMetrics
        {
            CpuUsagePercent = Math.Min(100, Math.Round(totalCpu, 1)),
            TotalMemoryBytes = totalMemory,
            UsedMemoryBytes = totalMemory - availableMemory,
            AvailableMemoryBytes = availableMemory,
            ProcessCount = processes.Count,
            ThreadCount = processes.Count * 10, // Approximation
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        };
    }

    private static string GetProcessUser(Process process)
    {
        try
        {
            return Environment.UserName;
        }
        catch
        {
            return "SYSTEM";
        }
    }

    private static DateTime GetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public int dwLength;
        public int dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

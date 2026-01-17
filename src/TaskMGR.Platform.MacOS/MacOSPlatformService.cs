using System.Diagnostics;
using System.Runtime.InteropServices;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;

namespace TaskMGR.Platform.MacOS;

public class MacOSPlatformService : IPlatformService
{
    public string PlatformName => "macOS";

    public async Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var processes = new List<ProcessInfo>();
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/ps",
                Arguments = "-eo pid,pcpu,rss,state,user,lstart,comm",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return processes;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 7) continue;

                if (int.TryParse(parts[0], out int pid) &&
                    double.TryParse(parts[1], out double cpu) &&
                    long.TryParse(parts[2], out long rss))
                {
                    processes.Add(new ProcessInfo
                    {
                        Pid = pid,
                        CpuPercent = cpu,
                        MemoryBytes = rss * 1024, // RSS is in KB
                        Status = parts[3],
                        User = parts[4],
                        Name = parts[^1],
                        StartTime = DateTime.Now // Simplified
                    });
                }
            }
        }
        catch (Exception)
        {
            // Fallback to .NET Process API
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    processes.Add(new ProcessInfo
                    {
                        Pid = proc.Id,
                        Name = proc.ProcessName,
                        MemoryBytes = proc.WorkingSet64,
                        CpuPercent = 0,
                        Status = "Running",
                        User = Environment.UserName
                    });
                }
                catch { }
            }
        }

        return processes;
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
        var metrics = new SystemMetrics();
        
        try
        {
            // Get CPU usage via top
            var cpuPsi = new ProcessStartInfo
            {
                FileName = "/usr/bin/top",
                Arguments = "-l 1 -n 0 -stats cpu",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            double cpuUsage = 0;
            using (var cpuProcess = Process.Start(cpuPsi))
            {
                if (cpuProcess != null)
                {
                    var output = await cpuProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                    await cpuProcess.WaitForExitAsync(cancellationToken);
                    
                    // Parse CPU usage from top output
                    var cpuLine = output.Split('\n').FirstOrDefault(l => l.Contains("CPU usage"));
                    if (cpuLine != null)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(cpuLine, @"(\d+\.?\d*)% user");
                        if (match.Success && double.TryParse(match.Groups[1].Value, out var user))
                        {
                            cpuUsage = user;
                            var sysMatch = System.Text.RegularExpressions.Regex.Match(cpuLine, @"(\d+\.?\d*)% sys");
                            if (sysMatch.Success && double.TryParse(sysMatch.Groups[1].Value, out var sys))
                            {
                                cpuUsage += sys;
                            }
                        }
                    }
                }
            }

            // Get memory info via vm_stat
            var vmPsi = new ProcessStartInfo
            {
                FileName = "/usr/bin/vm_stat",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            long totalMemory = 0;
            long usedMemory = 0;

            using (var vmProcess = Process.Start(vmPsi))
            {
                if (vmProcess != null)
                {
                    var output = await vmProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                    await vmProcess.WaitForExitAsync(cancellationToken);

                    var pageSize = 16384L; // Default page size for Apple Silicon
                    long freePages = 0, activePages = 0, inactivePages = 0, wiredPages = 0, compressedPages = 0;

                    foreach (var line in output.Split('\n'))
                    {
                        if (line.Contains("page size of"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)");
                            if (match.Success) pageSize = long.Parse(match.Value);
                        }
                        else if (line.StartsWith("Pages free:"))
                            freePages = ParseVmStatValue(line);
                        else if (line.StartsWith("Pages active:"))
                            activePages = ParseVmStatValue(line);
                        else if (line.StartsWith("Pages inactive:"))
                            inactivePages = ParseVmStatValue(line);
                        else if (line.StartsWith("Pages wired down:"))
                            wiredPages = ParseVmStatValue(line);
                        else if (line.StartsWith("Pages occupied by compressor:"))
                            compressedPages = ParseVmStatValue(line);
                    }

                    totalMemory = (freePages + activePages + inactivePages + wiredPages + compressedPages) * pageSize;
                    usedMemory = (activePages + wiredPages + compressedPages) * pageSize;
                }
            }

            // Get uptime
            var uptimePsi = new ProcessStartInfo
            {
                FileName = "/usr/bin/uptime",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var uptime = TimeSpan.Zero;
            using (var uptimeProcess = Process.Start(uptimePsi))
            {
                if (uptimeProcess != null)
                {
                    var output = await uptimeProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                    await uptimeProcess.WaitForExitAsync(cancellationToken);
                    
                    // Parse uptime (format varies)
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"up\s+(\d+)\s+days?,?\s*(\d+):(\d+)");
                    if (match.Success)
                    {
                        uptime = new TimeSpan(
                            int.Parse(match.Groups[1].Value),
                            int.Parse(match.Groups[2].Value),
                            int.Parse(match.Groups[3].Value),
                            0);
                    }
                    else
                    {
                        match = System.Text.RegularExpressions.Regex.Match(output, @"up\s+(\d+):(\d+)");
                        if (match.Success)
                        {
                            uptime = new TimeSpan(0, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 0);
                        }
                    }
                }
            }

            var processes = await GetProcessesAsync(cancellationToken);

            return new SystemMetrics
            {
                CpuUsagePercent = cpuUsage,
                TotalMemoryBytes = totalMemory,
                UsedMemoryBytes = usedMemory,
                AvailableMemoryBytes = totalMemory - usedMemory,
                ProcessCount = processes.Count,
                ThreadCount = 0,
                Uptime = uptime
            };
        }
        catch
        {
            return metrics;
        }
    }

    private static long ParseVmStatValue(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @":\s*(\d+)");
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }
}

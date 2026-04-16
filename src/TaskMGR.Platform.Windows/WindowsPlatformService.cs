using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.ObjectPool;
using TaskMGR.Core.Constants;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;
using TaskMGR.Core.Results;

namespace TaskMGR.Platform.Windows;

public sealed class WindowsPlatformService : IPlatformService
{
    private readonly IProcessCpuCache _cpuUsageCache;
    private readonly ObjectPool<List<ProcessInfo>> _processListPool;
    private int _lastProcessCount;
    private int _lastThreadCount;
    private double _lastCpuUsagePercent;

    public WindowsPlatformService()
        : this(new ProcessCpuCache())
    {
    }

    public WindowsPlatformService(IProcessCpuCache cpuUsageCache)
        : this(
            cpuUsageCache,
            new DefaultObjectPoolProvider().Create(new ProcessInfoListPooledObjectPolicy()))
    {
    }

    internal WindowsPlatformService(
        IProcessCpuCache cpuUsageCache,
        ObjectPool<List<ProcessInfo>> processListPool)
    {
        _cpuUsageCache = cpuUsageCache;
        _processListPool = processListPool;
    }

    public string PlatformName => PlatformNames.Windows;

    public Task<Result<IReadOnlyList<ProcessInfo>, string>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var processes = _processListPool.Get();

        try
        {
            var totalThreads = 0;
            var totalCpu = 0d;

            foreach (var proc in Process.GetProcesses())
            {
                using (proc)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        _cpuUsageCache.Update(proc.Id, proc.TotalProcessorTime);
                        _cpuUsageCache.TryGetPercent(proc.Id, out var cpuPercent);

                        processes.Add(
                            ProcessInfo.Create(
                                proc.Id,
                                proc.ProcessName,
                                Math.Round(cpuPercent, 1),
                                proc.WorkingSet64,
                                proc.Responding ? "Running" : "Not Responding",
                                GetProcessUser(),
                                GetProcessStartTime(proc)));

                        totalThreads += GetThreadCount(proc);
                        totalCpu += cpuPercent;
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                    {
                        // Process may have exited or deny access mid-snapshot.
                    }
                }
            }

            var activeIds = processes.Select(process => process.Pid).ToHashSet();
            _cpuUsageCache.Cleanup(activeIds);

            _lastProcessCount = processes.Count;
            _lastThreadCount = totalThreads;
            _lastCpuUsagePercent = Math.Min(100d, Math.Round(totalCpu, 1));

            return Task.FromResult(Result<IReadOnlyList<ProcessInfo>, string>.Ok(processes.ToArray()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IReadOnlyList<ProcessInfo>, string>.Fail($"Unable to enumerate processes: {ex.Message}"));
        }
        finally
        {
            _processListPool.Return(processes);
        }
    }

    public async Task<ProcessInfo?> GetProcessByIdAsync(int pid, CancellationToken cancellationToken = default)
    {
        var result = await GetProcessesAsync(cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error);
        }

        return result.Value.FirstOrDefault(p => p.Pid == pid);
    }

    public Task<Result<Unit, ProcessError>> KillProcessAsync(int pid, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var process = Process.GetProcessById(pid);
            process.Kill();
            return Task.FromResult(Result<Unit, ProcessError>.Ok(Unit.Value));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(Result<Unit, ProcessError>.Fail(ProcessError.NotFound));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(Result<Unit, ProcessError>.Fail(ProcessError.NotFound));
        }
        catch (Win32Exception)
        {
            return Task.FromResult(Result<Unit, ProcessError>.Fail(ProcessError.AccessDenied));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(Result<Unit, ProcessError>.Fail(ProcessError.AccessDenied));
        }
        catch
        {
            return Task.FromResult(Result<Unit, ProcessError>.Fail(ProcessError.Unknown));
        }
    }

    public Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long totalMemory = 0;
        long availableMemory = 0;

        if (OperatingSystem.IsWindows())
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = 64 };
            if (!GlobalMemoryStatusEx(ref memStatus))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read system memory information.");
            }

            totalMemory = (long)memStatus.ullTotalPhys;
            availableMemory = (long)memStatus.ullAvailPhys;
        }

        return Task.FromResult(
            new SystemMetrics
            {
                CpuUsagePercent = _lastCpuUsagePercent,
                TotalMemoryBytes = totalMemory,
                UsedMemoryBytes = totalMemory - availableMemory,
                AvailableMemoryBytes = availableMemory,
                ProcessCount = _lastProcessCount,
                ThreadCount = _lastThreadCount,
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            });
    }

    private static string GetProcessUser() => Environment.UserName;

    private static DateTime GetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return DateTime.MinValue;
        }
    }

    private static int GetThreadCount(Process process)
    {
        try
        {
            return process.Threads.Count;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return 0;
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

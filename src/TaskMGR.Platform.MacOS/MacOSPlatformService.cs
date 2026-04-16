using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.ObjectPool;
using TaskMGR.Core.Constants;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;
using TaskMGR.Core.Results;

namespace TaskMGR.Platform.MacOS;

public sealed class MacOSPlatformService : IPlatformService
{
    private const string LibProc = "/usr/lib/libproc.dylib";
    private const string LibSystem = "/usr/lib/libSystem.dylib";
    private const int ProcPidTaskInfo = 4;
    private const int ProcPidTbsdInfo = 3;
    private const int MaxCommandLength = 17;
    private const int MaxNameLength = 33;

    private readonly ObjectPool<List<ProcessInfo>> _processListPool;
    private int _lastProcessCount;
    private long _lastUsedMemoryBytes;

    public MacOSPlatformService()
        : this(new DefaultObjectPoolProvider().Create(new ProcessInfoListPooledObjectPolicy()))
    {
    }

    internal MacOSPlatformService(ObjectPool<List<ProcessInfo>> processListPool)
    {
        _processListPool = processListPool;
    }

    public string PlatformName => PlatformNames.MacOS;

    public async Task<Result<IReadOnlyList<ProcessInfo>, string>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var processes = _processListPool.Get();

        try
        {
            long usedMemoryBytes = 0;

            try
            {
                await foreach (var process in StreamProcessesAsync(cancellationToken).ConfigureAwait(false))
                {
                    processes.Add(process);
                    usedMemoryBytes += process.MemoryBytes;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                processes.Clear();
                usedMemoryBytes = PopulateFallbackProcesses(processes, cancellationToken);
            }

            _lastProcessCount = processes.Count;
            _lastUsedMemoryBytes = usedMemoryBytes;

            return Result<IReadOnlyList<ProcessInfo>, string>.Ok(processes.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ProcessInfo>, string>.Fail($"Unable to enumerate processes: {ex.Message}");
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

        return result.Value.FirstOrDefault(process => process.Pid == pid);
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

    public async Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_lastProcessCount == 0 && _lastUsedMemoryBytes == 0)
        {
            await RefreshSnapshotMetricsAsync(cancellationToken).ConfigureAwait(false);
        }

        var bootTime = ReadSysctlStruct<TimeValue>("kern.boottime");
        var loadAverage = ReadSysctlStruct<LoadAverage>("vm.loadavg");
        var totalMemory = checked((long)ReadSysctlUInt64("hw.memsize"));
        var usedMemory = Math.Min(_lastUsedMemoryBytes, totalMemory);

        return new SystemMetrics
        {
            CpuUsagePercent = GetCpuUsagePercent(loadAverage),
            TotalMemoryBytes = totalMemory,
            UsedMemoryBytes = usedMemory,
            AvailableMemoryBytes = Math.Max(0, totalMemory - usedMemory),
            ProcessCount = _lastProcessCount,
            ThreadCount = 0,
            Uptime = GetUptime(bootTime)
        };
    }

    private async IAsyncEnumerable<ProcessInfo> StreamProcessesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestedPidCount = Math.Max(proc_listallpids(IntPtr.Zero, 0), 256);
        var pidBuffer = ArrayPool<int>.Shared.Rent(requestedPidCount);
        var handle = GCHandle.Alloc(pidBuffer, GCHandleType.Pinned);

        try
        {
            var processCount = proc_listallpids(handle.AddrOfPinnedObject(), pidBuffer.Length * sizeof(int));
            if (processCount <= 0)
            {
                yield break;
            }

            for (var index = 0; index < processCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pid = pidBuffer[index];
                if (pid <= 0 || !TryCreateProcessInfo(pid, out var processInfo))
                {
                    continue;
                }

                yield return processInfo;

                if ((index + 1) % 64 == 0)
                {
                    await Task.Yield();
                }
            }
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }

            ArrayPool<int>.Shared.Return(pidBuffer, clearArray: false);
        }
    }

    private static bool TryCreateProcessInfo(int pid, [NotNullWhen(true)] out ProcessInfo? processInfo)
    {
        processInfo = null;

        if (proc_pidinfo_bsd(pid, ProcPidTbsdInfo, 0, out var bsdInfo, Marshal.SizeOf<ProcBsdInfo>()) <= 0)
        {
            return false;
        }

        ProcTaskInfo? taskInfo = null;
        if (proc_pidinfo_task(pid, ProcPidTaskInfo, 0, out var taskSnapshot, Marshal.SizeOf<ProcTaskInfo>()) > 0)
        {
            taskInfo = taskSnapshot;
        }

        var startTime = GetStartTime(bsdInfo);
        var residentMemory = taskInfo?.ResidentSize ?? 0;

        processInfo = ProcessInfo.Create(
            pid,
            GetProcessName(bsdInfo, pid),
            Math.Round(GetCpuPercent(taskInfo, startTime), 1),
            checked((long)residentMemory),
            GetProcessStatus(bsdInfo.Status),
            bsdInfo.UserId.ToString(),
            startTime.LocalDateTime);

        return true;
    }

    private static long PopulateFallbackProcesses(List<ProcessInfo> processes, CancellationToken cancellationToken)
    {
        long usedMemoryBytes = 0;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    processes.Add(
                        ProcessInfo.Create(
                            process.Id,
                            process.ProcessName,
                            0,
                            process.WorkingSet64,
                            "Running",
                            Environment.UserName,
                            GetFallbackStartTime(process)));

                    usedMemoryBytes += process.WorkingSet64;
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // Process may have exited or deny access mid-snapshot.
                }
            }
        }

        return usedMemoryBytes;
    }

    private async Task RefreshSnapshotMetricsAsync(CancellationToken cancellationToken)
    {
        var processCount = 0;
        long usedMemoryBytes = 0;

        await foreach (var process in StreamProcessesAsync(cancellationToken).ConfigureAwait(false))
        {
            processCount++;
            usedMemoryBytes += process.MemoryBytes;
        }

        _lastProcessCount = processCount;
        _lastUsedMemoryBytes = usedMemoryBytes;
    }

    private static double GetCpuPercent(ProcTaskInfo? taskInfo, DateTimeOffset startTime)
    {
        if (taskInfo is null)
        {
            return 0;
        }

        var totalCpuTicks = checked((long)((taskInfo.Value.TotalSystemTime + taskInfo.Value.TotalUserTime) / 100));
        var elapsed = DateTimeOffset.UtcNow - startTime;

        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        var totalCpu = TimeSpan.FromTicks(totalCpuTicks);
        return Math.Clamp(
            (totalCpu.TotalMilliseconds / elapsed.TotalMilliseconds) * 100d / Math.Max(1, Environment.ProcessorCount),
            0d,
            100d);
    }

    private static double GetCpuUsagePercent(LoadAverage loadAverage)
    {
        if (loadAverage.Scale <= 0 || loadAverage.Values is null || loadAverage.Values.Length == 0)
        {
            return 0;
        }

        var normalizedLoad = loadAverage.Values[0] / (double)loadAverage.Scale;
        return Math.Clamp(normalizedLoad * 100d / Math.Max(1, Environment.ProcessorCount), 0d, 100d);
    }

    private static DateTimeOffset GetStartTime(ProcBsdInfo bsdInfo)
    {
        var startedAt = DateTimeOffset.FromUnixTimeSeconds((long)bsdInfo.StartTimeSeconds);
        return startedAt.AddMilliseconds(bsdInfo.StartTimeMicroseconds / 1000d);
    }

    private static DateTime GetFallbackStartTime(Process process)
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

    private static string GetProcessName(ProcBsdInfo bsdInfo, int pid)
    {
        var name = GetNullTerminatedString(bsdInfo.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var command = GetNullTerminatedString(bsdInfo.Command);
        return string.IsNullOrWhiteSpace(command) ? pid.ToString() : command;
    }

    private static string GetProcessStatus(uint status) =>
        status switch
        {
            1 => "Idle",
            2 => "Running",
            3 => "Sleeping",
            4 => "Stopped",
            5 => "Zombie",
            _ => "Unknown"
        };

    private static string GetNullTerminatedString(byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return string.Empty;
        }

        var length = Array.IndexOf(value, (byte)0);
        if (length < 0)
        {
            length = value.Length;
        }

        return Encoding.UTF8.GetString(value, 0, length).Trim();
    }

    private static TimeSpan GetUptime(TimeValue bootTime)
    {
        var bootedAt = DateTimeOffset.FromUnixTimeSeconds(bootTime.Seconds)
            .AddMilliseconds(bootTime.Microseconds / 1000d);

        var uptime = DateTimeOffset.UtcNow - bootedAt;
        return uptime > TimeSpan.Zero ? uptime : TimeSpan.Zero;
    }

    private static ulong ReadSysctlUInt64(string name)
    {
        nuint length = sizeof(ulong);
        var buffer = Marshal.AllocHGlobal((int)length);

        try
        {
            ThrowIfSysctlFailed(name, buffer, ref length);
            return checked((ulong)Marshal.ReadInt64(buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static T ReadSysctlStruct<T>(string name)
        where T : struct
    {
        nuint length = (nuint)Marshal.SizeOf<T>();
        var buffer = Marshal.AllocHGlobal((int)length);

        try
        {
            ThrowIfSysctlFailed(name, buffer, ref length);
            return Marshal.PtrToStructure<T>(buffer)!;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ThrowIfSysctlFailed(string name, IntPtr buffer, ref nuint length)
    {
        if (sysctlbyname(name, buffer, ref length, IntPtr.Zero, 0) != 0)
        {
            throw new InvalidOperationException($"sysctlbyname failed for '{name}'.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeValue
    {
        public long Seconds;
        public int Microseconds;
        public int Padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LoadAverage
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] Values;

        public long Scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcBsdInfo
    {
        public uint Flags;
        public uint Status;
        public uint ExitStatus;
        public uint Pid;
        public uint ParentPid;
        public uint UserId;
        public uint GroupId;
        public uint RealUserId;
        public uint RealGroupId;
        public uint SavedUserId;
        public uint SavedGroupId;
        public uint Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxCommandLength)]
        public byte[] Command;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxNameLength)]
        public byte[] Name;

        public uint FileCount;
        public uint ProcessGroupId;
        public uint JobControlCount;
        public uint TerminalDevice;
        public uint TerminalProcessGroupId;
        public int NiceValue;
        public ulong StartTimeSeconds;
        public ulong StartTimeMicroseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcTaskInfo
    {
        public ulong VirtualSize;
        public ulong ResidentSize;
        public ulong TotalUserTime;
        public ulong TotalSystemTime;
        public ulong ThreadsUserTime;
        public ulong ThreadsSystemTime;
        public int Policy;
        public int Faults;
        public int PageIns;
        public int CopyOnWriteFaults;
        public int MessagesSent;
        public int MessagesReceived;
        public int MachSystemCalls;
        public int UnixSystemCalls;
        public int ContextSwitches;
        public int ThreadCount;
        public int RunningThreadCount;
        public int Priority;
    }

    [DllImport(LibProc, SetLastError = true)]
    private static extern int proc_listallpids(IntPtr buffer, int buffersize);

    [DllImport(LibProc, EntryPoint = "proc_pidinfo", SetLastError = true)]
    private static extern int proc_pidinfo_bsd(
        int pid,
        int flavor,
        ulong arg,
        out ProcBsdInfo buffer,
        int buffersize);

    [DllImport(LibProc, EntryPoint = "proc_pidinfo", SetLastError = true)]
    private static extern int proc_pidinfo_task(
        int pid,
        int flavor,
        ulong arg,
        out ProcTaskInfo buffer,
        int buffersize);

    [DllImport(LibSystem, SetLastError = true)]
    private static extern int sysctlbyname(
        string name,
        IntPtr oldp,
        ref nuint oldlenp,
        IntPtr newp,
        nuint newlen);
}

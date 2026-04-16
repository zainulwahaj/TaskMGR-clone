using System.Diagnostics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using TaskMGR.Core.Interfaces;
using TaskMGR.Platform.MacOS;
using TaskMGR.Platform.Windows;

namespace TaskMGR.Tests.Performance;

[MemoryDiagnoser]
public sealed class ProcessListBenchmarks
{
    private readonly IPlatformService _phase1Service = CreatePlatformService();

    [Benchmark(Baseline = true)]
    public int GetProcessesAsync_Baseline()
    {
        var processes = Process.GetProcesses();

        try
        {
            return processes.Length;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    [Benchmark]
    public async Task<int> GetProcessesAsync_Phase1()
    {
        var result = await _phase1Service.GetProcessesAsync();
        return result.IsSuccess ? result.Value.Count : 0;
    }

    private static IPlatformService CreatePlatformService() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsPlatformService(new ProcessCpuCache())
            : new MacOSPlatformService();
}

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskMGR.Core.Models;

public sealed class ProcessInfo : ObservableObject
{
    private int _pid;
    private string _name = string.Empty;
    private double _cpuPercent;
    private long _memoryBytes;
    private string _status = string.Empty;
    private string _user = string.Empty;
    private DateTime _startTime;
    private IReadOnlyList<double> _cpuHistorySamples = Array.Empty<double>();

    private ProcessInfo()
    {
    }

    public int Pid
    {
        get => _pid;
        private set => SetProperty(ref _pid, value);
    }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public double CpuPercent
    {
        get => _cpuPercent;
        private set => SetProperty(ref _cpuPercent, value);
    }

    public long MemoryBytes
    {
        get => _memoryBytes;
        private set => SetProperty(ref _memoryBytes, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string User
    {
        get => _user;
        private set => SetProperty(ref _user, value);
    }

    public DateTime StartTime
    {
        get => _startTime;
        private set => SetProperty(ref _startTime, value);
    }

    public IReadOnlyList<double> CpuHistorySamples
    {
        get => _cpuHistorySamples;
        private set => SetProperty(ref _cpuHistorySamples, value);
    }

    public static ProcessInfo Create(
        int pid,
        string name,
        double cpuPercent,
        long memoryBytes,
        string status,
        string user,
        DateTime startTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pid);
        ArgumentOutOfRangeException.ThrowIfNegative(memoryBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(user);

        return new ProcessInfo
        {
            Pid = pid,
            Name = name,
            CpuPercent = cpuPercent,
            MemoryBytes = memoryBytes,
            Status = status,
            User = user,
            StartTime = startTime
        };
    }

    public ProcessInfo Clone()
    {
        var clone = Create(Pid, Name, CpuPercent, MemoryBytes, Status, User, StartTime);
        clone.SetCpuHistorySamples(CpuHistorySamples);
        return clone;
    }

    public void UpdateFrom(ProcessInfo source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (ReferenceEquals(this, source))
        {
            return;
        }

        Pid = source.Pid;
        Name = source.Name;
        CpuPercent = source.CpuPercent;
        MemoryBytes = source.MemoryBytes;
        Status = source.Status;
        User = source.User;
        StartTime = source.StartTime;
        CpuHistorySamples = source.CpuHistorySamples;
    }

    public void SetCpuHistorySamples(IReadOnlyList<double> cpuHistorySamples)
    {
        ArgumentNullException.ThrowIfNull(cpuHistorySamples);
        CpuHistorySamples = cpuHistorySamples;
    }
}

# TaskMGR - Cross-Platform Task Manager

> A native, high-performance system monitor and process manager for Windows and macOS

**Version:** 1.0.0  
**Date:** January 2026  
**Platform:** Windows 10+, macOS 11+  
**Runtime:** .NET 8.0  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Requirements](#2-system-requirements)
3. [Architecture Overview](#3-architecture-overview)
4. [Project Structure](#4-project-structure)
5. [Technical Specifications](#5-technical-specifications)
6. [Core Components](#6-core-components)
7. [Platform Implementations](#7-platform-implementations)
8. [User Interface](#8-user-interface)
9. [Data Flow](#9-data-flow)
10. [Build & Deployment](#10-build--deployment)
11. [API Reference](#11-api-reference)
12. [Future Enhancements](#12-future-enhancements)

---

## 1. Executive Summary

TaskMGR is a cross-platform task manager application that provides real-time process monitoring and system resource visualization. Built with .NET 8 and Avalonia UI, it delivers native performance while maintaining a single codebase for both Windows and macOS platforms.

### Key Features

| Feature | Description |
|---------|-------------|
| Process Listing | View all running processes with PID, name, CPU%, memory, status, user |
| System Metrics | Real-time CPU usage, memory consumption, process count, uptime |
| Process Control | Terminate selected processes |
| Search & Filter | Instant process filtering by name |
| Auto-Refresh | Configurable refresh interval (default: 2 seconds) |
| Dark Theme | Modern dark UI optimized for extended use |

---

## 2. System Requirements

### Runtime Requirements

| Platform | Minimum Version | Architecture |
|----------|-----------------|--------------|
| Windows | Windows 10 (1809+) | x64, ARM64 |
| macOS | macOS 11 Big Sur | x64, ARM64 (Apple Silicon) |

### Development Requirements

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Build and runtime |
| IDE | VS Code / Visual Studio / Rider | Development |
| Git | 2.0+ | Version control |

---

## 3. Architecture Overview

### 3.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                      │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                    TaskMGR.UI                           ││
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ││
│  │  │    Views     │  │  ViewModels  │  │  Converters  │  ││
│  │  │  (XAML/AXAML)│  │   (MVVM)     │  │              │  ││
│  │  └──────────────┘  └──────────────┘  └──────────────┘  ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      ABSTRACTION LAYER                       │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                    TaskMGR.Core                         ││
│  │  ┌──────────────────────┐  ┌──────────────────────┐    ││
│  │  │      Interfaces      │  │       Models         │    ││
│  │  │  • IPlatformService  │  │  • ProcessInfo       │    ││
│  │  │  • IProcessProvider  │  │  • SystemMetrics     │    ││
│  │  │  • ISystemMetrics    │  │                      │    ││
│  │  └──────────────────────┘  └──────────────────────┘    ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      PLATFORM LAYER                          │
│  ┌──────────────────────┐    ┌──────────────────────┐      │
│  │ TaskMGR.Platform     │    │ TaskMGR.Platform     │      │
│  │      .Windows        │    │      .MacOS          │      │
│  │  ┌────────────────┐  │    │  ┌────────────────┐  │      │
│  │  │ Windows        │  │    │  │ MacOS          │  │      │
│  │  │ PlatformService│  │    │  │ PlatformService│  │      │
│  │  └────────────────┘  │    │  └────────────────┘  │      │
│  │         │            │    │         │            │      │
│  │         ▼            │    │         ▼            │      │
│  │  ┌────────────────┐  │    │  ┌────────────────┐  │      │
│  │  │   P/Invoke     │  │    │  │ Shell Commands │  │      │
│  │  │  kernel32.dll  │  │    │  │ ps/top/vm_stat │  │      │
│  │  └────────────────┘  │    │  └────────────────┘  │      │
│  └──────────────────────┘    └──────────────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    OPERATING SYSTEM                          │
│            Windows NT Kernel  /  Darwin (XNU)                │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Design Patterns

| Pattern | Implementation | Purpose |
|---------|---------------|---------|
| **MVVM** | ViewModels + Data Binding | Separation of UI and business logic |
| **Strategy** | `IPlatformService` implementations | Platform-specific behavior abstraction |
| **Factory** | `PlatformServiceFactory` | Runtime platform detection and instantiation |
| **Observer** | `INotifyPropertyChanged` | Reactive UI updates |

### 3.3 Dependency Graph

```
TaskMGR.UI
    ├── TaskMGR.Core
    ├── TaskMGR.Platform.Windows
    │       └── TaskMGR.Core
    └── TaskMGR.Platform.MacOS
            └── TaskMGR.Core
```

---

## 4. Project Structure

```
TaskMGR/
├── TaskMGR.sln                          # Solution file
├── README.md                            # This document
├── PACKAGING.md                         # Distribution guide
│
└── src/
    ├── TaskMGR.Core/                    # Shared library
    │   ├── TaskMGR.Core.csproj
    │   ├── Interfaces/
    │   │   ├── IPlatformService.cs      # Combined interface
    │   │   ├── IProcessProvider.cs      # Process operations
    │   │   └── ISystemMetricsProvider.cs# System metrics
    │   └── Models/
    │       ├── ProcessInfo.cs           # Process data record
    │       └── SystemMetrics.cs         # System metrics record
    │
    ├── TaskMGR.Platform.Windows/        # Windows implementation
    │   ├── TaskMGR.Platform.Windows.csproj
    │   └── WindowsPlatformService.cs    # Windows-specific logic
    │
    ├── TaskMGR.Platform.MacOS/          # macOS implementation
    │   ├── TaskMGR.Platform.MacOS.csproj
    │   └── MacOSPlatformService.cs      # macOS-specific logic
    │
    └── TaskMGR.UI/                      # Avalonia application
        ├── TaskMGR.UI.csproj
        ├── Program.cs                   # Entry point
        ├── App.axaml                    # Application resources
        ├── App.axaml.cs
        ├── MainWindow.axaml             # Main UI layout
        ├── MainWindow.axaml.cs
        ├── app.manifest                 # Windows manifest
        ├── Converters/
        │   └── BytesToStringConverter.cs# Memory formatting
        ├── Services/
        │   └── PlatformServiceFactory.cs# Platform detection
        └── ViewModels/
            └── MainWindowViewModel.cs   # Main view logic
```

---

## 5. Technical Specifications

### 5.1 Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Runtime | .NET | 8.0 LTS |
| UI Framework | Avalonia UI | 11.3.11 |
| UI Theme | Fluent Design | Built-in |
| MVVM Toolkit | CommunityToolkit.Mvvm | 8.2.2 |
| Data Grid | Avalonia.Controls.DataGrid | 11.3.11 |

### 5.2 NuGet Dependencies

```xml
<!-- TaskMGR.UI Dependencies -->
<PackageReference Include="Avalonia" Version="11.3.11" />
<PackageReference Include="Avalonia.Desktop" Version="11.3.11" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.11" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="11.3.11" />
<PackageReference Include="Avalonia.Controls.DataGrid" Version="11.3.11" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
<PackageReference Include="Avalonia.Diagnostics" Version="11.3.11" />
```

### 5.3 Target Frameworks

| Project | Framework | Output |
|---------|-----------|--------|
| TaskMGR.Core | net8.0 | Class Library |
| TaskMGR.Platform.Windows | net8.0 | Class Library |
| TaskMGR.Platform.MacOS | net8.0 | Class Library |
| TaskMGR.UI | net8.0 | WinExe |

---

## 6. Core Components

### 6.1 Models

#### ProcessInfo
```csharp
public record ProcessInfo
{
    public int Pid { get; init; }           // Process identifier
    public string Name { get; init; }        // Process name
    public double CpuPercent { get; init; }  // CPU usage percentage
    public long MemoryBytes { get; init; }   // Working set memory
    public string Status { get; init; }      // Running/Not Responding
    public string User { get; init; }        // Owner username
    public DateTime StartTime { get; init; } // Process start time
}
```

#### SystemMetrics
```csharp
public record SystemMetrics
{
    public double CpuUsagePercent { get; init; }   // Total CPU usage
    public long TotalMemoryBytes { get; init; }    // Total physical RAM
    public long UsedMemoryBytes { get; init; }     // Used memory
    public long AvailableMemoryBytes { get; init; }// Free memory
    public int ProcessCount { get; init; }         // Running processes
    public int ThreadCount { get; init; }          // Total threads
    public TimeSpan Uptime { get; init; }          // System uptime
}
```

### 6.2 Interfaces

#### IProcessProvider
```csharp
public interface IProcessProvider
{
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken ct);
    Task<ProcessInfo?> GetProcessByIdAsync(int pid, CancellationToken ct);
    Task<bool> KillProcessAsync(int pid, CancellationToken ct);
}
```

#### ISystemMetricsProvider
```csharp
public interface ISystemMetricsProvider
{
    Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken ct);
}
```

#### IPlatformService
```csharp
public interface IPlatformService : IProcessProvider, ISystemMetricsProvider
{
    string PlatformName { get; }
}
```

---

## 7. Platform Implementations

### 7.1 macOS Implementation

| Feature | Method | Command/API |
|---------|--------|-------------|
| Process List | `GetProcessesAsync` | `/bin/ps -eo pid,pcpu,rss,state,user,lstart,comm` |
| CPU Usage | `GetSystemMetricsAsync` | `/usr/bin/top -l 1 -n 0 -stats cpu` |
| Memory Info | `GetSystemMetricsAsync` | `/usr/bin/vm_stat` |
| System Uptime | `GetSystemMetricsAsync` | `/usr/bin/uptime` |
| Kill Process | `KillProcessAsync` | `Process.Kill()` |

**Memory Calculation (vm_stat):**
```
Used Memory = (Active + Wired + Compressed) × Page Size
Total Memory = (Free + Active + Inactive + Wired + Compressed) × Page Size
```

### 7.2 Windows Implementation

| Feature | Method | API |
|---------|--------|-----|
| Process List | `GetProcessesAsync` | `System.Diagnostics.Process.GetProcesses()` |
| CPU Usage | `GetProcessesAsync` | `Process.TotalProcessorTime` delta calculation |
| Memory Info | `GetSystemMetricsAsync` | P/Invoke `GlobalMemoryStatusEx` |
| System Uptime | `GetSystemMetricsAsync` | `Environment.TickCount64` |
| Kill Process | `KillProcessAsync` | `Process.Kill()` |

**P/Invoke Definition:**
```csharp
[DllImport("kernel32.dll", SetLastError = true)]
private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

private struct MEMORYSTATUSEX
{
    public int dwLength;
    public int dwMemoryLoad;
    public ulong ullTotalPhys;
    public ulong ullAvailPhys;
    // ... additional fields
}
```

**CPU Calculation:**
```
CPU% = (CurrentTotalTime - PreviousTotalTime) / (CurrentTime - PreviousTime) × 100 / ProcessorCount
```

---

## 8. User Interface

### 8.1 Layout Structure

```
┌─────────────────────────────────────────────────────────────┐
│                     HEADER (System Metrics)                  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐       │
│  │   CPU    │ │  Memory  │ │ Processes│ │  Uptime  │       │
│  │  12.5%   │ │ 8.2/16GB │ │   342    │ │ 5d 3h 2m │       │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘       │
├─────────────────────────────────────────────────────────────┤
│                     TOOLBAR                                  │
│  [Search...          ] Platform: macOS  [Refresh] [End Task]│
├─────────────────────────────────────────────────────────────┤
│                     PROCESS DATA GRID                        │
│  ┌─────┬────────────┬───────┬────────┬────────┬──────────┐ │
│  │ PID │ Name       │ CPU % │ Memory │ Status │ User     │ │
│  ├─────┼────────────┼───────┼────────┼────────┼──────────┤ │
│  │ 123 │ Safari     │  5.2  │ 1.2 GB │Running │ macuser  │ │
│  │ 456 │ Terminal   │  0.1  │ 45 MB  │Running │ macuser  │ │
│  │ ... │ ...        │  ...  │  ...   │  ...   │  ...     │ │
│  └─────┴────────────┴───────┴────────┴────────┴──────────┘ │
├─────────────────────────────────────────────────────────────┤
│                     STATUS BAR                               │
│  342 processes | CPU: 12.5%                                  │
└─────────────────────────────────────────────────────────────┘
```

### 8.2 Color Scheme

| Element | Color | Hex Code |
|---------|-------|----------|
| Background (Primary) | Dark Navy | `#0f0f23` |
| Background (Secondary) | Navy | `#1a1a2e` |
| Background (Tertiary) | Dark Blue | `#16213e` |
| CPU Accent | Cyan | `#4cc9f0` |
| Memory Accent | Pink | `#f72585` |
| Process Count | Purple | `#7209b7` |
| Uptime | Indigo | `#3a0ca3` |
| Refresh Button | Blue | `#4361ee` |
| End Task Button | Red | `#e63946` |
| Text (Muted) | Gray | `#888888` |

### 8.3 Data Binding

| UI Element | Binding Path | Mode |
|------------|--------------|------|
| Process Grid | `Processes` | OneWay |
| Selected Item | `SelectedProcess` | TwoWay |
| Search Box | `SearchText` | TwoWay |
| CPU Display | `SystemMetrics.CpuUsagePercent` | OneWay |
| Memory Display | `MemoryUsageText` | OneWay |
| Status Bar | `StatusMessage` | OneWay |

---

## 9. Data Flow

### 9.1 Refresh Cycle

```
┌──────────────────────────────────────────────────────────────┐
│                      AUTO-REFRESH LOOP                        │
│                                                               │
│  ┌─────────┐    ┌─────────────┐    ┌──────────────────────┐  │
│  │  Timer  │───▶│ RefreshAsync│───▶│ GetProcessesAsync    │  │
│  │ (2 sec) │    │   Command   │    │ GetSystemMetricsAsync│  │
│  └─────────┘    └─────────────┘    └──────────────────────┘  │
│                        │                      │               │
│                        │                      ▼               │
│                        │           ┌──────────────────────┐  │
│                        │           │  Platform Service    │  │
│                        │           │  (Windows/macOS)     │  │
│                        │           └──────────────────────┘  │
│                        │                      │               │
│                        │                      ▼               │
│                        │           ┌──────────────────────┐  │
│                        │           │     OS APIs          │  │
│                        │           └──────────────────────┘  │
│                        │                      │               │
│                        ▼                      ▼               │
│              ┌─────────────────────────────────────────────┐ │
│              │            UPDATE UI                         │ │
│              │  • Processes ObservableCollection            │ │
│              │  • SystemMetrics property                    │ │
│              │  • StatusMessage                             │ │
│              └─────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### 9.2 Kill Process Flow

```
User Click ──▶ KillProcessCommand ──▶ KillProcessAsync(pid)
                                              │
                                              ▼
                                    ┌──────────────────┐
                                    │ Process.Kill()   │
                                    └──────────────────┘
                                              │
                              ┌───────────────┴───────────────┐
                              ▼                               ▼
                        [Success]                        [Failure]
                              │                               │
                              ▼                               ▼
                    RefreshAsync()              StatusMessage = "Failed..."
```

---

## 10. Build & Deployment

### 10.1 Build Commands

```bash
# Restore dependencies
dotnet restore

# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run application
dotnet run --project src/TaskMGR.UI
```

### 10.2 Publish Commands

| Platform | Command |
|----------|---------|
| macOS ARM64 | `dotnet publish src/TaskMGR.UI -c Release -r osx-arm64 --self-contained` |
| macOS x64 | `dotnet publish src/TaskMGR.UI -c Release -r osx-x64 --self-contained` |
| Windows x64 | `dotnet publish src/TaskMGR.UI -c Release -r win-x64 --self-contained` |
| Windows ARM64 | `dotnet publish src/TaskMGR.UI -c Release -r win-arm64 --self-contained` |

### 10.3 Output Locations

```
src/TaskMGR.UI/bin/
├── Debug/net8.0/                    # Debug build
└── Release/net8.0/
    ├── osx-arm64/publish/           # macOS ARM64 publish
    ├── osx-x64/publish/             # macOS Intel publish
    ├── win-x64/publish/             # Windows x64 publish
    └── win-arm64/publish/           # Windows ARM64 publish
```

---

## 11. API Reference

### 11.1 MainWindowViewModel

| Property | Type | Description |
|----------|------|-------------|
| `Processes` | `ObservableCollection<ProcessInfo>` | Displayed process list |
| `SelectedProcess` | `ProcessInfo?` | Currently selected process |
| `SystemMetrics` | `SystemMetrics` | Current system metrics |
| `SearchText` | `string` | Filter text |
| `IsLoading` | `bool` | Refresh in progress |
| `StatusMessage` | `string` | Status bar text |
| `PlatformName` | `string` | Current OS name |
| `MemoryUsageText` | `string` | Formatted memory string |
| `MemoryUsagePercent` | `double` | Memory usage 0-100 |
| `UptimeText` | `string` | Formatted uptime |

| Command | Description |
|---------|-------------|
| `RefreshCommand` | Manually refresh data |
| `KillProcessCommand` | Terminate selected process |

### 11.2 PlatformServiceFactory

```csharp
public static class PlatformServiceFactory
{
    // Returns WindowsPlatformService or MacOSPlatformService
    // based on RuntimeInformation.IsOSPlatform()
    public static IPlatformService Create();
}
```

---

## 12. Future Enhancements

| Priority | Feature | Description |
|----------|---------|-------------|
| High | Process Details | Detailed view with threads, handles, modules |
| High | Performance Graphs | CPU/Memory history charts |
| Medium | Process Tree | Hierarchical parent-child view |
| Medium | Network Monitor | Per-process network usage |
| Medium | Disk I/O | Read/write statistics |
| Low | Linux Support | Add TaskMGR.Platform.Linux |
| Low | Localization | Multi-language support |
| Low | Themes | Light theme option |

---

## License

MIT License - See LICENSE file for details.

---

*Generated: January 2026*

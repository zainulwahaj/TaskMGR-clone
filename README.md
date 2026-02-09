# TaskMGR

<div align="center">

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8B44AC?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

**A modern, cross-platform task manager built with .NET 8 and Avalonia UI**

[Features](#features) • [Installation](#installation) • [Building](#building-from-source) • [Architecture](#architecture) • [Contributing](#contributing)

</div>

---

## Overview

TaskMGR is a native, high-performance system monitor and process manager that runs on both Windows and macOS from a single codebase. It provides real-time process monitoring, system resource visualization, and process management capabilities with a modern dark-themed interface.

<div align="center">

<!-- Add screenshot here -->
<!-- ![TaskMGR Screenshot](docs/screenshot.png) -->

</div>

## Features

- **📊 Real-Time Process Monitoring** — View all running processes with PID, name, CPU%, memory usage, status, and user
- **💻 System Metrics Dashboard** — Live CPU usage, memory consumption, process count, and system uptime
- **⚡ Process Control** — Terminate selected processes with a single click
- **🔍 Instant Search** — Filter processes by name in real-time
- **🔄 Auto-Refresh** — Automatic updates every 2 seconds (configurable)
- **🌙 Dark Theme** — Modern dark UI optimized for extended use
- **🖥️ Cross-Platform** — Native performance on Windows 10+ and macOS 11+

## Installation

### Pre-built Binaries

Download the latest release for your platform from the [Releases](https://github.com/yourusername/TaskMGR/releases) page.

| Platform | Architecture | Download |
|----------|--------------|----------|
| Windows | x64 | `TaskMGR-win-x64.zip` |
| Windows | ARM64 | `TaskMGR-win-arm64.zip` |
| macOS | Intel | `TaskMGR-osx-x64.dmg` |
| macOS | Apple Silicon | `TaskMGR-osx-arm64.dmg` |

### System Requirements

| Platform | Minimum Version |
|----------|-----------------|
| Windows | Windows 10 (1809+) |
| macOS | macOS 11 Big Sur |

> **Note:** Pre-built releases are self-contained — no .NET runtime installation required.

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Git

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/yourusername/TaskMGR.git
cd TaskMGR

# Restore dependencies
dotnet restore

# Build in Debug mode
dotnet build

# Run the application
dotnet run --project src/TaskMGR.UI
```

### Publishing for Distribution

#### macOS (Apple Silicon)
```bash
dotnet publish src/TaskMGR.UI -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

#### macOS (Intel)
```bash
dotnet publish src/TaskMGR.UI -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

#### Windows (x64)
```bash
dotnet publish src/TaskMGR.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

#### Windows (ARM64)
```bash
dotnet publish src/TaskMGR.UI -c Release -r win-arm64 --self-contained -p:PublishSingleFile=true
```

Output will be in `src/TaskMGR.UI/bin/Release/net8.0/{runtime}/publish/`

> 📦 See [PACKAGING.md](PACKAGING.md) for detailed instructions on creating `.app` bundles and `.dmg` installers for macOS.

## Project Structure

```
TaskMGR/
├── src/
│   ├── TaskMGR.Core/                 # Shared abstractions and models
│   │   ├── Interfaces/
│   │   │   ├── IPlatformService.cs   # Platform abstraction interface
│   │   │   ├── IProcessProvider.cs   # Process operations contract
│   │   │   └── ISystemMetricsProvider.cs
│   │   └── Models/
│   │       ├── ProcessInfo.cs        # Process data model
│   │       └── SystemMetrics.cs      # System metrics model
│   │
│   ├── TaskMGR.Platform.Windows/     # Windows-specific implementation
│   │   └── WindowsPlatformService.cs # P/Invoke with kernel32.dll
│   │
│   ├── TaskMGR.Platform.MacOS/       # macOS-specific implementation
│   │   └── MacOSPlatformService.cs   # Shell commands (ps, vm_stat)
│   │
│   └── TaskMGR.UI/                   # Avalonia UI application
│       ├── ViewModels/
│       │   └── MainWindowViewModel.cs
│       ├── Views/
│       ├── Converters/
│       ├── Services/
│       │   └── PlatformServiceFactory.cs
│       ├── MainWindow.axaml          # Main UI layout
│       └── Program.cs                # Entry point
│
├── TaskMGR.sln                       # Solution file
├── README.md
└── PACKAGING.md                      # Distribution guide
```

## Architecture

TaskMGR follows a clean, layered architecture with platform-specific implementations abstracted behind shared interfaces.

```
┌───────────────────────────────────────────────────────┐
│                   Presentation Layer                   │
│              TaskMGR.UI (Avalonia + MVVM)             │
└───────────────────────────────────────────────────────┘
                           │
                           ▼
┌───────────────────────────────────────────────────────┐
│                   Abstraction Layer                    │
│         TaskMGR.Core (Interfaces + Models)            │
└───────────────────────────────────────────────────────┘
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
┌─────────────────────────┐  ┌─────────────────────────┐
│   TaskMGR.Platform      │  │   TaskMGR.Platform      │
│        .Windows         │  │        .MacOS           │
│    (P/Invoke APIs)      │  │   (Shell Commands)      │
└─────────────────────────┘  └─────────────────────────┘
```

### Design Patterns

| Pattern | Usage |
|---------|-------|
| **MVVM** | Separation of UI and business logic via ViewModels and data binding |
| **Strategy** | `IPlatformService` implementations for OS-specific behavior |
| **Factory** | `PlatformServiceFactory` for runtime platform detection |
| **Observer** | `INotifyPropertyChanged` for reactive UI updates |

### Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 8.0 LTS |
| UI Framework | Avalonia UI 11.3 |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.2 |
| Theme | Fluent Design (Dark) |

## How It Works

### Platform Abstraction

The core challenge of cross-platform development is handling OS-specific APIs. TaskMGR solves this with a Strategy pattern:

**Windows:** Uses P/Invoke with `kernel32.dll` and the .NET `Process` class for accurate CPU percentage calculations with temporal caching.

**macOS:** Parses native shell commands (`ps`, `top`, `vm_stat`) since Darwin doesn't expose the same APIs as Windows.

```csharp
// The UI layer consumes IPlatformService without knowing the underlying OS
public interface IPlatformService : IProcessProvider, ISystemMetricsProvider
{
    string PlatformName { get; }
}
```

At runtime, `PlatformServiceFactory` detects the OS and instantiates the correct implementation:

```csharp
public static IPlatformService Create()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return new WindowsPlatformService();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return new MacOSPlatformService();
    throw new PlatformNotSupportedException();
}
```

## Usage

1. **Launch the application** — The main window displays system metrics and a process list
2. **Search processes** — Type in the search box to filter by process name
3. **View details** — Click any process to select it
4. **End a process** — Select a process and click "End Task" to terminate it
5. **Refresh** — Data auto-refreshes every 2 seconds, or click "Refresh" manually

## Contributing

Contributions are welcome! Here's how to get started:

1. **Fork** the repository
2. **Create a branch** for your feature (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open a Pull Request**

### Development Setup

```bash
# Clone your fork
git clone https://github.com/yourusername/TaskMGR.git

# Install dependencies
dotnet restore

# Run in development mode (with hot reload)
dotnet watch --project src/TaskMGR.UI
```


## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---



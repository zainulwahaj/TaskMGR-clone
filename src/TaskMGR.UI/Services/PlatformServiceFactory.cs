using System;
using System.Runtime.InteropServices;
using TaskMGR.Platform.MacOS;
using TaskMGR.Platform.Windows;

namespace TaskMGR.UI.Services;

/// <summary>
/// Creates the platform service implementation for the current operating system.
/// </summary>
public static class PlatformServiceFactory
{
    /// <summary>
    /// Creates the platform service that matches the current runtime OS.
    /// </summary>
    public static TaskMGR.Core.Interfaces.IPlatformService Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CreateForPlatform(OSPlatform.Windows);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return CreateForPlatform(OSPlatform.OSX);
        }

        throw new PlatformNotSupportedException("This application supports Windows and macOS only.");
    }

    /// <summary>
    /// Creates the platform service for a specific operating system.
    /// </summary>
    /// <param name="platform">The platform to instantiate.</param>
    public static TaskMGR.Core.Interfaces.IPlatformService CreateForPlatform(OSPlatform platform) =>
        platform == OSPlatform.Windows
            ? new WindowsPlatformService()
            : platform == OSPlatform.OSX
                ? new MacOSPlatformService()
                : throw new PlatformNotSupportedException("This application supports Windows and macOS only.");
}

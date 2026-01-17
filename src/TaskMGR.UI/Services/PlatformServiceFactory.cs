using System;
using System.Runtime.InteropServices;
using TaskMGR.Core.Interfaces;
using TaskMGR.Platform.MacOS;
using TaskMGR.Platform.Windows;

namespace TaskMGR.UI.Services;

public static class PlatformServiceFactory
{
    public static IPlatformService Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsPlatformService();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSPlatformService();
        
        throw new PlatformNotSupportedException("This application supports Windows and macOS only.");
    }
}

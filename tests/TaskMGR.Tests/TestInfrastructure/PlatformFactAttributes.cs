using System.Runtime.InteropServices;
using Xunit;

namespace TaskMGR.Tests.TestInfrastructure;

public sealed class MacOSFactAttribute : FactAttribute
{
    public MacOSFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Skip = "Requires macOS runtime.";
        }
    }
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "Requires Windows runtime.";
        }
    }
}

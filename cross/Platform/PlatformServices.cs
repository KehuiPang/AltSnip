using System.Runtime.InteropServices;

namespace AltSnip.Platform;

/// <summary>按当前操作系统挑选实现。</summary>
public static class PlatformServices
{
    public static IPlatformServices Current { get; } = Create();

    private static IPlatformServices Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsServices();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return new MacServices();
        return new LinuxServices();
    }
}

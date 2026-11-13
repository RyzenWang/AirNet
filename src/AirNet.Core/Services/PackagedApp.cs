using System.Runtime.InteropServices;

namespace AirNet.Core.Services;

/// <summary>
/// Detects whether the process runs from an MSIX package.
/// </summary>
/// <remarks>
/// Two behaviours depend on this: a packaged process cannot raise a UAC prompt, so the DNS
/// switch is downgraded to "copy the command", and its application-data writes are redirected
/// into the package's private cache. Both are silent failures otherwise.
/// </remarks>
public static class PackagedApp
{
    private const int ErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var length = 0;
            // Passing a null buffer asks only for the length; the return code tells us whether
            // a package identity exists at all.
            var result = GetCurrentPackageFullName(ref length, null);
            return result != ErrorNoPackage && result != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}

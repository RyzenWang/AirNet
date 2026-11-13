using System.Runtime.InteropServices;

namespace AirNet.Core.Services;

/// <summary>
/// Detects whether the process runs from an MSIX package, and what version that package is.
/// </summary>
/// <remarks>
/// Two behaviours depend on identity: a packaged process cannot raise a UAC prompt, so the DNS
/// switch is downgraded to "copy the command"; and the Store channel versions packages on its own
/// line, so the About card has to show both numbers or a bug report cannot be traced.
/// </remarks>
public static class PackagedApp
{
    private const int ErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    public static bool IsPackaged { get; } = PackageFullName is not null;

    /// <summary>The package version (for example "1.0.0.0"), or null when running unpackaged.</summary>
    public static string? PackageVersion { get; } = ParseVersion(PackageFullName);

    /// <summary>The full package identity string, which doubles as the identity check.</summary>
    public static string? PackageFullName { get; } = ReadFullName();

    /// <summary>
    /// Extracts the version from a package full name. The format is fixed by Windows:
    /// <c>Name_Version_Architecture__PublisherId</c>, e.g. <c>RyzenWang.AirNet_1.0.0.0_x64__abc</c>.
    /// </summary>
    public static string? ParseVersion(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var parts = fullName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && Version.TryParse(parts[1], out var version)
            ? version.ToString(4)
            : null;
    }

    private static string? ReadFullName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var length = 0;
            var probe = GetCurrentPackageFullName(ref length, null);
            if (probe == ErrorNoPackage)
            {
                return null;
            }

            if (probe != ErrorInsufficientBuffer || length <= 0)
            {
                return null;
            }

            var buffer = new char[length];
            if (GetCurrentPackageFullName(ref length, buffer) != 0)
            {
                return null;
            }

            // The returned length includes the terminating null.
            return new string(buffer, 0, Math.Max(0, length - 1));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}

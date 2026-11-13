using System.Diagnostics;
using System.Globalization;

namespace AirNet.Core.Probes;

/// <summary>
/// Radio data on macOS and Linux, where there is no wlanapi.
/// </summary>
/// <remarks>
/// Both platforms are read from command output rather than native APIs: CoreWLAN needs
/// Objective-C interop and nl80211 needs a netlink implementation, which is a lot of surface for
/// four numbers. The parsers are pure and unit-tested against captured output; the commands
/// themselves run with a short timeout and degrade to "no data" instead of failing the scan.
///
/// Neither platform has been exercised on real hardware by the maintainer — the Windows path is
/// the only one verified. Treat the numbers here as best effort until someone confirms them.
/// </remarks>
internal static class UnixRadio
{
    public static WifiState? TryRead()
    {
        try
        {
            return OperatingSystem.IsMacOS() ? ReadMac() : OperatingSystem.IsLinux() ? ReadLinux() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or IOException or FormatException)
        {
            return null;
        }
    }

    private static WifiState? ReadMac()
    {
        // SPAirPortDataType is the only stable way to get the current association without
        // private frameworks; it takes a second or two, hence the timeout.
        var text = Run("system_profiler", "SPAirPortDataType -detailLevel basic", 4000);
        return text is null ? null : ParseMacSystemProfiler(text);
    }

    private static WifiState? ReadLinux()
    {
        var interfaceName = FirstWirelessInterface();
        if (interfaceName is null)
        {
            return null;
        }

        var iw = Run("iw", $"dev {interfaceName} link", 1500);
        var wireless = File.Exists("/proc/net/wireless")
            ? string.Join('\n', File.ReadLines("/proc/net/wireless"))
            : null;
        var speed = ReadSysSpeed(interfaceName);

        return ParseLinuxIwLink(iw ?? string.Empty, wireless, speed);
    }

    /// <summary>First interface listed in /proc/net/wireless, which only lists wireless ones.</summary>
    internal static string? FirstWirelessInterface()
    {
        if (!File.Exists("/proc/net/wireless"))
        {
            return null;
        }

        foreach (var line in File.ReadLines("/proc/net/wireless"))
        {
            var trimmed = line.Trim();
            var colon = trimmed.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var name = trimmed[..colon].Trim();
            if (name.Length > 0 && !name.StartsWith("Inter-", StringComparison.Ordinal)
                && !name.StartsWith("face", StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the "Current Network Information" block; returns null when the machine is not
    /// associated (the block then holds only "Other Local Wi-Fi Networks").
    /// </summary>
    internal static WifiState? ParseMacSystemProfiler(string text)
    {
        string? ssid = null;
        string? phyMode = null;
        int? signalDbm = null;
        int? rateMbps = null;

        var inCurrent = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.StartsWith("Current Network Information:", StringComparison.Ordinal))
            {
                inCurrent = true;
                continue;
            }

            if (!inCurrent)
            {
                continue;
            }

            if (trimmed.StartsWith("Other Local Wi-Fi Networks:", StringComparison.Ordinal))
            {
                break;   // the neighbour list repeats "PHY Mode" for other SSIDs
            }

            if (trimmed.EndsWith(":", StringComparison.Ordinal) && !trimmed.Contains("  ", StringComparison.Ordinal))
            {
                ssid ??= trimmed.TrimEnd(':');
                continue;
            }

            if (trimmed.StartsWith("PHY Mode:", StringComparison.Ordinal))
            {
                phyMode = trimmed["PHY Mode:".Length..].Trim();
            }
            else if (trimmed.StartsWith("Signal / Noise:", StringComparison.Ordinal))
            {
                var value = trimmed["Signal / Noise:".Length..].Trim();
                var slash = value.Split('/')[0].Trim();
                var dbm = slash.Replace("dBm", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (int.TryParse(dbm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    signalDbm = parsed;
                }
            }
            else if (trimmed.StartsWith("Transmit Rate:", StringComparison.Ordinal))
            {
                var value = trimmed["Transmit Rate:".Length..].Trim();
                var digits = new string(value.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                if (double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    rateMbps = (int)Math.Round(parsed);
                }
            }
        }

        if (ssid is null && signalDbm is null && rateMbps is null)
        {
            return null;
        }

        var percent = signalDbm is { } macDbm ? PercentFromDbm(macDbm) : 0;
        return new WifiState(
            ssid ?? string.Empty,
            percent,
            (rateMbps ?? 0) * 1000,
            PhyFromMacMode(phyMode));
    }

    /// <summary>
    /// Parses `iw dev &lt;if&gt; link`, using /proc/net/wireless as a fallback for signal quality
    /// when the SSID itself is not readable (iw needs a recent kernel and sometimes root).
    /// </summary>
    internal static WifiState? ParseLinuxIwLink(string iwLink, string? procWireless, int? sysSpeedMbps)
    {
        string? ssid = null;
        int? signalDbm = null;
        int? rateMbps = null;

        foreach (var raw in iwLink.Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("SSID:", StringComparison.Ordinal))
            {
                ssid = trimmed["SSID:".Length..].Trim();
            }
            else if (trimmed.StartsWith("signal:", StringComparison.Ordinal))
            {
                var value = trimmed["signal:".Length..].Trim().Replace("dBm", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    signalDbm = parsed;
                }
            }
            else if (trimmed.StartsWith("tx bitrate:", StringComparison.Ordinal))
            {
                var digits = new string(trimmed["tx bitrate:".Length..].Trim()
                    .TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                if (double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    rateMbps = (int)Math.Round(parsed);
                }
            }
        }

        // /proc/net/wireless "level" is dBm on most drivers and a 0-100 quality on a few.
        if (signalDbm is null && procWireless is not null)
        {
            signalDbm = SignalFromProcNetWireless(procWireless);
        }

        if (ssid is null && signalDbm is null && rateMbps is null && sysSpeedMbps is null)
        {
            return null;
        }

        return new WifiState(
            ssid ?? string.Empty,
            signalDbm is { } dbm ? PercentFromDbm(dbm) : 0,
            (rateMbps ?? sysSpeedMbps ?? 0) * 1000,
            string.Empty);   // Linux has no portable PHY name; the UI falls back to "Wi-Fi"
    }

    /// <summary>Column 3 of a /proc/net/wireless data row (after the interface name).</summary>
    internal static int? SignalFromProcNetWireless(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var trimmed = raw.Trim();
            var colon = trimmed.IndexOf(':');
            if (colon <= 0 || trimmed.StartsWith("Inter-", StringComparison.Ordinal)
                || trimmed.StartsWith("face", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = trimmed[(colon + 1)..].Split([' ', '.'], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3)
            {
                continue;
            }

            if (int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
            {
                // Drivers report either dBm (negative) or a quality out of 100.
                return level < 0 ? level : null;
            }
        }

        return null;
    }

    /// <summary>Inverse of the estimate used elsewhere: quality = 2 × (dBm + 100), clamped.</summary>
    internal static int PercentFromDbm(int dbm) => Math.Clamp(2 * (dbm + 100), 0, 100);

    internal static string PhyFromMacMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return string.Empty;
        }

        var lower = mode.ToLowerInvariant();
        return lower switch
        {
            _ when lower.Contains("be") => "Wi-Fi 7",
            _ when lower.Contains("ax") => "Wi-Fi 6",
            _ when lower.Contains("ac") => "Wi-Fi 5",
            _ when lower.Contains("n") => "Wi-Fi 4",
            _ when lower.Contains("a") || lower.Contains("g") || lower.Contains("b") => "旧标准",
            _ => string.Empty,
        };
    }

    private static int? ReadSysSpeed(string interfaceName)
    {
        try
        {
            var path = $"/sys/class/net/{interfaceName}/speed";
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed)
                ? speed
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Runs a command with a hard timeout; null when it is missing or hangs.</summary>
    private static string? Run(string file, string arguments, int timeoutMs)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Already exited between the check and the kill.
                }

                return null;
            }

            return output.Wait(timeoutMs) ? output.Result : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or IOException or AggregateException)
        {
            return null;
        }
    }
}

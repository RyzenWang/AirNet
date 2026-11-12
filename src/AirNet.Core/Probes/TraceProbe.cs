using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Hop-by-hop route discovery. Uses the platform's own traceroute binary rather than raw
/// sockets: raw ICMP needs elevation on Windows, and the system tool is already installed,
/// localized, and handles TTL expiry for us.
/// </summary>
public static partial class TraceProbe
{
    private static readonly TimeSpan HardLimit = TimeSpan.FromSeconds(25);

    public static async Task<TraceResult> TraceAsync(
        string target,
        int maxHops = 16,
        int probeTimeoutMs = 500,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeTarget(target))
        {
            return TraceResult.Unavailable(target, "目标地址不合法");
        }

        var attempts = OperatingSystem.IsWindows()
            ? new[] { ("tracert", $"-d -h {maxHops} -w {probeTimeoutMs} {target}") }
            : new[]
            {
                ("traceroute", $"-n -m {maxHops} -w 1 {target}"),
                ("tracepath", $"-n -m {maxHops} {target}"),
            };

        foreach (var (fileName, arguments) in attempts)
        {
            var output = await RunAsync(fileName, arguments, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(output))
            {
                continue;
            }

            var hops = Parse(output);
            if (hops.Count > 0)
            {
                var reached = hops.LastOrDefault(h => h.Responded)?.Index ?? hops.Count;
                var reachedTarget = await TargetReachedAsync(target, hops, cancellationToken).ConfigureAwait(false);
                return new TraceResult(target, hops, true, reached, null, reachedTarget);
            }
        }

        return TraceResult.Unavailable(target, "系统 traceroute 不可用或被拦截");
    }

    /// <summary>Only dotted IPv4 and hostnames; arguments are passed without a shell, but keep it strict.</summary>
    private static bool IsSafeTarget(string target) =>
        target.Length is > 0 and <= 64 && SafeTargetRegex().IsMatch(target);

    private static async Task<string?> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var readTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var finished = await Task.WhenAny(readTask, Task.Delay(HardLimit, cancellationToken))
                .ConfigureAwait(false);

            if (finished != readTask)
            {
                TryKill(process);
                return null;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return await readTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or OperationCanceledException or IOException)
        {
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // already gone
        }
    }

    /// <summary>
    /// A traceroute can stop at a router that filters TTL-expired probes without reaching the
    /// target. Only a last hop that matches the target proves the path end; a resolver failure
    /// returns null so the caller keeps the older, less precise behaviour.
    /// </summary>
    private static async Task<bool?> TargetReachedAsync(
        string target,
        IReadOnlyList<TraceHop> hops,
        CancellationToken cancellationToken)
    {
        var last = hops.LastOrDefault(h => h.Responded);
        if (last is null)
        {
            return false;
        }

        if (IPAddress.TryParse(target, out var literal))
        {
            return string.Equals(last.Address, literal.ToString(), StringComparison.Ordinal);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(target, cancellationToken).ConfigureAwait(false);
            return addresses.Any(a => string.Equals(a.ToString(), last.Address, StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses both traceroute dialects. Latency is read from any "&lt;1 ms" / "12.345 ms" token
    /// and the hop address from the dotted quad, which keeps the parser independent of the
    /// localized timeout text.
    /// </summary>
    internal static List<TraceHop> Parse(string output)
    {
        var hops = new List<TraceHop>();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var head = HopLineRegex().Match(line);
            if (!head.Success)
            {
                continue;
            }

            var index = int.Parse(head.Groups[1].Value);
            var rest = head.Groups[2].Value;

            var address = Ipv4Regex().Match(rest);
            var latencies = LatencyRegex().Matches(rest)
                .Select(m => m.Groups[1].Value.TrimStart('<'))
                .Select(v => double.TryParse(v, out var parsed) ? parsed : double.NaN)
                .Where(v => !double.IsNaN(v))
                .ToList();

            var responded = address.Success && latencies.Count > 0;
            hops.Add(new TraceHop(
                index,
                address.Success ? address.Value : "*",
                responded ? Math.Round(latencies.Min(), 1) : null,
                responded));
        }

        return hops;
    }

    [GeneratedRegex(@"^[A-Za-z0-9.\-:]+$")]
    private static partial Regex SafeTargetRegex();

    [GeneratedRegex(@"^\s*(\d{1,2})\s+(.+)$")]
    private static partial Regex HopLineRegex();

    [GeneratedRegex(@"\b(\d{1,3}(?:\.\d{1,3}){3})\b")]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex(@"([\d.]+|<[\d.]+)\s*ms")]
    private static partial Regex LatencyRegex();
}

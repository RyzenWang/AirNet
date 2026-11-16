using System.Globalization;
using System.Text;
using AirNet.Core.Diagnostics;
using AirNet.Core.History;
using AirNet.Core.Probes;
using AirNet.Core.Reporting;

namespace AirNet.Cli;

internal static class Program
{
    private static readonly CancellationTokenSource Cancellation = new();

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cancellation.Cancel();
        };

        try
        {
            return await DispatchAsync(args, Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 3;
        }
    }

    private static async Task<int> DispatchAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsAny(args[0], "-h", "--help", "help"))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args[1..];

        switch (command)
        {
            case "scan":
            case "probe":
                return await ScanAsync(rest, cancellationToken).ConfigureAwait(false);
            case "watch":
                return await WatchAsync(rest, cancellationToken).ConfigureAwait(false);
            case "history":
                return await HistoryAsync(rest, cancellationToken).ConfigureAwait(false);
            case "version":
            case "--version":
                Console.WriteLine(AppVersion.Current);
                return 0;
            default:
                Console.Error.WriteLine($"unknown command: {args[0]}");
                Console.Error.WriteLine("run: airnet-cli --help");
                return 2;
        }
    }

    private static async Task<int> ScanAsync(string[] args, CancellationToken cancellationToken)
    {
        if (HasFlag(args, "--help", "-h"))
        {
            Console.WriteLine("usage: airnet-cli scan [--json [PATH]] [--bandwidth]");
            return 0;
        }

        var json = ParseJsonTarget(args, out var jsonPath);
        var bandwidth = HasFlag(args, "--bandwidth");
        var progress = new Progress<ScanProgress>(p =>
            Console.Error.WriteLine($"[{p.Fraction,4:P0}] {p.Phase}"));

        var snapshot = await new MeasurementRunner()
            .RunAsync(0, 0, progress, live: null, dualProbe: true, includeBandwidth: bandwidth,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            await WriteJsonAsync(snapshot, jsonPath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            PrintSnapshot(snapshot);
        }

        return ExitCode(snapshot);
    }

    private static async Task<int> WatchAsync(string[] args, CancellationToken cancellationToken)
    {
        if (HasFlag(args, "--help", "-h"))
        {
            Console.WriteLine("usage: airnet-cli watch [--interval SECONDS] [--count N] [--json] [--bandwidth]");
            return 0;
        }

        var interval = Math.Max(1, ReadInt(args, "--interval", 5));
        var count = Math.Max(0, ReadInt(args, "--count", 0));
        var json = HasFlag(args, "--json");
        var bandwidth = HasFlag(args, "--bandwidth");
        var runner = new MeasurementRunner();
        var iteration = 0;

        while (count == 0 || iteration < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iteration++;

            var snapshot = await runner.RunAsync(
                    0, 0, progress: null, live: null, dualProbe: true,
                    includeBandwidth: bandwidth, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (json)
            {
                Console.WriteLine(ProbeJsonContext.SerializeCompact(ProbeJson.From(snapshot, AppVersion.Current)));
                await Console.Out.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var down = snapshot.HasBandwidth ? $"{snapshot.DownloadMbps:0.0}/{snapshot.UploadMbps:0.0} Mbps" : "bandwidth off";
                Console.WriteLine(
                    $"{snapshot.CapturedAt.ToLocalTime():HH:mm:ss}  " +
                    $"score {snapshot.Score,3} {snapshot.GradeLabel,-4}  " +
                    $"latency {snapshot.Latency.AverageMs,6:0.0} ms  " +
                    $"jitter {snapshot.Latency.JitterMs,5:0.0} ms  " +
                    $"loss {snapshot.Latency.LossPercent,4:0.0}%  {down}");
            }

            if (count > 0 && iteration >= count)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> HistoryAsync(string[] args, CancellationToken cancellationToken)
    {
        if (HasFlag(args, "--help", "-h"))
        {
            Console.WriteLine("usage: airnet-cli history [--json] [--limit N]");
            return 0;
        }

        var limit = Math.Max(1, ReadInt(args, "--limit", 10));
        var records = await new HistoryStore().LoadAsync(limit, cancellationToken).ConfigureAwait(false);
        var json = HasFlag(args, "--json");

        if (json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(records));
            return 0;
        }

        if (records.Count == 0)
        {
            Console.WriteLine("no local history");
            return 0;
        }

        foreach (var record in records)
        {
            Console.WriteLine(
                $"{record.At.ToLocalTime():yyyy-MM-dd HH:mm}  {record.Score,3} {record.Grade,-9} " +
                $"{record.LatencyAvg,6:0.0} ms  {record.DownMbps,7:0.0}/{record.UpMbps,6:0.0} Mbps  {record.Verdict}");
        }

        return 0;
    }

    private static async Task WriteJsonAsync(
        NetworkSnapshot snapshot,
        string? path,
        CancellationToken cancellationToken)
    {
        var json = ProbeJsonContext.Serialize(ProbeJson.From(snapshot, AppVersion.Current));
        if (string.IsNullOrWhiteSpace(path) || path == "-")
        {
            Console.WriteLine(json);
            return;
        }

        await File.WriteAllTextAsync(path, json + Environment.NewLine, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        Console.Error.WriteLine($"json: {path}");
    }

    private static void PrintSnapshot(NetworkSnapshot snapshot)
    {
        Console.WriteLine($"AirNet {AppVersion.Current}  {snapshot.CapturedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"{snapshot.Score}/100  {snapshot.GradeLabel}  {snapshot.Verdict}");
        Console.WriteLine($"latency {snapshot.Latency.AverageMs:0.0} ms  jitter {snapshot.Latency.JitterMs:0.0} ms  " +
                          $"loss {snapshot.Latency.LossPercent:0.0}%  P95 {snapshot.Latency.P95Ms:0.0} ms");
        if (snapshot.HasBandwidth)
        {
            Console.WriteLine($"download {snapshot.DownloadMbps:0.0} Mbps  upload {snapshot.UploadMbps:0.0} Mbps");
        }
        else
        {
            Console.WriteLine("bandwidth not measured (add --bandwidth)");
        }
    }

    private static int ExitCode(NetworkSnapshot snapshot) => snapshot.Score switch
    {
        >= 90 => 0,
        >= 75 => 1,
        >= 60 => 2,
        _ => 3,
    };

    private static bool ParseJsonTarget(string[] args, out string? path)
    {
        var index = Array.FindIndex(args, a => a == "--json");
        path = null;
        if (index < 0)
        {
            return false;
        }

        if (index + 1 < args.Length && !args[index + 1].StartsWith('-'))
        {
            path = args[index + 1];
        }

        return true;
    }

    private static int ReadInt(string[] args, string name, int fallback)
    {
        var index = Array.FindIndex(args, a => a == name);
        return index >= 0 && index + 1 < args.Length
               && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static bool HasFlag(string[] args, params string[] names) =>
        args.Any(a => names.Contains(a, StringComparer.OrdinalIgnoreCase));

    private static bool IsAny(string value, params string[] choices) =>
        choices.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static void PrintHelp() =>
        Console.WriteLine("""
            AirNet CLI — headless network measurements

            usage:
              airnet-cli scan [--json [PATH]] [--bandwidth]
              airnet-cli watch [--interval SECONDS] [--count N] [--json] [--bandwidth]
              airnet-cli history [--json] [--limit N]
              airnet-cli version

            exit codes (scan):
              0 healthy (>= 90) · 1 fair (>= 75) · 2 poor (>= 60) · 3 failed (< 60 or error)

            notes:
              --json without a PATH prints to stdout; with a PATH writes that file.
              watch --json prints one ProbeJson object per line (NDJSON).
              scan/watch never write history; use the GUI for persisted runs.
            """);
}

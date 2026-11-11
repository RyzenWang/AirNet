using System.Text.Json;
using System.Text.Json.Serialization;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.History;

/// <summary>One persisted run, flattened so a whole history line stays a few hundred bytes.</summary>
public sealed class HistoryRecord
{
    public DateTimeOffset At { get; set; }
    public int Score { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Ssid { get; set; } = string.Empty;
    public double LatencyAvg { get; set; }
    public double Jitter { get; set; }
    public double Loss { get; set; }
    public double DownMbps { get; set; }
    public double UpMbps { get; set; }
    public string LocalAddress { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty;
    public bool IsSample { get; set; }
}

/// <summary>
/// Source-generated JSON keeps the store trim-safe: reflection-based serialization is the first
/// thing that breaks under <c>PublishTrimmed</c>.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(HistoryRecord))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;

/// <summary>
/// Append-only JSONL history. A file of short lines stays readable and greppable, needs no
/// native dependency, and survives a crash mid-write far better than a rewritten document.
/// </summary>
public sealed class HistoryStore
{
    private const int MaxRecords = 500;
    private const long TrimThresholdBytes = 256 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public HistoryStore(string? directory = null)
    {
        // AIRNET_DATA_DIR lets the screenshot pipeline (and tests) use an isolated, empty store
        // instead of inheriting the developer's real measurements.
        var root = directory
            ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirNet");
        Directory.CreateDirectory(root);
        FilePath = Path.Combine(root, "history.jsonl");
    }

    public string FilePath { get; }

    public async Task AppendAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(ToRecord(snapshot), HistoryJsonContext.Default.HistoryRecord);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(FilePath, line + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
            await TrimIfNeededAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Newest first. Records older than the retention window are skipped.</summary>
    public async Task<IReadOnlyList<HistoryRecord>> LoadAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cutoff = DateTimeOffset.Now - Retention;
            var records = new List<HistoryRecord>();
            foreach (var line in await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var record = TryParse(line);
                if (record is not null && record.At >= cutoff)
                {
                    records.Add(record);
                }
            }

            records.Reverse();   // file order is oldest first
            return records.Count <= limit ? records : records.GetRange(0, limit);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return 0;
        }

        var lines = await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false);
        return lines.Count(l => !string.IsNullOrWhiteSpace(l));
    }

    public Task ClearAsync()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }

        return Task.CompletedTask;
    }

    private async Task TrimIfNeededAsync(CancellationToken cancellationToken)
    {
        var info = new FileInfo(FilePath);
        if (!info.Exists || info.Length < TrimThresholdBytes)
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false);
        var keep = lines.Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(MaxRecords);
        await File.WriteAllLinesAsync(FilePath, keep, cancellationToken).ConfigureAwait(false);
    }

    private static HistoryRecord? TryParse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize(line, HistoryJsonContext.Default.HistoryRecord);
        }
        catch (JsonException)
        {
            return null;   // a half-written tail must not poison the whole history
        }
    }

    private static HistoryRecord ToRecord(NetworkSnapshot snapshot) => new()
    {
        At = snapshot.CapturedAt,
        Score = snapshot.Score,
        Grade = snapshot.GradeLabel,
        Kind = snapshot.Interface.Kind,
        Ssid = snapshot.Interface.Ssid,
        LatencyAvg = snapshot.Latency.AverageMs,
        Jitter = snapshot.Latency.JitterMs,
        Loss = snapshot.Latency.LossPercent,
        DownMbps = snapshot.DownloadMbps,
        UpMbps = snapshot.UploadMbps,
        LocalAddress = snapshot.Interface.LocalAddress,
        Verdict = snapshot.Verdict,
        IsSample = snapshot.IsSample,
    };
}

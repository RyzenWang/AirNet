using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirNet.Core.Sites;

/// <summary>One compact site row persisted in sites.jsonl.</summary>
public sealed class SiteHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double DnsMs { get; set; }
    public double TcpMs { get; set; }
    public double TlsMs { get; set; }
    public double TtfbMs { get; set; }
    public int? HttpStatus { get; set; }
    public string FailureKind { get; set; } = string.Empty;
    public string? FailureNote { get; set; }
}

/// <summary>One persisted site-latency run.</summary>
public sealed class SiteHistoryRecord
{
    public DateTimeOffset At { get; set; }
    public List<SiteHistoryEntry> Sites { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(SiteHistoryRecord))]
internal sealed partial class SiteHistoryJsonContext : JsonSerializerContext;

/// <summary>
/// Append-only site-latency history. It deliberately uses its own file instead of mixing with
/// history.jsonl: this keeps the main snapshot history independent from a 33-request site run.
/// </summary>
public sealed class SiteHistoryStore
{
    private const int MaxRecords = 500;
    private const long TrimThresholdBytes = 256 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public SiteHistoryStore(string? directory = null)
    {
        var root = directory
                   ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
                   ?? Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                       "AirNet");
        Directory.CreateDirectory(root);
        FilePath = Path.Combine(root, "sites.jsonl");
    }

    public string FilePath { get; }

    public async Task AppendAsync(SiteLatencyReport report, CancellationToken cancellationToken = default)
    {
        if (report.Results.Count == 0)
        {
            return;
        }

        var line = JsonSerializer.Serialize(ToRecord(report), SiteHistoryJsonContext.Default.SiteHistoryRecord);

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

    /// <summary>Newest first; records older than the retention window are skipped.</summary>
    public async Task<IReadOnlyList<SiteLatencyReport>> LoadAsync(
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
            var records = new List<SiteLatencyReport>();
            foreach (var line in await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var record = TryParse(line);
                if (record is not null && record.At >= cutoff)
                {
                    records.Add(ToReport(record));
                }
            }

            records.Reverse();
            return records.Count <= limit ? records : records.GetRange(0, limit);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SiteLatencyReport?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var records = await LoadAsync(1, cancellationToken).ConfigureAwait(false);
        return records.Count == 0 ? null : records[0];
    }

    private async Task TrimIfNeededAsync(CancellationToken cancellationToken)
    {
        var info = new FileInfo(FilePath);
        if (!info.Exists || info.Length < TrimThresholdBytes)
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false);
        var keep = lines.Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(MaxRecords);
        await File.WriteAllLinesAsync(FilePath, keep, cancellationToken).ConfigureAwait(false);
    }

    private static SiteHistoryRecord? TryParse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize(line, SiteHistoryJsonContext.Default.SiteHistoryRecord);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SiteHistoryRecord ToRecord(SiteLatencyReport report) => new()
    {
        At = report.StartedAt,
        Sites = report.Results.Select(ToEntry).ToList(),
    };

    private static SiteHistoryEntry ToEntry(SiteResult result)
    {
        var median = result.Median;
        return new SiteHistoryEntry
        {
            Id = result.Target.Id,
            Name = result.Target.Name,
            Host = result.Target.Host,
            Group = result.Target.Group.ToString(),
            Success = result.Success,
            DnsMs = median?.DnsMs ?? 0,
            TcpMs = median?.TcpMs ?? 0,
            TlsMs = median?.TlsMs ?? 0,
            TtfbMs = median?.TtfbMs ?? 0,
            HttpStatus = result.HttpStatus,
            FailureKind = result.FailureKind.ToString(),
            FailureNote = result.FailureNote,
        };
    }

    private static SiteLatencyReport ToReport(SiteHistoryRecord record)
    {
        var results = record.Sites.Select(entry => ToResult(entry, record.At)).ToList();
        return new SiteLatencyReport(results, record.At, TimeSpan.Zero);
    }

    private static SiteResult ToResult(SiteHistoryEntry entry, DateTimeOffset at)
    {
        var group = Enum.TryParse<SiteGroup>(entry.Group, ignoreCase: true, out var parsed)
            ? parsed
            : SiteGroup.Custom;
        var target = new SiteTarget(
            entry.Id,
            entry.Name,
            entry.Host,
            group,
            IsCustom: group == SiteGroup.Custom);
        var kind = Enum.TryParse<SiteFailureKind>(entry.FailureKind, ignoreCase: true, out var parsedKind)
            ? parsedKind
            : entry.Success ? SiteFailureKind.None : SiteFailureKind.Unknown;
        var median = entry.Success
            ? new SiteSample(
                entry.DnsMs,
                entry.TcpMs,
                entry.TlsMs,
                entry.TtfbMs,
                true,
                kind,
                entry.FailureNote,
                entry.HttpStatus)
            : null;

        return new SiteResult(
            target,
            median is null ? [] : [median],
            median,
            entry.Success,
            entry.Success ? 1 : 0,
            kind,
            entry.FailureNote,
            At: at);
    }
}

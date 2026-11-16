using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirNet.Core.Services;

/// <summary>One attempted DNS change, successful or not. Local only, JSONL, never uploaded.</summary>
public sealed class DnsOperationEntry
{
    public DateTimeOffset At { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Adapter { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(DnsOperationEntry))]
internal sealed partial class DnsOperationJsonContext : JsonSerializerContext;

/// <summary>
/// Append-only audit trail for the only write operation AirNet exposes. Kept separate from
/// <see cref="DnsBackupStore"/>: the backup answers "what do I restore", this answers
/// "what did I try, when, and did it work".
/// </summary>
public sealed class DnsOperationLog
{
    private const int MaxEntries = 200;

    public DnsOperationLog(string? directory = null)
    {
        var root = directory
            ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirNet");
        Directory.CreateDirectory(root);
        FilePath = Path.Combine(root, "dns-operations.jsonl");
    }

    public string FilePath { get; }

    public async Task AppendAsync(DnsOperationEntry entry, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(entry, DnsOperationJsonContext.Default.DnsOperationEntry);
        await File.AppendAllTextAsync(FilePath, line + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
        await TrimAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Newest first; malformed lines are skipped rather than breaking the settings page.</summary>
    public async Task<IReadOnlyList<DnsOperationEntry>> LoadAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var entries = new List<DnsOperationEntry>();
        foreach (var line in await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize(line, DnsOperationJsonContext.Default.DnsOperationEntry)
                    is { } entry)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // One truncated line must not hide the rest of the audit trail.
            }
        }

        entries.Reverse();
        return limit <= 0 ? entries : entries.Take(limit).ToList();
    }

    private async Task TrimAsync(CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false);
        if (lines.Length <= MaxEntries)
        {
            return;
        }

        await File.WriteAllLinesAsync(
                FilePath,
                lines[^MaxEntries..],
                cancellationToken)
            .ConfigureAwait(false);
    }
}

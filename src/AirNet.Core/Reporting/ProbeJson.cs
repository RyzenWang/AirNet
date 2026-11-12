using System.Text.Json;
using System.Text.Json.Serialization;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Reporting;

/// <summary>
/// Stable JSON projection for `AirNet --probe --json`. This is a public contract: scripts will
/// parse it, so fields are only ever added, and every rename is a schema bump.
/// </summary>
public sealed record ProbeJson(
    int SchemaVersion,
    string App,
    string Version,
    DateTimeOffset CapturedAt,
    int Score,
    string Grade,
    string GradeLabel,
    string Verdict,
    ProbeJsonLatency Latency,
    ProbeJsonTiming Timing,
    ProbeJsonDualStack DualStack,
    ProbeJsonInterface Interface,
    ProbeJsonInterference Interference,
    ProbeJsonDns Dns,
    IReadOnlyList<ProbeJsonFinding> Findings)
{
    public const int CurrentSchemaVersion = 1;

    public static ProbeJson From(NetworkSnapshot snapshot, string version) => new(
        CurrentSchemaVersion,
        "AirNet",
        version,
        snapshot.CapturedAt,
        snapshot.Score,
        snapshot.Grade.ToString(),
        snapshot.GradeLabel,
        snapshot.Verdict,
        new ProbeJsonLatency(
            snapshot.Latency.AverageMs,
            snapshot.Latency.P95Ms,
            snapshot.Latency.JitterMs,
            snapshot.Latency.LossPercent,
            snapshot.Latency.Sent,
            snapshot.Latency.Received),
        new ProbeJsonTiming(
            snapshot.Timing.Host,
            snapshot.Timing.DnsMs,
            snapshot.Timing.TcpMs,
            snapshot.Timing.TlsMs,
            snapshot.Timing.TtfbMs,
            snapshot.Timing.TotalMs,
            snapshot.Timing.Succeeded,
            snapshot.Timing.ViaProxy,
            snapshot.Timing.HttpStatus),
        new ProbeJsonDualStack(
            snapshot.DualStack.Verdict.ToString(),
            snapshot.DualStack.V4Ms,
            snapshot.DualStack.V6Ms,
            snapshot.DualStack.Note),
        new ProbeJsonInterface(
            snapshot.Interface.Name,
            snapshot.Interface.Kind,
            snapshot.Interface.IsWireless,
            snapshot.Interface.Ssid,
            snapshot.Interface.SignalPercent,
            snapshot.Interface.LinkSpeedMbps,
            snapshot.Interface.LocalAddress,
            snapshot.Interface.Gateway,
            snapshot.EgressText),
        new ProbeJsonInterference(
            snapshot.Interference.ProxyConfigured,
            snapshot.Interference.ProxyText,
            snapshot.Interference.TunnelPresent,
            snapshot.Interference.TunnelText),
        new ProbeJsonDns(
            snapshot.Dns.Current?.Address ?? "-",
            snapshot.Dns.Current?.MedianMs,
            snapshot.Dns.Alternatives
                .Select(a => new ProbeJsonDnsServer(a.Label, a.Address, a.MedianMs))
                .ToList(),
            snapshot.Dns.WorthChanging,
            snapshot.Dns.Verdict),
        snapshot.Findings
            .Select(f => new ProbeJsonFinding(f.Severity.ToString(), f.Title, f.Detail))
            .ToList());
}

public sealed record ProbeJsonLatency(
    double AverageMs,
    double P95Ms,
    double JitterMs,
    double LossPercent,
    int Sent,
    int Received);

public sealed record ProbeJsonTiming(
    string Host,
    double DnsMs,
    double TcpMs,
    double TlsMs,
    double TtfbMs,
    double TotalMs,
    bool Succeeded,
    bool ViaProxy,
    int? HttpStatus);

public sealed record ProbeJsonDualStack(
    string Verdict,
    double? V4Ms,
    double? V6Ms,
    string Note);

public sealed record ProbeJsonInterface(
    string Name,
    string Kind,
    bool IsWireless,
    string? Ssid,
    double? SignalPercent,
    double LinkSpeedMbps,
    string LocalAddress,
    string Gateway,
    string Egress);

public sealed record ProbeJsonInterference(
    bool Proxy,
    string ProxyText,
    bool Tunnel,
    string TunnelText);

public sealed record ProbeJsonDns(
    string CurrentAddress,
    double? CurrentMs,
    IReadOnlyList<ProbeJsonDnsServer> Servers,
    bool WorthChanging,
    string Verdict);

public sealed record ProbeJsonDnsServer(string Label, string Address, double? MedianMs);

public sealed record ProbeJsonFinding(string Severity, string Title, string Detail);

/// <summary>
/// Source-generated serialisation: the app ships trimmed, and reflection-based JSON would be
/// cut away with no build-time signal.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProbeJson))]
public sealed partial class ProbeJsonContext : JsonSerializerContext
{
    public static string Serialize(ProbeJson probe) =>
        JsonSerializer.Serialize(probe, Default.ProbeJson);
}

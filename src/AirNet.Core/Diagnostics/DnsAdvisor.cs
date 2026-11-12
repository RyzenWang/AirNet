namespace AirNet.Core.Diagnostics;

/// <summary>
/// A reversible DNS change for the current adapter: the exact commands to apply it and to put
/// the machine back. AirNet shows the command before running anything — changing DNS is a real
/// system change and must never happen silently.
/// </summary>
public sealed record DnsPlan(
    string AdapterName,
    string CurrentDns,
    string ProposedDns,
    string ApplyCommand,
    string RestoreCommand,
    string Headline,
    string Rationale,
    bool IsWorthChanging)
{
    public static DnsPlan Unavailable(string reason) => new(
        "-", "-", "-", string.Empty, string.Empty, "无法给出 DNS 建议", reason, false);
}

public static class DnsAdvisor
{
    private const string PreferredResolver = "223.5.5.5";
    private const string SecondaryResolver = "1.1.1.1";

    /// <summary>Only recommend a change when resolution is measurably slow and not already optimal.</summary>
    public static DnsPlan Plan(InterfaceInfo nic, LayerTiming timing) => Plan(nic, timing, null);

    /// <summary>
    /// Same, but with measured evidence: when the comparison has data, the recommendation quotes
    /// the numbers instead of inferring from a single 25 ms threshold.
    /// </summary>
    public static DnsPlan Plan(InterfaceInfo nic, LayerTiming timing, DnsComparison? comparison)
    {
        if (string.IsNullOrWhiteSpace(nic.Name) || nic.Name == "未检测到活动网卡")
        {
            return DnsPlan.Unavailable("未检测到活动网卡");
        }

        if (!timing.Succeeded)
        {
            return DnsPlan.Unavailable(timing.FailureNote ?? "首包分解不可用，无法判断 DNS 耗时");
        }

        var current = nic.DnsServer;
        var alreadyOptimal = current == PreferredResolver || current == SecondaryResolver;

        if (!alreadyOptimal && comparison is { HasEvidence: true })
        {
            var fastest = comparison.Best!.Address;
            var secondary = fastest == PreferredResolver ? SecondaryResolver : PreferredResolver;
            var worthChanging = comparison.WorthChanging;

            return new DnsPlan(
                AdapterName: nic.Name,
                CurrentDns: string.IsNullOrWhiteSpace(current) ? "自动（DHCP）" : current,
                ProposedDns: $"{fastest} / {secondary}",
                ApplyCommand: BuildApply(nic.Name, fastest, secondary),
                RestoreCommand: BuildRestore(nic.Name, current),
                Headline: comparison.Verdict,
                Rationale: comparison.Detail,
                IsWorthChanging: worthChanging);
        }

        var heuristicWorthChanging = timing.DnsMs >= 25 && !alreadyOptimal;

        var headline = alreadyOptimal
            ? "当前 DNS 已是最优解之一"
            : heuristicWorthChanging
                ? $"解析用了 {timing.DnsMs:0} ms，建议换成 {PreferredResolver}"
                : $"解析只有 {timing.DnsMs:0} ms，无需调整";

        var rationale = heuristicWorthChanging
            ? "DNS 慢会拖慢每个新连接的首包时间。切换后可用同一页复测验证，随时可还原。"
            : "当前解析延迟在正常范围内，改 DNS 不会带来可感知收益，不建议折腾。";

        return new DnsPlan(
            AdapterName: nic.Name,
            CurrentDns: string.IsNullOrWhiteSpace(current) ? "自动（DHCP）" : current,
            ProposedDns: $"{PreferredResolver} / {SecondaryResolver}",
            ApplyCommand: BuildApply(nic.Name, PreferredResolver, SecondaryResolver),
            RestoreCommand: BuildRestore(nic.Name, current),
            Headline: headline,
            Rationale: rationale,
            IsWorthChanging: heuristicWorthChanging);
    }

    private static string BuildApply(string adapter, string primary, string secondary)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"netsh interface ip set dns name=\"{adapter}\" static {primary} primary"
                   + Environment.NewLine
                   + $"netsh interface ip add dns name=\"{adapter}\" {secondary} index=2";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"networksetup -setdnsservers \"{adapter}\" {primary} {secondary}";
        }

        return $"nmcli con mod \"{adapter}\" ipv4.dns \"{primary} {secondary}\" "
               + Environment.NewLine
               + $"nmcli con up \"{adapter}\"";
    }

    private static string BuildRestore(string adapter, string current)
    {
        // An empty resolver, or one inside the private ranges, almost always came from the router
        // over DHCP — restoring with source=dhcp returns the adapter to its original state.
        var looksDynamic = string.IsNullOrWhiteSpace(current)
                           || current is "-" or "自动（DHCP）"
                           || IsPrivate(current);

        if (OperatingSystem.IsWindows())
        {
            return looksDynamic
                ? $"netsh interface ip set dns name=\"{adapter}\" source=dhcp"
                : $"netsh interface ip set dns name=\"{adapter}\" static {current} primary";
        }

        if (OperatingSystem.IsMacOS())
        {
            return looksDynamic
                ? $"networksetup -setdnsservers \"{adapter}\" Empty"
                : $"networksetup -setdnsservers \"{adapter}\" {current}";
        }

        return looksDynamic
            ? $"nmcli con mod \"{adapter}\" ipv4.ignore-auto-dns no && nmcli con up \"{adapter}\""
            : $"nmcli con mod \"{adapter}\" ipv4.dns \"{current}\" && nmcli con up \"{adapter}\"";
    }

    private static bool IsPrivate(string address)
    {
        if (!System.Net.IPAddress.TryParse(address, out var parsed))
        {
            return false;
        }

        var octets = parsed.ToString().Split('.');
        if (octets.Length != 4)
        {
            return false;
        }

        var a = int.Parse(octets[0], System.Globalization.CultureInfo.InvariantCulture);
        var b = int.Parse(octets[1], System.Globalization.CultureInfo.InvariantCulture);
        return a == 10 || (a == 192 && b == 168) || (a == 172 && b is >= 16 and <= 31);
    }
}

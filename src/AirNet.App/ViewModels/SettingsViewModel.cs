using AirNet.Core.Diagnostics;
using AirNet.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>
/// Preferences, plus the only two places AirNet can change the machine: the history file and
/// (behind an explicit confirmation) the adapter's DNS servers.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly DnsBackupStore _dnsBackups = new();

    public SettingsViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _autoRefresh = shell.Preferences.AutoRefresh;
        _dualProbe = shell.Preferences.DualProbe;
        _keepHistory = shell.Preferences.KeepHistory;
        _dnsPlan = DnsAdvisor.Plan(shell.Snapshot.Interface, shell.Snapshot.Timing);
        // Evidence rows too, so the sample state already shows the comparison table.
        RefreshDnsPlan();
        _ = InitializeAsync();
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private bool _autoRefresh;

    [ObservableProperty]
    private bool _dualProbe;

    [ObservableProperty]
    private bool _keepHistory;

    [ObservableProperty]
    private string _historyCountText = "读取中…";

    [ObservableProperty]
    private string _historyStatusText = string.Empty;

    [ObservableProperty]
    private DnsPlan _dnsPlan;

    [ObservableProperty]
    private string _dnsCommandText = string.Empty;

    [ObservableProperty]
    private string _dnsStatusText = string.Empty;

    [ObservableProperty]
    private bool _hasDnsBackup;
    /// <summary>Measured resolver timings, or empty when this run had no evidence.</summary>
    [ObservableProperty]
    private IReadOnlyList<DnsServerRow> _dnsServerRows = [];
    [ObservableProperty]
    private string _dnsBackupText = string.Empty;

    public string VersionText => "0.2.5 · M3";
    public string RuntimeText => ".NET 10 · Avalonia 11.3 · Windows / macOS / Linux";
    public string FootprintText => "R2R 单文件 · 无驱动 · 无后台常驻";
    public string PrivacyText =>
        "测量在本机完成；出口 IP 查询会访问 3322 / Cloudflare，不上传本地字段。历史记录仅保存在本地。";

    public bool CanApplyDns => DnsPlan.IsWorthChanging;
    public bool HasDnsEvidence => DnsServerRows.Count > 0;

    public string DnsCurrentText => $"当前 {DnsPlan.CurrentDns}";

    partial void OnAutoRefreshChanged(bool value)
    {
        Shell.Preferences.AutoRefresh = value;
        Shell.SavePreferences();
        Shell.ApplyAutoRefreshPreference();
    }

    partial void OnDualProbeChanged(bool value)
    {
        Shell.Preferences.DualProbe = value;
        Shell.SavePreferences();
    }

    partial void OnKeepHistoryChanged(bool value)
    {
        Shell.Preferences.KeepHistory = value;
        Shell.SavePreferences();
    }

    public async Task InitializeAsync()
    {
        await RefreshHistoryCountAsync().ConfigureAwait(true);
        await RefreshDnsBackupAsync().ConfigureAwait(true);
    }

    public async Task RefreshHistoryCountAsync()
    {
        var count = await Shell.History.CountAsync().ConfigureAwait(true);
        HistoryCountText = count == 0 ? "暂无记录" : $"{count} 条记录 · 保留 30 天";
    }

    /// <summary>Re-planned after every scan, because the recommendation now rests on measurements.</summary>
    public void RefreshDnsPlan()
    {
        var comparison = Shell.Snapshot.Dns;
        DnsPlan = DnsAdvisor.Plan(Shell.Snapshot.Interface, Shell.Snapshot.Timing, comparison);
        DnsCommandText = DnsPlan.ApplyCommand;
        DnsServerRows = BuildDnsRows(comparison);
        OnPropertyChanged(nameof(CanApplyDns));
        OnPropertyChanged(nameof(HasDnsEvidence));
        OnPropertyChanged(nameof(DnsCurrentText));
    }

    /// <summary>The three resolvers measured on the same host, current first.</summary>
    private static IReadOnlyList<DnsServerRow> BuildDnsRows(DnsComparison comparison)
    {
        if (!comparison.HasEvidence)
        {
            return [];
        }

        var rows = new List<DnsServerRow>();
        if (comparison.Current is { } current)
        {
            rows.Add(new DnsServerRow
            {
                Label = string.IsNullOrWhiteSpace(current.Address) ? "当前 DNS" : $"当前 · {current.Address}",
                TimeText = current.TimeText,
                IsBest = false,
            });
        }

        foreach (var alternative in comparison.Alternatives)
        {
            var isBest = ReferenceEquals(alternative, comparison.Best);
            rows.Add(new DnsServerRow
            {
                Label = $"{alternative.Label} · {alternative.Address}{(isBest ? " · 最快" : string.Empty)}",
                TimeText = alternative.TimeText,
                IsBest = isBest,
            });
        }

        return rows;
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await Shell.History.ClearAsync().ConfigureAwait(true);
        await RefreshHistoryCountAsync().ConfigureAwait(true);
        Shell.Overview.ResetRecentRuns();
        HistoryStatusText = "已清空本机历史";
    }

    [RelayCommand]
    private async Task ApplyDnsAsync()
    {
        if (!DnsPlan.IsWorthChanging)
        {
            DnsStatusText = "当前解析已经够快，无需修改";
            return;
        }

        DnsStatusText = "正在等待管理员确认…";
        var result = await ElevatedCommandRunner.RunAsync(DnsPlan.ApplyCommand).ConfigureAwait(true);

        if (result.Succeeded)
        {
            await _dnsBackups.SaveAsync(new DnsBackup
            {
                AdapterName = DnsPlan.AdapterName,
                OriginalDns = DnsPlan.CurrentDns,
                RestoreCommand = DnsPlan.RestoreCommand,
                AppliedAt = DateTimeOffset.Now,
            }).ConfigureAwait(true);

            await RefreshDnsBackupAsync().ConfigureAwait(true);
            DnsStatusText = $"已切换到 {DnsPlan.ProposedDns}，可用「还原」恢复";
        }
        else
        {
            DnsStatusText = result.Message;
        }
    }

    [RelayCommand]
    private async Task RestoreDnsAsync()
    {
        var backup = await _dnsBackups.LoadAsync().ConfigureAwait(true);
        if (backup is null)
        {
            DnsStatusText = "没有可还原的记录";
            return;
        }

        DnsStatusText = "正在等待管理员确认…";
        var result = await ElevatedCommandRunner.RunAsync(backup.RestoreCommand).ConfigureAwait(true);

        if (result.Succeeded)
        {
            _dnsBackups.Clear();
            await RefreshDnsBackupAsync().ConfigureAwait(true);
            DnsStatusText = "已还原为原有 DNS 设置";
        }
        else
        {
            DnsStatusText = result.Message;
        }
    }

    private async Task RefreshDnsBackupAsync()
    {
        var backup = await _dnsBackups.LoadAsync().ConfigureAwait(true);
        HasDnsBackup = backup is not null;
        DnsBackupText = backup is null
            ? string.Empty
            : $"{backup.AppliedAt.ToLocalTime():MM-dd HH:mm} 修改过 {backup.AdapterName}，原 DNS {backup.OriginalDns}";
    }
}

/// <summary>One row of the resolver comparison table.</summary>
public sealed class DnsServerRow
{
    public string Label { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public bool IsBest { get; init; }
}

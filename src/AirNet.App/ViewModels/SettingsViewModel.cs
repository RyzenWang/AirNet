using AirNet.Core.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AirNet.Core.Probes;
using AirNet.Core.Services;
using AirNet.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>
/// Preferences, plus the only two places AirNet can change the machine: the history file and
/// (behind an explicit confirmation) the adapter's DNS servers.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private static string T(string key) => LocalizationService.Current.T(key);
    private static string Fmt(string key, params object[] args) => LocalizationService.Current.Format(key, args);

    private readonly DnsBackupStore _dnsBackups = new();
    private readonly DnsOperationLog _dnsOperations = new();

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
    private string _historyCountText = T("L.Reading");

    [ObservableProperty]
    private string _historyStatusText = string.Empty;
    /// <summary>Result of the last diagnostic bundle export, shown under the buttons.</summary>
    [ObservableProperty]
    private string _diagnosticsStatusText = string.Empty;
    /// <summary>Optional note the user wants included in the bundle header.</summary>
    public string? DiagnosticsNote { get; set; }

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

    [ObservableProperty]
    private string _dnsOperationText = string.Empty;

    [ObservableProperty]
    private bool _hasDnsOperation;

    [ObservableProperty]
    private bool _isNeighborScanning;

    [ObservableProperty]
    private string _neighborStatusText = T("L.NeighborHint");

    [ObservableProperty]
    private string _neighborAdvice = T("L.NeighborAdviceDefault");

    [ObservableProperty]
    private IReadOnlyList<WifiNeighborRow> _neighborRows = [];

    public bool HasNeighborRows => NeighborRows.Count > 0;

    public string VersionText => $"{AppVersion.Current} · M3";

    /// <summary>
    /// The Store channel versions packages on its own line (1.0.0.0, 1.1.0.0, …) while the
    /// source keeps its semantic version. Showing both is the only way a bug report can be
    /// traced back to a commit.
    /// </summary>
    public string PackageText => PackagedApp.PackageVersion is { } version
        ? Fmt("L.PackageFormat", version, AppVersion.Current)
        : string.Empty;

    public bool HasPackageNote => PackageText.Length > 0;
    public string RuntimeText => ".NET 10 · Avalonia 11.3 · Windows / macOS / Linux";
    public string FootprintText => T("L.FootprintText");

    /// <summary>
    /// Says "experimental" on the platforms nobody has run on real hardware yet. Hidden on the
    /// two that were verified, because a permanent caveat everywhere stops being read.
    /// </summary>
    public string PlatformNote => OperatingSystem.IsMacOS()
        ? T("L.ExperimentalMac")
        : OperatingSystem.IsLinux()
          && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
             == System.Runtime.InteropServices.Architecture.Arm64
            ? T("L.ExperimentalLinuxArm")
            : string.Empty;

    public bool HasPlatformNote => PlatformNote.Length > 0;
    public string PrivacyText => T("L.PrivacyText");

    public bool IsChineseLanguage => LocalizationService.Current.IsChinese;
    public bool IsEnglishLanguage => LocalizationService.Current.IsEnglish;

    [RelayCommand]
    private void SetLanguage(string? language)
    {
        LocalizationService.Current.Apply(language);
        Shell.Preferences.Language = LocalizationService.Current.Language;
        Shell.SavePreferences();
        OnPropertyChanged(nameof(IsChineseLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
    }

    public bool CanApplyDns => DnsPlan.IsWorthChanging;
    public bool HasDnsEvidence => DnsServerRows.Count > 0;

    public string DnsCurrentText => Fmt("L.CurrentDns", DnsPlan.CurrentDns);
    public string DnsHeadlineText => LocalizationService.Current.IsChinese
        ? DnsPlan.Headline
        : DnsPlan.IsWorthChanging ? T("L.DnsSlowHeadline") : T("L.DnsFastHeadline");
    public string DnsRationaleText => LocalizationService.Current.IsChinese
        ? DnsPlan.Rationale
        : DnsPlan.IsWorthChanging ? T("L.DnsSlowRationale") : T("L.DnsFastRationale");

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
        await RefreshDnsOperationAsync().ConfigureAwait(true);
    }

    public async Task RefreshHistoryCountAsync()
    {
        var count = await Shell.History.CountAsync().ConfigureAwait(true);
        HistoryCountText = count == 0 ? T("L.NoRecords") : Fmt("L.RecordsFormat", count);
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
        OnPropertyChanged(nameof(DnsHeadlineText));
        OnPropertyChanged(nameof(DnsRationaleText));
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
                Label = string.IsNullOrWhiteSpace(current.Address) ? T("L.CurrentDnsLabel") : $"{T("L.CurrentDnsLabel")} · {current.Address}",
                TimeText = current.TimeText,
                IsBest = false,
            });
        }

        foreach (var alternative in comparison.Alternatives)
        {
            var isBest = ReferenceEquals(alternative, comparison.Best);
            rows.Add(new DnsServerRow
            {
                Label = $"{(alternative.Label == "阿里 DNS" ? T("L.AliDns") : alternative.Label)} · {alternative.Address}{(isBest ? T("L.FastestSuffix") : string.Empty)}",
                TimeText = alternative.TimeText,
                IsBest = isBest,
            });
        }

        return rows;
    }

    partial void OnNeighborRowsChanged(IReadOnlyList<WifiNeighborRow> value) =>
        OnPropertyChanged(nameof(HasNeighborRows));

    [RelayCommand]
    private async Task ScanNeighborsAsync()
    {
        if (IsNeighborScanning)
        {
            return;
        }

        if (OperatingSystem.IsWindows() && !InterfaceInspector.Inspect().IsWireless)
        {
            NeighborRows = [];
            NeighborStatusText = T("L.WiredNoScan");
            NeighborAdvice = T("L.WiredAdvice");
            return;
        }

        IsNeighborScanning = true;
        NeighborStatusText = T("L.ScanningWlan");
        try
        {
            var scan = await WlanNeighborProbe.ScanAsync().ConfigureAwait(true);
            NeighborRows = scan.Neighbors
                .Take(8)
                .Select(item => new WifiNeighborRow
                {
                    Ssid = item.Ssid,
                    Bssid = item.Bssid,
                    SignalText = item.SignalText,
                    Phy = item.Phy,
                    RateText = item.RateText,
                    IsStrong = item.SignalQuality >= 70,
                    IsWeak = item.SignalQuality < 45,
                })
                .ToList();

            NeighborStatusText = !scan.IsSupported
                ? scan.Note ?? T("L.NoNeighborSupport")
                : scan.HasNeighbors
                    ? Fmt("L.FoundNeighbors", scan.Neighbors.Count)
                    : scan.Note ?? T("L.NoVisibleNeighbors");
            NeighborAdvice = BuildNeighborAdvice(scan);
        }
        catch (OperationCanceledException)
        {
            NeighborStatusText = T("L.ScanCancelled");
        }
        catch (Exception ex)
        {
            NeighborStatusText = Fmt("L.ScanFailed", ex.Message);
        }
        finally
        {
            IsNeighborScanning = false;
        }
    }

    private static string BuildNeighborAdvice(WifiNeighborScan scan)
    {
        if (!scan.IsSupported)
        {
            return T("L.NeighborUnsupportedAdvice");
        }

        if (!scan.HasNeighbors)
        {
            return T("L.NoNeighborAdvice");
        }

        var best = scan.Neighbors.MaxBy(item => item.SignalQuality)!;
        var sameSsid = scan.Neighbors.Count(item => string.Equals(item.Ssid, best.Ssid, StringComparison.OrdinalIgnoreCase));
        var advice = Fmt("L.StrongestNeighbor", best.Ssid, best.SignalQuality, best.Phy);
        if (sameSsid > 1)
        {
            advice += Fmt("L.SameSsid", sameSsid);
        }

        advice += best.SignalQuality < 50 ? T("L.WeakSignalAdvice") : T("L.GoodSignalAdvice");
        return advice;
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await Shell.History.ClearAsync().ConfigureAwait(true);
        await RefreshHistoryCountAsync().ConfigureAwait(true);
        Shell.Overview.ResetRecentRuns();
        await Shell.Trends.LoadAsync().ConfigureAwait(true);
        HistoryStatusText = T("L.ClearHistoryDone");
    }

    /// <summary>
    /// Writes the diagnostic bundle the "导出诊断包" button names: settings, history, crash log
    /// and a header saying which build produced it. Without it a bug report is just "打不开".
    /// </summary>
    public async Task ExportDiagnosticsAsync(TopLevel top)
    {

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("L.DiagnosticsTitle"),
            SuggestedFileName = $"airnet-diag-{DateTime.Now:yyyyMMdd-HHmm}",
            DefaultExtension = "zip",
            FileTypeChoices =
            [
                new FilePickerFileType(T("L.DiagnosticsFilter")) { Patterns = ["*.zip"] },
            ],
        });

        if (file?.Path is not { } uri)
        {
            return;
        }

        try
        {
            var summary = DiagnosticBundle.BuildSummary(AppVersion.Current, Shell.Snapshot, DiagnosticsNote);
            var collected = DiagnosticBundle.Write(uri.LocalPath, AppPaths.DataDirectory(), summary);

            DiagnosticsStatusText = collected == 0
                ? Fmt("L.DiagnosticsNoData", Path.GetFileName(uri.LocalPath))
                : Fmt("L.DiagnosticsWritten", Path.GetFileName(uri.LocalPath), collected);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticsStatusText = Fmt("L.DiagnosticsFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ApplyDnsAsync()
    {
        if (!DnsPlan.IsWorthChanging)
        {
            DnsStatusText = T("L.DnsAlreadyFast");
            return;
        }

        DnsStatusText = T("L.WaitingAdmin");
        var result = await ElevatedCommandRunner.RunAsync(DnsPlan.ApplyCommand).ConfigureAwait(true);
        await LogDnsOperationAsync(
            "apply",
            DnsPlan.AdapterName,
            DnsPlan.CurrentDns,
            DnsPlan.ProposedDns,
            DnsPlan.ApplyCommand,
            result).ConfigureAwait(true);

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
            DnsStatusText = Fmt("L.DnsApplied", DnsPlan.ProposedDns);
        }
        else
        {
            DnsStatusText = result.Message;
        }

        await RefreshDnsOperationAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void PreviewDns()
    {
        DnsStatusText = DnsPlan.IsWorthChanging
            ? Fmt("L.DnsPreview", DnsPlan.ApplyCommand)
            : T("L.DnsAlreadyFast");
    }

    [RelayCommand]
    private async Task RestoreDnsAsync()
    {
        var backup = await _dnsBackups.LoadAsync().ConfigureAwait(true);
        if (backup is null)
        {
            DnsStatusText = T("L.NoBackup");
            return;
        }

        DnsStatusText = T("L.WaitingAdmin");
        var result = await ElevatedCommandRunner.RunAsync(backup.RestoreCommand).ConfigureAwait(true);
        await LogDnsOperationAsync(
            "restore",
            backup.AdapterName,
            "changed",
            backup.OriginalDns,
            backup.RestoreCommand,
            result).ConfigureAwait(true);

        if (result.Succeeded)
        {
            _dnsBackups.Clear();
            await RefreshDnsBackupAsync().ConfigureAwait(true);
            DnsStatusText = T("L.DnsRestored");
        }
        else
        {
            DnsStatusText = result.Message;
        }

        await RefreshDnsOperationAsync().ConfigureAwait(true);
    }

    private async Task LogDnsOperationAsync(
        string action,
        string adapter,
        string from,
        string to,
        string command,
        CommandResult result)
    {
        try
        {
            await _dnsOperations.AppendAsync(new DnsOperationEntry
            {
                At = DateTimeOffset.Now,
                Action = action,
                Adapter = adapter,
                From = from,
                To = to,
                Command = command,
                Success = result.Succeeded,
                Message = result.Message,
            }).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The audit trail is best effort; never fail the actual DNS change because of it.
        }
    }

    private async Task RefreshDnsOperationAsync()
    {
        var entry = (await _dnsOperations.LoadAsync(1).ConfigureAwait(true)).FirstOrDefault();
        HasDnsOperation = entry is not null;
        DnsOperationText = entry is null
            ? string.Empty
            : Fmt("L.DnsOperation", $"{entry.At.ToLocalTime():MM-dd HH:mm}", entry.Action,
                entry.Success ? "OK" : "FAILED", entry.Adapter);
    }

    private async Task RefreshDnsBackupAsync()
    {
        var backup = await _dnsBackups.LoadAsync().ConfigureAwait(true);
        HasDnsBackup = backup is not null;
        DnsBackupText = backup is null
            ? string.Empty
            : Fmt("L.DnsBackup", $"{backup.AppliedAt.ToLocalTime():MM-dd HH:mm}",
                backup.AdapterName, backup.OriginalDns);
    }
}

/// <summary>One row of the resolver comparison table.</summary>
public sealed class DnsServerRow
{
    public string Label { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public bool IsBest { get; init; }
}

public sealed class WifiNeighborRow
{
    public string Ssid { get; init; } = string.Empty;
    public string Bssid { get; init; } = string.Empty;
    public string SignalText { get; init; } = string.Empty;
    public string Phy { get; init; } = string.Empty;
    public string RateText { get; init; } = string.Empty;
    public bool IsStrong { get; init; }
    public bool IsWeak { get; init; }
}

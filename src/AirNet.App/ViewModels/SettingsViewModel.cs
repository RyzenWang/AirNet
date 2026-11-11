using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>Preferences. Theme, scan options, and the local history controls.</summary>
public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _ = RefreshHistoryCountAsync();
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private bool _autoRefresh = true;

    [ObservableProperty]
    private bool _includeSpeed = true;

    [ObservableProperty]
    private bool _dualProbe = true;

    [ObservableProperty]
    private bool _keepHistory = true;

    [ObservableProperty]
    private string _historyCountText = "读取中…";

    [ObservableProperty]
    private string _historyStatusText = string.Empty;

    public string VersionText => "0.1.0 · M1";
    public string RuntimeText => ".NET 10 · Avalonia 11.3 · Windows / macOS / Linux";
    public string FootprintText => "冷启动 0.9 s · 内存 152 MB · 40 MB";
    public string PrivacyText => "所有测量在本机完成，不上传任何字段。历史记录仅保存在本地。";

    public async Task RefreshHistoryCountAsync()
    {
        var count = await Shell.History.CountAsync().ConfigureAwait(true);
        HistoryCountText = count == 0 ? "暂无记录" : $"{count} 条记录 · 保留 30 天";
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await Shell.History.ClearAsync().ConfigureAwait(true);
        await RefreshHistoryCountAsync().ConfigureAwait(true);
        Shell.Overview.ResetRecentRuns();
        HistoryStatusText = "已清空本机历史";
    }
}

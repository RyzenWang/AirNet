using CommunityToolkit.Mvvm.ComponentModel;

namespace AirNet.App.ViewModels;

/// <summary>Preferences. The theme section is the only one wired in the visual prototype.</summary>
public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(ShellViewModel shell)
    {
        Shell = shell;
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

    public string VersionText => "0.1.0 · 视觉原型";
    public string RuntimeText => ".NET 10 · Avalonia 11.3 · Windows / macOS / Linux";
    public string FootprintText => "冷启动 1.4 s · 内存 154 MB（未裁剪）";
    public string PrivacyText => "所有测量在本机完成，不上传任何字段。历史记录仅保存在本地。";
}

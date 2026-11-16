using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace AirNet.App.Localization;

/// <summary>
/// Runtime UI localization. Static XAML text uses <c>{DynamicResource L.Key}</c>; text built in
/// a ViewModel goes through <see cref="T"/> / <see cref="Format"/>. A language change replaces
/// one merged resource dictionary, so DynamicResource consumers update without rebuilding views.
/// </summary>
public sealed class LocalizationService
{
    public const string English = "en";
    public const string ChineseSimplified = "zh-CN";

    public static LocalizationService Current { get; } = new();

    private static readonly IReadOnlyDictionary<string, (string En, string Zh)> Catalog =
        new Dictionary<string, (string En, string Zh)>(StringComparer.Ordinal)
        {
            ["L.Workspace"] = ("WORKSPACE", "WORKSPACE"),
            ["L.Overview"] = ("Overview", "总览"),
            ["L.Monitor"] = ("Live monitor", "实时检测"),
            ["L.Speed"] = ("Speed test", "带宽测速"),
            ["L.Sites"] = ("Site latency", "网站延迟"),
            ["L.Path"] = ("Path", "链路路径"),
            ["L.History"] = ("History", "历史趋势"),
            ["L.Settings"] = ("Settings", "设置"),
            ["L.SettingsNote"] = ("Changes apply immediately and stay on this machine", "所有选项即时生效，配置保存在本机"),
            ["L.StartScan"] = ("Start scan", "开始检测"),
            ["L.Stop"] = ("Stop", "停止"),
            ["L.AutoRefresh"] = ("Auto refresh", "自动刷新"),
            ["L.ExportReport"] = ("Export report", "导出报告"),
            ["L.LocalLink"] = ("LOCAL LINK", "本机链路"),
            ["L.Lan"] = ("LAN", "局域网"),
            ["L.Gateway"] = ("Gateway", "网关"),
            ["L.Dns"] = ("DNS", "DNS"),
            ["L.Egress"] = ("Egress", "出口"),
            ["L.LightweightMode"] = ("Lightweight mode", "轻量模式"),
            ["L.LightweightNote"] = ("No daemon · no driver · online only while scanning", "无常驻 · 无驱动 · 仅在检测时联网"),
            ["L.Language"] = ("Language", "语言"),
            ["L.Chinese"] = ("中文", "中文"),
            ["L.English"] = ("English", "English"),
            ["L.Theme"] = ("Appearance", "外观"),
            ["L.FollowSystem"] = ("System", "跟随系统"),
            ["L.Light"] = ("Light", "浅色"),
            ["L.Dark"] = ("Dark", "深色"),
            ["L.About"] = ("About", "关于"),
            ["L.AppearanceNote"] = ("Warm paper palette; changes apply immediately.", "暖白纸面色板；修改即时生效。"),
            ["L.HeroTitle1"] = ("Is this network good,", "网络好不好，"),
            ["L.HeroTitle2"] = ("and is it fast enough?", "带宽够不够。"),
            ["L.HeroSubtitle"] = ("Measure latency, jitter, loss and bandwidth layer by layer, then name the likely culprit: router, ISP or far end.", "逐层测量延迟、抖动、丢包与带宽，用一句话说清问题出在路由器、运营商还是对端。"),
            ["L.ShareCard"] = ("Share card", "分享卡片"),
            ["L.NoDriverNote"] = ("No driver · no resident process · bandwidth is included", "无驱动 · 无常驻进程 · 检测含上下行测速"),
            ["L.RecentScans"] = ("RECENT SCANS", "最近检测"),
            ["L.OneScan"] = ("One scan, readable at a glance.", "一次检测，读懂网络。"),
            ["L.Latency"] = ("Latency", "延迟"),
            ["L.Loss"] = ("Loss", "丢包"),
            ["L.Bandwidth"] = ("Bandwidth", "带宽"),
        };

    private ResourceDictionary? _dictionary;

    public string Language { get; private set; } = ChineseSimplified;

    public bool IsChinese => string.Equals(Language, ChineseSimplified, StringComparison.OrdinalIgnoreCase);

    public bool IsEnglish => !IsChinese;

    public string LanguageShortLabel => IsChinese ? "中" : "EN";

    public CultureInfo Culture => IsChinese
        ? CultureInfo.GetCultureInfo("zh-CN")
        : CultureInfo.GetCultureInfo("en-US");

    public event EventHandler? LanguageChanged;

    public void Apply(string? language)
    {
        var normalized = string.Equals(language, ChineseSimplified, StringComparison.OrdinalIgnoreCase)
                         || (language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ?? false)
            ? ChineseSimplified
            : English;

        var changed = !string.Equals(Language, normalized, StringComparison.Ordinal) || _dictionary is null;
        Language = normalized;

        if (Application.Current is { } application)
        {
            var dictionary = new ResourceDictionary();
            foreach (var pair in Catalog)
            {
                dictionary[pair.Key] = IsChinese ? pair.Value.Zh : pair.Value.En;
            }

            if (_dictionary is not null)
            {
                application.Resources.MergedDictionaries.Remove(_dictionary);
            }

            application.Resources.MergedDictionaries.Add(dictionary);
            _dictionary = dictionary;
        }

        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string T(string key) =>
        Catalog.TryGetValue(key, out var value)
            ? IsChinese ? value.Zh : value.En
            : key;

    public string Format(string key, params object[] args) =>
        string.Format(Culture, T(key), args);

    public void Dispose()
    {
        if (_dictionary is not null && Application.Current is { } application)
        {
            application.Resources.MergedDictionaries.Remove(_dictionary);
        }

        _dictionary = null;
    }
}

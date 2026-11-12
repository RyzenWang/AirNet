namespace AirNet.Core.Sites;

public enum SiteGroup
{
    Domestic,
    International,
    Custom,
}

/// <summary>One website measured by the site-latency page. Custom entries live in sites.json.</summary>
public sealed record SiteTarget(
    string Id,
    string Name,
    string Host,
    SiteGroup Group,
    bool IsCustom = false,
    DateTimeOffset? AddedAt = null)
{
    public string GroupLabel => Group switch
    {
        SiteGroup.Domestic => "国内",
        SiteGroup.International => "国际",
        _ => "自定义",
    };
}

/// <summary>The fixed default catalog. Custom sites are appended by <see cref="WithCustom"/>.</summary>
public static class SiteCatalog
{
    public static IReadOnlyList<SiteTarget> Default { get; } =
    [
        new("baidu", "百度", "www.baidu.com", SiteGroup.Domestic),
        new("tencent", "腾讯", "www.qq.com", SiteGroup.Domestic),
        new("douyin", "抖音", "www.douyin.com", SiteGroup.Domestic),
        new("xiaohongshu", "小红书", "www.xiaohongshu.com", SiteGroup.Domestic),
        new("bilibili", "哔哩哔哩", "www.bilibili.com", SiteGroup.Domestic),
        new("zhihu", "知乎", "www.zhihu.com", SiteGroup.Domestic),
        new("weibo", "微博", "www.weibo.com", SiteGroup.Domestic),
        new("github", "GitHub", "github.com", SiteGroup.International),
        new("google", "Google", "www.google.com", SiteGroup.International),
        new("youtube", "YouTube", "www.youtube.com", SiteGroup.International),
        new("x", "推特 / X", "x.com", SiteGroup.International),
    ];

    public static IReadOnlyList<SiteTarget> WithCustom(IEnumerable<SiteTarget> custom) =>
        [.. Default, .. custom.Where(site => site.IsCustom)];
}

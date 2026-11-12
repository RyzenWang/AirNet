namespace AirNet.Core.Sites;

/// <summary>Validation and host normalisation for user-entered sites. Kept pure so it is testable.</summary>
public static class CustomSiteRules
{
    public const int MaxCustomSites = 8;
    public const int MaxNameLength = 24;

    public static string? ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return "名称不能为空";
        }

        if (trimmed.Length > MaxNameLength)
        {
            return $"名称不能超过 {MaxNameLength} 个字符";
        }

        return trimmed.Any(char.IsControl) ? "名称不能包含控制字符" : null;
    }

    public static bool TryNormalizeHost(string? input, out string host, out string error)
    {
        host = string.Empty;
        error = string.Empty;

        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            error = "地址不能为空";
            return false;
        }

        if (text.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                error = "地址格式不正确";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error = "只支持 HTTPS";
                return false;
            }

            if (!uri.IsDefaultPort && uri.Port != 443)
            {
                error = "只支持 443 端口";
                return false;
            }

            host = uri.Host;
        }
        else
        {
            // "example.com/path" and "example.com:443" are both accepted; the probe only uses the host.
            var candidate = text.Split('/', '?', '#')[0];
            var colon = candidate.LastIndexOf(':');
            if (colon > 0)
            {
                if (candidate[(colon + 1)..] != "443")
                {
                    error = "只支持 443 端口";
                    return false;
                }

                candidate = candidate[..colon];
            }

            host = candidate;
        }

        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0)
        {
            error = "地址不能为空";
            return false;
        }

        if (host.Any(char.IsWhiteSpace) || host.Contains('/') || host.Contains('\\'))
        {
            error = "地址格式不正确";
            return false;
        }

        if (Uri.CheckHostName(host) != UriHostNameType.Dns)
        {
            error = "请填写域名（不支持 IP 或非法主机名）";
            return false;
        }

        return true;
    }

    public static string? ValidateDuplicate(string host, IEnumerable<SiteTarget> existing)
    {
        return existing.Any(site => string.Equals(site.Host, host, StringComparison.OrdinalIgnoreCase))
            ? "这个域名已经在站点清单里了"
            : null;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirNet.Core.Sites;

public sealed class CustomSiteRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
}

public sealed class CustomSiteDocument
{
    public List<CustomSiteRecord> Sites { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CustomSiteDocument))]
internal sealed partial class CustomSiteJsonContext : JsonSerializerContext;

/// <summary>
/// Loads and saves user-added sites in sites.json. A corrupt file is ignored rather than blocking
/// the fixed default catalog; callers can always fall back to <see cref="SiteCatalog.Default"/>.
/// </summary>
public sealed class CustomSiteStore
{
    private readonly string _filePath;

    public CustomSiteStore(string? directory = null)
    {
        var root = directory
                   ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
                   ?? Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                       "AirNet");
        _filePath = Path.Combine(root, "sites.json");
    }

    public string FilePath => _filePath;

    public IReadOnlyList<SiteTarget> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var json = File.ReadAllText(_filePath);
            var document = JsonSerializer.Deserialize(json, CustomSiteJsonContext.Default.CustomSiteDocument);
            if (document?.Sites is null)
            {
                return [];
            }

            var sites = new List<SiteTarget>();
            foreach (var record in document.Sites.Take(CustomSiteRules.MaxCustomSites))
            {
                if (string.IsNullOrWhiteSpace(record.Name)
                    || !CustomSiteRules.TryNormalizeHost(record.Host, out var host, out _))
                {
                    continue;
                }

                var id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id;
                sites.Add(new SiteTarget(
                    id,
                    record.Name.Trim(),
                    host,
                    SiteGroup.Custom,
                    IsCustom: true,
                    AddedAt: record.AddedAt == default ? DateTimeOffset.Now : record.AddedAt));
            }

            return sites;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<SiteTarget> customSites)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new CustomSiteDocument
            {
                Sites = customSites
                    .Where(site => site.IsCustom)
                    .Take(CustomSiteRules.MaxCustomSites)
                    .Select(site => new CustomSiteRecord
                    {
                        Id = site.Id,
                        Name = site.Name,
                        Host = site.Host,
                        AddedAt = site.AddedAt ?? DateTimeOffset.Now,
                    })
                    .ToList(),
            };

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(document, CustomSiteJsonContext.Default.CustomSiteDocument));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A preference write failure must not take down the page.
        }
    }
}

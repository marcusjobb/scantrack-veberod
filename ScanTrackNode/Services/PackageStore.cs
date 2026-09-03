using System.Text.Json;
using ScanTrackNode.Models;

namespace ScanTrackNode.Services;

// Singleton som håller mottagna paket och sparar dem till disk.
// Monteras /data som Azure File Share för att överleva omstarter.
public class PackageStore
{
    private readonly string _path;
    private readonly List<Package> _packages;
    private readonly ILogger<PackageStore> _logger;

    public PackageStore(IConfiguration config, ILogger<PackageStore> logger)
    {
        _logger = logger;
        _path = config["STATE_FILE"] ?? "/data/packages.json";
        _packages = Load(_path, logger);
        _logger.LogInformation("PackageStore: {Count} paket laddade från {Path}", _packages.Count, _path);
    }

    public IReadOnlyList<Package> All => _packages;

    public void Add(Package package)
    {
        _packages.Add(package);
        Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_packages));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kunde inte spara pakethistorik till {Path}", _path);
        }
    }

    private static List<Package> Load(string path, ILogger logger)
    {
        try
        {
            if (!File.Exists(path)) return [];
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<Package>>(json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kunde inte läsa pakethistorik från {Path} — börjar tom", path);
            return [];
        }
    }
}

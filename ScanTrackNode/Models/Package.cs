namespace ScanTrackNode.Models;

public class Package
{
    public string PackageId { get; set; } = GenerateId();
    public string Destination { get; set; } = "";
    public List<string> History { get; set; } = new();
    public string Payload { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    private static string GenerateId() =>
        "PKG-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
}

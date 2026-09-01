using System.Text;
using System.Text.Json;

namespace ScanTrackNode.Services;

public class NodeRegistry
{
    private readonly IHttpClientFactory _factory;
    private readonly string _registryUrl;
    private readonly string _cityName;
    private readonly string _nodeUrl;
    private readonly ILogger<NodeRegistry> _logger;

    public NodeRegistry(IHttpClientFactory factory, IConfiguration config, ILogger<NodeRegistry> logger)
    {
        _factory = factory;
        _registryUrl = config["REGISTRY_URL"]
            ?? throw new InvalidOperationException("Miljövariabel REGISTRY_URL saknas");
        _cityName = config["CITY_NAME"]
            ?? throw new InvalidOperationException("Miljövariabel CITY_NAME saknas");
        _nodeUrl = config["NODE_URL"]
            ?? throw new InvalidOperationException("Miljövariabel NODE_URL saknas");
        _logger = logger;
    }

    public async Task RegisterSelfAsync()
    {
        var body = JsonSerializer.Serialize(new { city = _cityName, url = _nodeUrl });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            var http = _factory.CreateClient();
            var response = await http.PostAsync($"{_registryUrl}/nodes", content);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Nod registrerad: {City} → {Url}", _cityName, _nodeUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kunde inte registrera noden mot {Registry}", _registryUrl);
        }
    }

    public async Task<Dictionary<string, string>> GetNodesAsync()
    {
        try
        {
            var http = _factory.CreateClient();
            var json = await http.GetStringAsync($"{_registryUrl}/nodes");
            var nodes = JsonSerializer.Deserialize<List<NodeEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return nodes.ToDictionary(n => n.City, n => n.Url, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kunde inte hämta nodlista från {Registry}", _registryUrl);
            return new Dictionary<string, string>();
        }
    }

    private record NodeEntry(string City, string Url);
}

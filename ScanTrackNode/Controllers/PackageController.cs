using Microsoft.AspNetCore.Mvc;
using ScanTrackNode.Graph;
using ScanTrackNode.Models;
using ScanTrackNode.Services;

namespace ScanTrackNode.Controllers;

[ApiController]
[Route("[controller]")]
public class PaketController : ControllerBase
{
    private readonly DijkstraService _dijkstra;
    private readonly PackageForwarder _forwarder;
    private readonly NodeRegistry _registry;
    private readonly IConfiguration _config;
    private readonly ILogger<PaketController> _logger;

    private static readonly List<Package> _received = new();

    public PaketController(
        DijkstraService dijkstra,
        PackageForwarder forwarder,
        NodeRegistry registry,
        IConfiguration config,
        ILogger<PaketController> logger)
    {
        _dijkstra = dijkstra;
        _forwarder = forwarder;
        _registry = registry;
        _config = config;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> TaEmot([FromBody] Package package)
    {
        var cityName = _config["CITY_NAME"] ?? "Okänd";
        package.History.Add(cityName);

        _logger.LogInformation(
            "Paket {Id} anlände till {City}. Historik: {History}. Destination: {Dest}",
            package.PackageId, cityName, string.Join(" → ", package.History), package.Destination);

        if (string.Equals(cityName, package.Destination, StringComparison.OrdinalIgnoreCase))
        {
            _received.Add(package);
            _logger.LogInformation("Paket {Id} levererat till {City}!", package.PackageId, cityName);
            return Ok(new { status = "levererat", stad = cityName, paket = package });
        }

        // Hämta vilka noder som faktiskt är online just nu
        var onlineNodes = await _registry.GetNodesAsync();

        // Dijkstra hittar bara vägar via städer som är registrerade
        var nextHop = _dijkstra.NextHop(cityName, package.Destination, package.History, onlineNodes.Keys);

        if (nextHop == null)
        {
            _logger.LogWarning(
                "Ingen väg från {City} till {Dest} med tillgängliga noder: {Online}",
                cityName, package.Destination, string.Join(", ", onlineNodes.Keys));
            return UnprocessableEntity(new
            {
                fel = "Ingen väg hittades — för få noder online",
                onlineNoder = onlineNodes.Keys,
                historik = package.History
            });
        }

        _logger.LogInformation("Vidarebefordrar {Id}: {City} → {Next}", package.PackageId, cityName, nextHop);

        var lyckades = await _forwarder.ForwardAsync(package, nextHop);

        if (!lyckades)
            return StatusCode(502, new { fel = $"Kunde inte nå {nextHop}" });

        return Ok(new { status = "vidarebefordrat", nästaHopp = nextHop, paket = package });
    }

    [HttpGet]
    public IActionResult Lista() =>
        Ok(new { stad = _config["CITY_NAME"], mottagna = _received });

    [HttpGet("/status")]
    public IActionResult Status() =>
        Ok(new
        {
            stad = _config["CITY_NAME"],
            url = _config["NODE_URL"],
            mottagna = _received.Count,
            uppeSedanUtc = DateTime.UtcNow
        });

    [HttpGet("/route")]
    public async Task<IActionResult> VisaRutt([FromQuery] string from, [FromQuery] string to)
    {
        var onlineNodes = await _registry.GetNodesAsync();
        var route = _dijkstra.FullRoute(from, to, Enumerable.Empty<string>(), onlineNodes.Keys);

        if (route.Count < 2)
            return NotFound(new
            {
                fel = $"Ingen rutt från {from} till {to} med nuvarande noder online",
                onlineNoder = onlineNodes.Keys
            });

        return Ok(new { från = from, till = to, rutt = route, antalStopp = route.Count - 2 });
    }
}

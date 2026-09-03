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
    private readonly PackageStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<PaketController> _logger;

    public PaketController(
        DijkstraService dijkstra,
        PackageForwarder forwarder,
        NodeRegistry registry,
        PackageStore store,
        IConfiguration config,
        ILogger<PaketController> logger)
    {
        _dijkstra = dijkstra;
        _forwarder = forwarder;
        _registry = registry;
        _store = store;
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
            _store.Add(package);
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
        Ok(new { stad = _config["CITY_NAME"], mottagna = _store.All });

    [HttpGet("/status")]
    public IActionResult Status() =>
        Ok(new
        {
            stad = _config["CITY_NAME"],
            url = _config["NODE_URL"],
            mottagna = _store.All.Count,
            uppeSedanUtc = DateTime.UtcNow
        });

    [HttpGet("/route")]
    public async Task<IActionResult> VisaRutt([FromQuery] string from, [FromQuery] string to)
    {
        var onlineNodes = await _registry.GetNodesAsync();
        var route = _dijkstra.FullRouteWithPhantoms(from, to, onlineNodes.Keys);

        if (route.Count < 2)
            return NotFound(new
            {
                fel = $"Ingen rutt från {from} till {to} med nuvarande noder online",
                onlineNoder = onlineNodes.Keys
            });

        // Städer prefixade med * är offline-genomfarter ("förbi X"), inte riktiga hopp.
        var läsbar = route.Select(s => s.StartsWith('*') ? $"förbi {s[1..]}" : s).ToList();
        return Ok(new { från = from, till = to, rutt = route, läsbarRutt = läsbar, antalStopp = route.Count - 2 });
    }
}

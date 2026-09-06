namespace ScanTrackNode.Services;

public class HeartbeatService : BackgroundService
{
    private readonly NodeRegistry _registry;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(NodeRegistry registry, ILogger<HeartbeatService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), ct);
            _logger.LogInformation("Heartbeat: registrerar om mot registret");
            await _registry.RegisterSelfAsync();
        }
    }
}

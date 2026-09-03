namespace ScanTrackNode.Graph;

public class DijkstraService
{
    private readonly Dictionary<string, List<(string City, int Km)>> _graph;

    public DijkstraService(Dictionary<string, List<(string City, int Km)>> graph)
    {
        _graph = graph;
    }

    // Returnerar nästa hopp på kortaste vägen från 'from' till 'to'.
    // Städer i 'visited' hoppas över för att undvika loopar.
    // 'onlineNodes' begränsar vilka städer som får användas som mellansteg
    // — saknas en stad i registret ignoreras den helt enkelt.
    // Returnerar null om ingen väg finns med tillgängliga noder.
    public string? NextHop(
        string from,
        string to,
        IEnumerable<string> visited,
        IEnumerable<string>? onlineNodes = null)
    {
        var skip = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
        var online = onlineNodes != null
            ? new HashSet<string>(onlineNodes, StringComparer.OrdinalIgnoreCase)
            : null;

        // Virtuella nödfallskanter: om en online-stad har 0 online-grannar i grafen
        // kopplas den till alla andra online-städer med ett straff-avstånd.
        // Dessa kanter existerar bara i minnet för detta anrop.
        var graph = _graph;
        if (online != null && online.Count > 1)
        {
            List<string>? isolated = null;
            foreach (var city in online)
            {
                var hasOnlineNeighbor = graph.TryGetValue(city, out var neighbors)
                    && neighbors.Any(n => online.Contains(n.City));
                if (!hasOnlineNeighbor)
                    (isolated ??= []).Add(city);
            }

            if (isolated is { Count: > 0 })
            {
                graph = new Dictionary<string, List<(string City, int Km)>>(
                    _graph, StringComparer.OrdinalIgnoreCase);

                const int penaltyKm = 99_999;
                foreach (var city in isolated)
                {
                    if (!graph.ContainsKey(city))
                        graph[city] = [];
                    foreach (var other in online)
                    {
                        if (string.Equals(city, other, StringComparison.OrdinalIgnoreCase)) continue;
                        graph[city].Add((other, penaltyKm));
                        if (!graph.ContainsKey(other)) graph[other] = [];
                        graph[other].Add((city, penaltyKm));
                    }
                }
            }
        }

        var dist = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, int>();

        foreach (var city in graph.Keys)
            dist[city] = int.MaxValue;

        dist[from] = 0;
        queue.Enqueue(from, 0);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (string.Equals(current, to, StringComparison.OrdinalIgnoreCase))
                break;

            if (!_graph.TryGetValue(current, out var neighbors))
                continue;

            foreach (var (neighbor, weight) in neighbors)
            {
                var isDestination = string.Equals(neighbor, to, StringComparison.OrdinalIgnoreCase);

                // Hoppa över besökta städer
                if (skip.Contains(neighbor) && !isDestination) continue;

                // Hoppa över städer som inte är online (mellansteg måste vara registrerade)
                if (!isDestination && online != null && !online.Contains(neighbor)) continue;

                var newDist = dist[current] + weight;
                if (newDist < dist.GetValueOrDefault(neighbor, int.MaxValue))
                {
                    dist[neighbor] = newDist;
                    prev[neighbor] = current;
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }

        if (!prev.ContainsKey(to))
            return null;

        var node = to;
        while (prev.TryGetValue(node, out var parent) &&
               !string.Equals(parent, from, StringComparison.OrdinalIgnoreCase))
        {
            node = parent;
        }

        return node;
    }

    // Returnerar hela rutten som lista, med hänsyn till online-noder.
    public List<string> FullRoute(
        string from,
        string to,
        IEnumerable<string> visited,
        IEnumerable<string>? onlineNodes = null)
    {
        var route = new List<string> { from };
        var current = from;

        for (var i = 0; i < _graph.Count; i++)
        {
            var next = NextHop(current, to, route, onlineNodes);
            if (next == null) break;
            route.Add(next);
            if (string.Equals(next, to, StringComparison.OrdinalIgnoreCase)) break;
            current = next;
        }

        return route;
    }

    // Som FullRoute, men tillåter offline-städer som genomfartspunkter med straff-avstånd.
    // Offline-städer i resultatet prefixas med "förbi " — de är inte riktiga hopp,
    // bara geografiska markeringar längs den kortaste vägen.
    public List<string> FullRouteWithPhantoms(
        string from,
        string to,
        IEnumerable<string>? onlineNodes = null,
        int offlinePenaltyKm = 5_000)
    {
        var online = onlineNodes != null
            ? new HashSet<string>(onlineNodes, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Bygg en tillfällig graf där offline-städers kanter får straff-avstånd
        var augmented = new Dictionary<string, List<(string City, int Km)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (city, neighbors) in _graph)
        {
            augmented[city] = neighbors
                .Select(n =>
                {
                    var penalty = (!online.Contains(city) || !online.Contains(n.City))
                        ? offlinePenaltyKm
                        : 0;
                    return (n.City, n.Km + penalty);
                })
                .ToList();
        }

        // Dijkstra utan online-filtrering — alla städer i grafen är tillåtna
        var dist = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, int>();

        foreach (var city in augmented.Keys)
            dist[city] = int.MaxValue;

        dist[from] = 0;
        queue.Enqueue(from, 0);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (string.Equals(current, to, StringComparison.OrdinalIgnoreCase)) break;
            if (!augmented.TryGetValue(current, out var neighbors)) continue;

            foreach (var (neighbor, weight) in neighbors)
            {
                var newDist = dist[current] + weight;
                if (newDist < dist.GetValueOrDefault(neighbor, int.MaxValue))
                {
                    dist[neighbor] = newDist;
                    prev[neighbor] = current;
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }

        if (!prev.ContainsKey(to)) return [from];

        // Bygg rutten bakifrån.
        // Offline-städer prefixas med * — tolkas som "förbi X" i logg och frontend.
        var path = new List<string>();
        var node = to;
        while (node != null)
        {
            var label = online.Contains(node) ? node : $"*{node}";
            path.Add(label);
            prev.TryGetValue(node, out node!);
        }

        path.Reverse();
        return path;
    }
}

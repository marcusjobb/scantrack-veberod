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

        var dist = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, int>();

        foreach (var city in _graph.Keys)
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
}

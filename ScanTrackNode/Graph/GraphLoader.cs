namespace ScanTrackNode.Graph;

public static class GraphLoader
{
    // Läser cities.csv och bygger en dubbelriktad viktad graf.
    // Format: from,to,km (rubrikrad ignoreras)
    public static Dictionary<string, List<(string City, int Km)>> Load(string csvPath)
    {
        var graph = new Dictionary<string, List<(string, int)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(csvPath).Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            var from = parts[0].Trim();
            var to = parts[1].Trim();

            if (!int.TryParse(parts[2].Trim(), out var km)) continue;

            // Dubbelriktad: Göteborg→Malmö och Malmö→Göteborg
            graph.TryAdd(from, new List<(string, int)>());
            graph.TryAdd(to, new List<(string, int)>());

            graph[from].Add((to, km));
            graph[to].Add((from, km));
        }

        return graph;
    }
}

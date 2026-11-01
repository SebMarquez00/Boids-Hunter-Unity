using System.Collections.Generic;
using UnityEngine;

public static class NavigationPathfinding
{
    // Conserva frontier, cameFrom y costSoFar del ejemplo de A* de clase.
    public static List<int> FindPath(NavigationGraph graph, int start, int goal, NavigationAccess access)
    {
        if (graph == null || graph.Nodes == null) return new List<int>();
        IReadOnlyList<NavigationNode> nodes = graph.Nodes;
        if (!IsValidIndex(nodes, start) || !IsValidIndex(nodes, goal)) return new List<int>();
        if (!graph.CanUseNode(start, access) || !graph.CanUseNode(goal, access)) return new List<int>();

        NavigationPriorityQueue<int> frontier = new NavigationPriorityQueue<int>();
        Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        Dictionary<int, float> costSoFar = new Dictionary<int, float>();
        HashSet<int> closed = new HashSet<int>();
        frontier.Enqueue(start, 0f);
        cameFrom[start] = -1;
        costSoFar[start] = 0f;

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            if (!closed.Add(current)) continue;
            if (current == goal) return ReconstructPath(cameFrom, goal);

            IReadOnlyList<int> neighbors = nodes[current].Neighbors;
            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighbor = neighbors[i];
                if (!IsValidIndex(nodes, neighbor) || closed.Contains(neighbor)) continue;
                if (!graph.CanUseNode(neighbor, access) || !graph.CanUseConnection(current, neighbor, access)) continue;

                float newCost = costSoFar[current] + Vector3.Distance(nodes[current].Position, nodes[neighbor].Position);
                if (costSoFar.TryGetValue(neighbor, out float previousCost) && newCost >= previousCost) continue;

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                float heuristic = Vector3.Distance(nodes[neighbor].Position, nodes[goal].Position);
                frontier.Enqueue(neighbor, newCost + heuristic);
            }
        }

        return new List<int>();
    }

    private static bool IsValidIndex(IReadOnlyList<NavigationNode> nodes, int index)
    {
        return index >= 0 && index < nodes.Count && nodes[index] != null;
    }

    private static List<int> ReconstructPath(Dictionary<int, int> cameFrom, int goal)
    {
        List<int> path = new List<int>();
        for (int current = goal; current != -1; current = cameFrom[current]) path.Add(current);
        path.Reverse();
        return path;
    }
}

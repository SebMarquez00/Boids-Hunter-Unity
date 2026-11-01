using System.Collections.Generic;
using UnityEngine;

public class NavigationGraph : MonoBehaviour
{
    [SerializeField] private ScenarioReferences _scenario;
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private Vector2 _origin = new Vector2(-31f, -20f);
    [SerializeField] private Vector2 _size = new Vector2(62f, 40f);
    [SerializeField, Min(0.01f)] private float _agentRadius = 0.4f;
    [SerializeField] private float _samplingHeight = 0.75f;
    [SerializeField, Min(0.1f)] private float _connectionDistance = 1.5f;
    [SerializeField] private bool _showGraph;
    [SerializeField, HideInInspector] private List<NavigationNode> _nodes = new List<NavigationNode>();

    public IReadOnlyList<NavigationNode> Nodes => _nodes;
    public float AgentRadius => _agentRadius;
    public float SamplingHeight => _samplingHeight;
    public LayerMask ObstacleMask => _obstacleMask;

    // El generador de Editor guarda los nodos y sus conexiones en la escena.
    public void ReplaceNodes(List<NavigationNode> nodes)
    {
        _nodes = nodes ?? new List<NavigationNode>();
    }

    public bool CanUseNode(int index, NavigationAccess access)
    {
        return index >= 0 && index < _nodes.Count && _nodes[index] != null
            && (access == NavigationAccess.Hunter || !_nodes[index].IsSafeZone)
            && IsPhysicalSegmentClear(_nodes[index].Position, _nodes[index].Position);
    }

    public bool CanUseConnection(int from, int to, NavigationAccess access)
    {
        if (!CanUseNode(from, access) || !CanUseNode(to, access)) return false;
        bool connected = false;
        foreach (int neighbor in _nodes[from].Neighbors)
            if (neighbor == to) { connected = true; break; }
        return connected && (access == NavigationAccess.Hunter
            || !TouchesSafeZone(_nodes[from].Position, _nodes[to].Position))
            && IsPhysicalSegmentClear(_nodes[from].Position, _nodes[to].Position);
    }

    public bool TryFindPath(Vector3 from, Vector3 destination, NavigationAccess access,
        out List<Vector3> path, out float length)
    {
        path = new List<Vector3>();
        length = 0f;
        from = AtSamplingHeight(from);
        destination = AtSamplingHeight(destination);
        if (!CanTraverse(from, from, access) || !CanTraverse(destination, destination, access)) return false;
        int start = FindClosestNode(from, access);
        int goal = FindClosestNode(destination, access);
        if (start < 0 || goal < 0) return false;
        if ((from - destination).sqrMagnitude < 0.000001f)
        {
            path.Add(destination);
            return true;
        }

        List<int> nodePath = NavigationPathfinding.FindPath(this, start, goal, access);
        if (nodePath.Count == 0) return false;
        AddPoint(path, from);
        foreach (int index in nodePath) AddPoint(path, _nodes[index].Position);
        AddPoint(path, destination);
        for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
        return true;
    }

    public int FindClosestNode(Vector3 position, NavigationAccess access)
    {
        position = AtSamplingHeight(position);
        int closest = -1;
        float closestDistance = _connectionDistance * _connectionDistance;
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (!CanUseNode(i, access)) continue;
            float distance = (position - _nodes[i].Position).sqrMagnitude;
            if (distance > closestDistance || !CanTraverse(position, _nodes[i].Position, access)) continue;
            closest = i;
            closestDistance = distance;
        }
        return closest;
    }

    public bool CanTraverse(Vector3 from, Vector3 to, NavigationAccess access)
    {
        from = AtSamplingHeight(from);
        to = AtSamplingHeight(to);
        if (!InsideMap(from) || !InsideMap(to)) return false;
        if (access == NavigationAccess.Wolf && TouchesSafeZone(from, to)) return false;
        return IsPhysicalSegmentClear(from, to);
    }

    // La topologia se guarda, pero cada busqueda consulta los obstaculos actuales.
    private bool IsPhysicalSegmentClear(Vector3 from, Vector3 to)
    {
        if (Physics.CheckSphere(from, _agentRadius, _obstacleMask, QueryTriggerInteraction.Ignore)
            || Physics.CheckSphere(to, _agentRadius, _obstacleMask, QueryTriggerInteraction.Ignore)) return false;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        return distance < 0.0001f || !Physics.SphereCast(from, _agentRadius, direction / distance,
            out _, distance, _obstacleMask, QueryTriggerInteraction.Ignore);
    }

    public bool IsSafePosition(Vector3 position)
    {
        return TouchesSafeZone(position, position);
    }

    private bool InsideMap(Vector3 position)
    {
        return position.x >= _origin.x + _agentRadius && position.x <= _origin.x + _size.x - _agentRadius
            && position.z >= _origin.y + _agentRadius && position.z <= _origin.y + _size.y - _agentRadius;
    }

    private bool TouchesSafeZone(Vector3 from, Vector3 to)
    {
        if (_scenario == null) return false;
        foreach (ScenarioZone zone in _scenario.SafeZones)
        {
            if (zone == null) continue;
            Vector3 scale = zone.transform.lossyScale;
            float marginX = _agentRadius / Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float marginZ = _agentRadius / Mathf.Max(0.001f, Mathf.Abs(scale.z));
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(
                Mathf.Abs(zone.Size.x) + marginX * 2f, 2f, Mathf.Abs(zone.Size.y) + marginZ * 2f));
            Vector3 localFrom = zone.transform.InverseTransformPoint(from);
            Vector3 localTo = zone.transform.InverseTransformPoint(to);
            localFrom.y = localTo.y = 0f;
            if (bounds.Contains(localFrom) || bounds.Contains(localTo)) return true;
            Vector3 offset = localTo - localFrom;
            float distance = offset.magnitude;
            if (distance > 0.0001f && bounds.IntersectRay(new Ray(localFrom, offset / distance), out float entry)
                && entry <= distance) return true;
        }
        return false;
    }

    private Vector3 AtSamplingHeight(Vector3 position)
    {
        position.y = _samplingHeight;
        return position;
    }

    private static void AddPoint(List<Vector3> path, Vector3 point)
    {
        if (path.Count == 0 || (path[path.Count - 1] - point).sqrMagnitude > 0.000001f) path.Add(point);
    }

    private void OnDrawGizmos()
    {
        if (_showGraph) DrawGraph();
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showGraph) DrawGraph();
    }

    private void DrawGraph()
    {
        Color previous = Gizmos.color;
        for (int i = 0; i < _nodes.Count; i++)
        {
            NavigationNode node = _nodes[i];
            Gizmos.color = node.IsSafeZone ? Color.magenta : new Color(0.3f, 0.85f, 1f, 0.6f);
            Gizmos.DrawSphere(node.Position, 0.055f);
            foreach (int neighbor in node.Neighbors)
                if (neighbor > i && neighbor < _nodes.Count) Gizmos.DrawLine(node.Position, _nodes[neighbor].Position);
        }
        Gizmos.color = previous;
    }
}

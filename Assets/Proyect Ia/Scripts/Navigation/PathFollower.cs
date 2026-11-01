using System.Collections.Generic;
using UnityEngine;

public class PathFollower : MonoBehaviour
{
    [SerializeField] private NavigationGraph _graph;
    [SerializeField] private NavigationAccess _access = NavigationAccess.Hunter;
    [SerializeField, Min(0f)] private float _speed = 5f;

    private readonly List<Vector3> _path = new List<Vector3>();
    private int _waypointIndex;

    public NavigationGraph Graph
    {
        get => _graph;
        set
        {
            if (_graph == value) return;
            Stop();
            _graph = value;
        }
    }

    public NavigationAccess Access
    {
        get => _access;
        set
        {
            if (_access == value) return;
            Stop();
            _access = value;
        }
    }

    public float Speed
    {
        get => _speed;
        set => _speed = Mathf.Max(0f, value);
    }

    public IReadOnlyList<Vector3> Path => _path;
    public int WaypointIndex => _waypointIndex;
    public bool IsMoving => !IsBlocked && _waypointIndex < _path.Count;
    public bool HasArrived { get; private set; }
    public bool IsBlocked { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float PathLength { get; private set; }

    public bool SetDestination(Vector3 destination)
    {
        Stop();
        if (_graph == null || !_graph.TryFindPath(transform.position, destination, _access,
            out List<Vector3> path, out float length)) return false;
        if (path == null || path.Count == 0) return false;

        _path.AddRange(path);
        PathLength = length;
        return true;
    }

    // El estado o controlador propietario decide cuando avanzar; no tiene Update propio.
    public void Tick(float deltaTime)
    {
        Velocity = Vector3.zero;
        if (!IsMoving || deltaTime <= 0f) return;
        if (_graph == null)
        {
            Block();
            return;
        }

        Vector3 previousPosition = transform.position;
        float remainingDistance = Mathf.Max(0f, _speed) * deltaTime;
        FollowWaypoints(remainingDistance);
        if (IsMoving) Velocity = (transform.position - previousPosition) / deltaTime;
    }

    private void FollowWaypoints(float remainingDistance)
    {
        while (IsMoving)
        {
            Vector3 target = AtCurrentHeight(_path[_waypointIndex]);
            if (!_graph.CanTraverse(transform.position, target, _access))
            {
                Block();
                return;
            }

            float distance = Vector3.Distance(transform.position, target);
            if (distance == 0f)
            {
                AdvanceWaypoint();
                continue;
            }
            if (remainingDistance <= 0f) return;

            if (distance <= remainingDistance)
            {
                // Llega exactamente al nodo antes de utilizar el presupuesto en el siguiente.
                transform.position = target;
                remainingDistance = Mathf.Max(0f, remainingDistance - distance);
                AdvanceWaypoint();
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, target, remainingDistance);
                return;
            }
        }
    }

    private void AdvanceWaypoint()
    {
        _waypointIndex++;
        if (_waypointIndex == _path.Count) HasArrived = true;
    }

    private void Block()
    {
        IsBlocked = true;
        HasArrived = false;
        Velocity = Vector3.zero;
    }

    public void Stop()
    {
        _path.Clear();
        _waypointIndex = 0;
        HasArrived = false;
        IsBlocked = false;
        Velocity = Vector3.zero;
        PathLength = 0f;
    }

    private Vector3 AtCurrentHeight(Vector3 point)
    {
        point.y = transform.position.y;
        return point;
    }

    private void OnDrawGizmos()
    {
        if (_path.Count == 0) return;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.yellow;
        Vector3 offset = Vector3.up * 0.12f;
        for (int i = 1; i < _path.Count; i++)
            Gizmos.DrawLine(AtCurrentHeight(_path[i - 1]) + offset, AtCurrentHeight(_path[i]) + offset);
        if (IsMoving)
            Gizmos.DrawLine(transform.position + offset, AtCurrentHeight(_path[_waypointIndex]) + offset);
        Gizmos.DrawWireSphere(AtCurrentHeight(_path[_path.Count - 1]) + offset, 0.2f);
        Gizmos.color = previousColor;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class WolfSearchState : State
{
    private readonly WolfAgent _agent;
    private readonly List<int> _candidates = new List<int>();
    private Vector3 _searchOrigin;
    private Vector3 _destination;
    private bool _reachedLastKnownPosition;
    private bool _hasDestination;
    private float _selectionRetry;

    public WolfSearchState(WolfAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter() => RestartAtSnapshot();

    public override void Update()
    {
        if (WolfAgent.PlanarDistanceSquared(_searchOrigin, _agent.LastKnownPosition) > 0.000001f)
            RestartAtSnapshot();
        if (_agent.Navigation == null) return;
        if (!_reachedLastKnownPosition)
        {
            _agent.NavigateTo(_searchOrigin, _agent.SearchSpeed);
            _reachedLastKnownPosition = _agent.Navigation.HasArrived;
            return;
        }
        SearchNearbyNodes();
    }

    private void RestartAtSnapshot()
    {
        _agent.Stop();
        _searchOrigin = _agent.LastKnownPosition;
        _reachedLastKnownPosition = false;
        _hasDestination = false;
        _selectionRetry = 0f;
    }

    private void SearchNearbyNodes()
    {
        _selectionRetry = Mathf.Max(0f, _selectionRetry - _agent.DeltaTime);
        if (!_hasDestination || _agent.Navigation.HasArrived)
        {
            if (_selectionRetry > 0f) return;
            _hasDestination = SelectDestination();
            if (!_hasDestination)
            {
                _agent.Stop();
                _selectionRetry = 1f;
                return;
            }
        }
        _agent.NavigateTo(_destination, _agent.SearchSpeed);
    }

    private bool SelectDestination()
    {
        NavigationGraph graph = _agent.Navigation.Follower != null ? _agent.Navigation.Follower.Graph : null;
        if (graph == null) return false;
        _candidates.Clear();
        float radiusSquared = _agent.SearchRadius * _agent.SearchRadius;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            NavigationNode node = graph.Nodes[i];
            if (node == null || WolfAgent.PlanarDistanceSquared(_searchOrigin, node.Position) > radiusSquared
                || WolfAgent.PlanarDistanceSquared(_agent.transform.position, node.Position) < 1f
                || _agent.IsSafePosition(node.Position) || !graph.CanUseNode(i, NavigationAccess.Wolf)) continue;
            _candidates.Add(i);
        }
        if (_candidates.Count == 0) return false;
        int start = Random.Range(0, _candidates.Count);
        for (int i = 0; i < _candidates.Count; i++)
        {
            Vector3 candidate = graph.Nodes[_candidates[(start + i) % _candidates.Count]].Position;
            if (!graph.TryFindPath(_agent.transform.position, candidate, NavigationAccess.Wolf, out _, out _)) continue;
            _destination = candidate;
            return true;
        }
        return false;
    }

    public override void Exit() => _agent.Stop();
}

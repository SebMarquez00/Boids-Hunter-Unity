using UnityEngine;

public class WolfPatrolState : State
{
    private readonly WolfAgent _agent;
    private int _waypointIndex;

    public WolfPatrolState(WolfAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter() => _agent.Stop();

    public override void Update()
    {
        if (TryChangeState()) return;
        FollowCircuit();
    }

    private bool TryChangeState()
    {
        if (_agent.HasVisualContact)
        {
            _stateMachine.ChangeState(WolfStates.Chase);
            return true;
        }
        if (!_agent.HasLastKnownPosition) return false;
        _stateMachine.ChangeState(WolfStates.Search);
        return true;
    }

    private void FollowCircuit()
    {
        int count = _agent.Waypoints.Count;
        if (count == 0 || _agent.Navigation == null)
        {
            _agent.Stop();
            return;
        }
        _waypointIndex %= count;
        for (int i = 0; i < count && _agent.Waypoints[_waypointIndex] == null; i++)
            _waypointIndex = (_waypointIndex + 1) % count;
        Transform waypoint = _agent.Waypoints[_waypointIndex];
        if (waypoint == null)
        {
            _agent.Stop();
            return;
        }
        _agent.NavigateTo(waypoint.position, _agent.PatrolSpeed);
        if (_agent.Navigation.HasArrived) _waypointIndex = (_waypointIndex + 1) % count;
    }

    public override void Exit() => _agent.Stop();
}

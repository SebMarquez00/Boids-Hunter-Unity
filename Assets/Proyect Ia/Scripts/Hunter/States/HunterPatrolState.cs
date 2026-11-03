using UnityEngine;

public class HunterPatrolState : State
{
    private readonly HunterAgent _agent;
    private int _currentNode;

    public HunterPatrolState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        Debug.Log("Hunter: Patrol");
    }

    public override void Update()
    {
        if (TryChangeState()) return;
        FollowWaypoints();
    }

    private bool TryChangeState()
    {
        // Antes de iniciar un ataque, prioriza una recoleccion disponible.
        BoidAgent eliminated = _agent.FindClosestDeadBoid();
        if (eliminated != null)
        {
            _agent.SetTarget(eliminated);
            _stateMachine.ChangeState(HunterStates.Gather);
            return true;
        }

        if (_agent.CanPlaceInterestObject())
        {
            _stateMachine.ChangeState(HunterStates.PlaceInterest);
            return true;
        }

        if (!_agent.CanAttack) return false;
        BoidAgent target = _agent.FindClosestAliveBoid();
        if (target == null) return false;

        _agent.SetTarget(target);
        _stateMachine.ChangeState(HunterStates.Attack);
        return true;
    }

    private void FollowWaypoints()
    {
        if (_agent.Waypoints.Count == 0)
        {
            _agent.Stop();
            return;
        }

        _currentNode %= _agent.Waypoints.Count;
        Transform waypoint = _agent.Waypoints[_currentNode];
        if (waypoint == null)
        {
            _agent.Stop();
            return;
        }

        if (_agent.DistanceTo(waypoint.position) <= _agent.WaypointCheckDistance)
        {
            _currentNode = (_currentNode + 1) % _agent.Waypoints.Count;
            waypoint = _agent.Waypoints[_currentNode];
        }

        if (waypoint == null)
        {
            _agent.Stop();
            return;
        }

        _agent.MoveTowards(waypoint.position);
    }

    public override void Exit()
    {
        _agent.Stop();
    }
}

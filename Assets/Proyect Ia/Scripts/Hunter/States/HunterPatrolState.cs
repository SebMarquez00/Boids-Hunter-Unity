using UnityEngine;

public class HunterPatrolState : State
{
    private HunterAgent _agent;
    private int _currentNode;

    public HunterPatrolState(
        HunterAgent agent,
        StateMachine stateMachine
    ) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        Debug.Log("Hunter: Patrol");
    }

    public override void Update()
    {
        if (_agent.Waypoints.Count == 0)
        {
            _agent.Stop();
            return;
        }

        Transform waypoint =
            _agent.Waypoints[_currentNode];

        if (waypoint == null)
        {
            _agent.Stop();
            return;
        }

        Vector3 direction =
            waypoint.position - _agent.transform.position;

        direction.y = 0f;

        if (direction.magnitude <= _agent.WaypointCheckDistance)
        {
            _currentNode++;

            if (_currentNode >= _agent.Waypoints.Count)
            {
                _currentNode = 0;
            }

            waypoint = _agent.Waypoints[_currentNode];

            if (waypoint == null)
            {
                _agent.Stop();
                return;
            }
        }

        _agent.MoveTowards(waypoint.position);
    }

    public override void Exit()
    {
        _agent.Stop();
    }
}
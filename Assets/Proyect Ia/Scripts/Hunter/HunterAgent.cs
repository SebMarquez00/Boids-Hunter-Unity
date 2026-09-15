using System.Collections.Generic;
using UnityEngine;

public enum HunterStates
{
    Patrol
}

public class HunterAgent : Agent
{
    [Header("Movement")]
    [SerializeField] private float _speed = 3f;

    [Header("Patrol")]
    [SerializeField]
    private List<Transform> _waypoints =
        new List<Transform>();

    [SerializeField] private float _waypointCheckDistance = 0.3f;

    [Header("Debug")]
    [SerializeField] private string _currentStateName;

    private StateMachine _stateMachine;

    public List<Transform> Waypoints => _waypoints;

    public float WaypointCheckDistance =>
        _waypointCheckDistance;

    private void Awake()
    {
        _stateMachine = new StateMachine();

        HunterPatrolState patrolState =
            new HunterPatrolState(this, _stateMachine);

        _stateMachine.RegisterState(
            HunterStates.Patrol,
            patrolState
        );

        _stateMachine.ChangeState(HunterStates.Patrol);
    }

    private void Update()
    {
        _stateMachine.Update();

        _currentStateName =
            _stateMachine.CurrentState.GetType().Name;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        Vector3 previousPosition = transform.position;

        transform.position = Vector3.MoveTowards(
            transform.position,
            transform.position + direction,
            _speed * Time.deltaTime
        );

        if (Time.deltaTime > 0f)
        {
            _velocity =
                (transform.position - previousPosition)
                / Time.deltaTime;
        }
        else
        {
            _velocity = Vector3.zero;
        }

        if (_velocity.sqrMagnitude > 0.001f)
        {
            transform.forward = _velocity.normalized;
        }
    }

    public void Stop()
    {
        _velocity = Vector3.zero;
    }
}
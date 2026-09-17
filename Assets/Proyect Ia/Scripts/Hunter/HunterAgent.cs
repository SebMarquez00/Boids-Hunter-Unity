using System.Collections.Generic;
using UnityEngine;

public enum HunterStates
{
    Patrol,
    Attack,
    Gather,
    PlaceInterest
}

public class HunterAgent : Agent
{
    [Header("Movement")]
    [SerializeField] private float _speed = 4.5f;

    [Header("Patrol")]
    [SerializeField]
    private List<Transform> _waypoints =
        new List<Transform>();

    [SerializeField] private float _waypointCheckDistance = 0.3f;

    [Header("Debug")]
    [SerializeField] private string _currentStateName;

    [Header("Gather")]
    [SerializeField] private float _gatherRadius = 1.2f;
    [SerializeField] private float _gatherDuration = 2f;
    [SerializeField] private float _gatherProgress;

    [Header("Interest Objects")]
    [SerializeField] private InterestObject _interestPrefab;
    [SerializeField] private Transform _interestParent;
    [SerializeField] private float _spawnInterval = 5f;
    [SerializeField] private float _interestHeight = 0.125f;
    [SerializeField] private float _spawnTimer;
    private const int MaxInterestObjects = 5;

    [SerializeField, Min(0f)] private float _placementDuration = 1f;
    public float PlacementDuration => _placementDuration;

    // Datos de solo lectura para la interfaz.
    public State CurrentState => _stateMachine.CurrentState;
    public float GatherProgress => _gatherProgress;
    public int InterestLimit => MaxInterestObjects;

    public float GatherRadius => _gatherRadius;
    public float GatherDuration => _gatherDuration;

    private StateMachine _stateMachine;

    public List<Transform> Waypoints => _waypoints;

    public float WaypointCheckDistance =>
        _waypointCheckDistance;

    [Header("Perception")]
    [SerializeField] private float _viewRadius = 7f;

    [Header("Attack")]
    [SerializeField] private float _tba = 1.5f;
    [SerializeField] private float _rangeAttackRadius = 5.5f;
    [SerializeField] private float _meleeAttackRadius = 1.5f;

    [SerializeField] private float _meleeDamage = 10f;

    [SerializeField] private float _attackTimer;

    [Header("Target")]
    [SerializeField] private BoidAgent _target;

    [Header("Projectile")]
    [SerializeField] private HunterProjectile _projectilePrefab;

    private HunterProjectile _activeProjectile;
    private bool _projectileHit;

    public bool HasActiveProjectile =>
        _activeProjectile != null;

    public bool ProjectileHit => _projectileHit;

    public float TBA => _tba;
    public float RangeAttackRadius => _rangeAttackRadius;
    public float MeleeAttackRadius => _meleeAttackRadius;

    public bool CanAttack => _attackTimer >= TBA;

    public BoidAgent Target => _target;

    private void Awake()
    {
        _stateMachine = new StateMachine();

        HunterPatrolState patrolState =
            new HunterPatrolState(this, _stateMachine);

        HunterAttackState attackState =
            new HunterAttackState(this, _stateMachine);

        _stateMachine.RegisterState(
            HunterStates.Patrol,
            patrolState
        );

        _stateMachine.RegisterState(
            HunterStates.Attack,
            attackState
        );

        HunterGatherState gatherState =
            new HunterGatherState(this, _stateMachine);

        _stateMachine.RegisterState(
            HunterStates.Gather,
            gatherState
        );

        HunterPlaceInterestState placementState =
            new HunterPlaceInterestState(this, _stateMachine);

        _stateMachine.RegisterState(
            HunterStates.PlaceInterest,
            placementState
        );

        _stateMachine.ChangeState(HunterStates.Patrol);
    }

    private void Update()
    {
        _attackTimer += Time.deltaTime;
        _spawnTimer += Time.deltaTime;
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
    public float DistanceTo(Vector3 position)
    {
        Vector3 direction = position - transform.position;

        direction.y = 0f;

        return direction.magnitude;
    }

    public bool CanSee(BoidAgent agent)
    {
        if (agent == null || !agent.isActiveAndEnabled)
        {
            return false;
        }

        return DistanceTo(agent.transform.position) <= _viewRadius;
    }

    public BoidAgent FindClosestAliveBoid()
    {
        return FindClosestBoid(true);
    }

    public BoidAgent FindClosestDeadBoid()
    {
        return FindClosestBoid(false);
    }

    private BoidAgent FindClosestBoid(bool alive)
    {
        BoidAgent closest = null;
        float closestDistance = float.MaxValue;

        foreach (BoidAgent boid in BoidAgent.AllAgents)
        {
            if (boid == null || boid.IsAlive != alive)
            {
                continue;
            }

            if (!CanSee(boid))
            {
                continue;
            }

            float distance = DistanceTo(boid.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = boid;
            }
        }

        return closest;
    }

    public void SetTarget(BoidAgent target)
    {
        _target = target;
    }
    public bool TryMeleeAttack()
    {
        if (!CanAttack)
        {
            return false;
        }

        if (_target == null || !_target.IsAlive || !CanSee(_target))
        {
            return false;
        }

        if (DistanceTo(_target.transform.position) > MeleeAttackRadius)
        {
            return false;
        }

        if (_meleeDamage <= 0f)
        {
            return false;
        }

        _target.TakeDamage(_meleeDamage);

        _attackTimer = 0f;

        Debug.Log("Hunter: ataque cuerpo a cuerpo");

        return true;
    }
    public void Shoot()
    {
        if (!CanAttack || HasActiveProjectile)
        {
            return;
        }

        if (_projectilePrefab == null)
        {
            return;
        }

        if (_target == null || !_target.IsAlive || !CanSee(_target))
        {
            return;
        }

        if (DistanceTo(_target.transform.position) > RangeAttackRadius)
        {
            return;
        }

        _projectileHit = false;

        _activeProjectile = Instantiate(
            _projectilePrefab,
            transform.position,
            Quaternion.identity
        );

        _activeProjectile.Initialize(
            this,
            _target
        );
    }

    public void ResolveProjectile(bool hit)
    {
        _activeProjectile = null;
        _projectileHit = hit;

        if (hit)
        {
            _attackTimer = 0f;

            Debug.Log("Hunter: impacto a distancia");
        }
    }

    public void ClearProjectile()
    {
        if (_activeProjectile != null)
        {
            Destroy(_activeProjectile.gameObject);
        }

        _activeProjectile = null;
        _projectileHit = false;
    }
    public void SetGatherProgress(float progress)
    {
        _gatherProgress = progress;
    }

    public bool CanPlaceInterestObject()
    {
        if (_interestPrefab == null
            || _spawnTimer < Mathf.Max(0.1f, _spawnInterval)
            || InterestObject.AllObjects.Count >= MaxInterestObjects)
        {
            return false;
        }

        foreach (InterestObject interest in InterestObject.AllObjects)
        {
            if (interest == null) continue;
            Vector3 offset = interest.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 1f) return false;
        }

        return true;
    }

    public void PlaceInterestObject()
    {
        if (!CanPlaceInterestObject()) return;

        Vector3 position = transform.position;
        position.y = _interestHeight;

        Instantiate(
            _interestPrefab,
            position,
            Quaternion.identity,
            _interestParent
        );

        _spawnTimer = 0f;
    }

    public void Stop()
    {
        _velocity = Vector3.zero;
    }
}
using System.Collections.Generic;
using UnityEngine;

public enum HunterStates
{
    Patrol,
    Attack,
    Gather,
    PlaceInterest,
    Search,
    Dead
}

public class HunterAgent : Agent
{
    [Header("Movement")]
    [SerializeField] private float _speed = 4.5f;
    [SerializeField, Min(0f)] private float _maxPredictionTime = 1f;

    [Header("Scenario Navigation")]
    [SerializeField] private AgentNavigator _navigation;
    [SerializeField] private ScenarioReferences _scenario;

    [Header("Respawn")]
    [SerializeField, Min(0f)] private float _respawnTime = 5f;
    private bool _captureRequested;
    private int _gatherCount;

    [Header("Patrol")]
    [SerializeField] private List<Transform> _waypoints = new List<Transform>();
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
    [SerializeField, Min(0f)] private float _placementDuration = 1f;
    private const int MaxInterestObjects = 5;
    private const float InterestSpacing = 1f;

    [Header("Perception")]
    [SerializeField] private float _viewRadius = 7f;
    [SerializeField] private LayerMask _perceptionMask = ~0;
    [SerializeField, Min(0.01f)] private float _perceptionInterval = 0.1f;

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

    private StateMachine _stateMachine;
    private HunterPerception _perception;
    private HunterMovement _movement;
    private HunterCombat _combat;
    private HunterLife _life;

    // Datos de solo lectura utilizados por los estados y la interfaz.
    public State CurrentState => _stateMachine?.CurrentState;
    public float GatherProgress => _gatherProgress;
    public int InterestLimit => MaxInterestObjects;
    public float GatherRadius => _gatherRadius;
    public float GatherDuration => _gatherDuration;
    public float PlacementDuration => _placementDuration;
    public List<Transform> Waypoints => _waypoints;
    public float WaypointCheckDistance => _waypointCheckDistance;
    public float TBA => _tba;
    public float RangeAttackRadius => _rangeAttackRadius;
    public float MeleeAttackRadius => _meleeAttackRadius;
    public bool CanAttack => !IsDead && !_captureRequested && _attackTimer >= TBA;
    public BoidAgent Target => _target;
    public bool HasActiveProjectile => _combat != null && _combat.HasActiveProjectile;
    public bool ProjectileHit => _combat != null && _combat.ProjectileHit;
    public bool UsesNavigation => _navigation != null && _scenario != null;
    public AgentNavigator Navigation => _navigation;
    public ScenarioReferences Scenario => _scenario;
    public float MovementSpeed => Mathf.Max(0f, _speed);
    public bool IsDead => CurrentState is HunterDeadState;
    public bool IsAvailableToWolves => isActiveAndEnabled && !IsDead && !_captureRequested;
    public bool IsInSafeZone => _scenario != null && _scenario.ContainsSafeZone(transform.position);
    public float RespawnTime => Mathf.Max(0f, _respawnTime);
    public float RespawnRemaining => CurrentState is HunterDeadState dead ? Mathf.Max(0f, RespawnTime - dead.Elapsed) : 0f;
    public int GatherCount => _gatherCount;
    public int DeathCount { get; private set; }
    public int RespawnCount { get; private set; }
    public HunterStates InitialState => UsesNavigation ? HunterStates.Search : HunterStates.Patrol;
    public HunterStates ReturnState => InitialState;
    public bool IsInHuntingGround => _scenario != null && _scenario.HuntingZone != null
        && _scenario.HuntingZone.Contains(transform.position);
    public LayerMask ObstacleMask => UsesNavigation && _navigation.Follower != null
        && _navigation.Follower.Graph != null ? _navigation.Follower.Graph.ObstacleMask : (LayerMask)0;

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        UpdateTimers();
        UpdatePerception();
        _stateMachine.Update();
        UpdateStateFeedback();
    }

    private void OnDisable()
    {
        ClearProjectile();
        Stop();
    }

    private void Initialize()
    {
        _perception = new HunterPerception();
        _movement = new HunterMovement(transform);
        _combat = new HunterCombat(this);
        _life = new HunterLife(transform);
        InitializeStateMachine();
    }

    private void InitializeStateMachine()
    {
        _stateMachine = new StateMachine();
        _stateMachine.RegisterState(HunterStates.Patrol, new HunterPatrolState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Attack, new HunterAttackState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Gather, new HunterGatherState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.PlaceInterest, new HunterPlaceInterestState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Search, new HunterSearchState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Dead, new HunterDeadState(this, _stateMachine));
        _stateMachine.RegisterGlobalTransition(HunterStates.Dead, () => _captureRequested && !IsDead);
        _stateMachine.ChangeState(InitialState);
    }

    private void UpdateTimers()
    {
        if (IsDead || _captureRequested) return;
        _attackTimer += Time.deltaTime;
        if (!UsesNavigation || IsInHuntingGround)
            _spawnTimer += Time.deltaTime;
    }

    private void UpdatePerception()
    {
        if (IsDead || _captureRequested) return;
        _perception.Update(transform.position, _viewRadius, InterestSpacing, _perceptionMask,
            _perceptionInterval, Time.deltaTime);
    }

    private void UpdateStateFeedback()
    {
        _currentStateName = _stateMachine.CurrentState.GetType().Name;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        if (UsesNavigation)
        {
            NavigateTo(targetPosition);
            return;
        }
        _velocity = _movement.MoveTowards(targetPosition, _speed, Time.deltaTime);
    }

    public void NavigateTo(Vector3 targetPosition)
    {
        if (!UsesNavigation)
        {
            MoveTowards(targetPosition);
            return;
        }
        _navigation.NavigateTo(targetPosition, _speed, Time.deltaTime);
        _velocity = _navigation.Velocity;
    }

    public void ApproachTarget(BoidAgent target)
    {
        if (!CanSee(target))
        {
            Stop();
            return;
        }
        MoveToVisibleTarget(target.transform.position, target.transform.position);
    }

    public void Pursue(BoidAgent target)
    {
        if (!CanSee(target))
        {
            Stop();
            return;
        }

        Vector3 futurePosition = _movement.PredictPosition(target.transform.position, target.Velocity,
            _speed, _maxPredictionTime);
        MoveToVisibleTarget(futurePosition, target.transform.position);
    }

    private void MoveToVisibleTarget(Vector3 preferredPosition, Vector3 currentTargetPosition)
    {
        if (!UsesNavigation)
        {
            MoveTowards(preferredPosition);
            return;
        }
        NavigationGraph graph = _navigation.Follower != null ? _navigation.Follower.Graph : null;
        if (graph != null && graph.CanTraverse(transform.position, preferredPosition, NavigationAccess.Hunter))
            MoveDirectly(preferredPosition);
        else if (graph != null && graph.CanTraverse(transform.position, currentTargetPosition, NavigationAccess.Hunter))
            MoveDirectly(currentTargetPosition);
        else NavigateTo(currentTargetPosition);
    }

    private void MoveDirectly(Vector3 targetPosition)
    {
        _navigation.Stop();
        _velocity = _movement.MoveTowards(targetPosition, _speed, Time.deltaTime, _navigation.TurnSpeed);
    }

    public float DistanceTo(Vector3 position)
    {
        return _movement.DistanceTo(position);
    }

    public bool CanSee(BoidAgent agent)
    {
        return _perception.CanSee(agent, transform.position, _viewRadius, ObstacleMask);
    }

    public BoidAgent FindClosestAliveBoid()
    {
        return _perception.FindClosestBoid(transform.position, _viewRadius, true, ObstacleMask);
    }

    public BoidAgent FindClosestDeadBoid()
    {
        return _perception.FindClosestBoid(transform.position, _viewRadius, false, ObstacleMask);
    }

    public void SetTarget(BoidAgent target)
    {
        _target = target;
    }

    public bool TryMeleeAttack()
    {
        if (!_combat.TryMeleeAttack(_meleeDamage)) return false;

        _attackTimer = 0f;
        Debug.Log("Hunter: ataque cuerpo a cuerpo");
        return true;
    }

    public void Shoot()
    {
        _combat.Shoot(_projectilePrefab);
    }

    public void ResolveProjectile(bool hit)
    {
        _combat.ResolveProjectile(hit);
        if (!hit) return;

        _attackTimer = 0f;
        Debug.Log("Hunter: impacto a distancia");
    }

    public void ClearProjectile()
    {
        _combat?.ClearProjectile();
    }

    public void SetGatherProgress(float progress)
    {
        _gatherProgress = progress;
    }

    public bool CanPlaceInterestObject()
    {
        if (IsDead || _captureRequested) return false;
        if (UsesNavigation && !IsInHuntingGround) return false;
        if (_interestPrefab == null || _spawnTimer < Mathf.Max(0.1f, _spawnInterval)) return false;
        if (InterestObject.ActiveCount >= MaxInterestObjects) return false;

        return !_perception.HasNearbyInterest(transform.position, InterestSpacing);
    }

    public void PlaceInterestObject()
    {
        if (!CanPlaceInterestObject()) return;

        Vector3 position = transform.position;
        position.y = _interestHeight;
        Instantiate(_interestPrefab, position, Quaternion.identity, _interestParent);
        _spawnTimer = 0f;
    }

    public void Stop()
    {
        if (_navigation != null) _navigation.Stop();
        _velocity = Vector3.zero;
    }

    public bool RequestCapture()
    {
        if (!UsesNavigation || !IsAvailableToWolves || IsInSafeZone) return false;
        // El lobo comunica el hecho; la transición se resuelve al actualizar la FSM.
        _captureRequested = true;
        return true;
    }

    public void EnterDead()
    {
        Stop();
        ClearProjectile();
        SetTarget(null);
        SetGatherProgress(0f);
        _captureRequested = false;
        _life.Hide();
        DeathCount++;
    }

    public void Respawn()
    {
        Stop();
        if (_scenario != null && _scenario.HunterSpawn != null)
            transform.SetPositionAndRotation(_scenario.HunterSpawn.position, _scenario.HunterSpawn.rotation);
        _gatherCount = 0;
        _attackTimer = _spawnTimer = 0f;
        _perception = new HunterPerception();
        _life.Restore();
        RespawnCount++;
    }

    public void RegisterGather()
    {
        if (!IsDead && !_captureRequested) _gatherCount++;
    }

    private void OnDrawGizmosSelected()
    {
        DrawRadius(_viewRadius, Color.cyan);
        DrawRadius(_rangeAttackRadius, Color.yellow);
        DrawRadius(_meleeAttackRadius, Color.red);
    }

    private void DrawRadius(float radius, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, radius));
    }
}

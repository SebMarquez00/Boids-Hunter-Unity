using System.Collections.Generic;
using UnityEngine;

public enum WolfStates { Patrol, Chase, Search }

public class WolfAgent : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private AgentNavigator _navigation;
    [SerializeField] private HunterAgent _hunter;
    [SerializeField] private ScenarioReferences _scenario;
    [SerializeField] private WolfPack _pack;
    [SerializeField] private Transform[] _waypoints = new Transform[0];

    [Header("Movement")]
    [SerializeField, Min(0f)] private float _patrolSpeed = 2.5f;
    [SerializeField, Min(0f)] private float _chaseSpeed = 3.4f;
    [SerializeField, Min(0f)] private float _searchSpeed = 2.8f;

    [Header("Perception")]
    [SerializeField, Min(0f)] private float _viewRadius = 9f;
    [SerializeField, Range(0f, 360f)] private float _viewAngle = 110f;
    [SerializeField, Min(0f)] private float _captureRadius = 0.85f;
    [SerializeField, Min(0.01f)] private float _perceptionInterval = 0.1f;
    [SerializeField, Min(0f)] private float _searchRadius = 6f;

    [Header("Debug")]
    [SerializeField] private string _currentStateName;

    private const float HowlInterval = 1.5f;
    private const float AlertDistance = 0.75f;
    private StateMachine _stateMachine;
    private WolfPerception _perception;
    private WolfPack _subscribedPack;
    private float _perceptionTimer;
    private float _howlTimer;
    private bool _hasReportedPosition;
    private bool _pendingSightingAlert;
    private Vector3 _reportedPosition;

    public AgentNavigator Navigation => _navigation;
    public HunterAgent Hunter => _hunter;
    public ScenarioReferences Scenario => _scenario;
    public WolfPack Pack => _pack;
    public IReadOnlyList<Transform> Waypoints => _waypoints;
    public State CurrentState => _stateMachine?.CurrentState;
    public string CurrentStateName => _currentStateName;
    public float ViewRadius => Mathf.Max(0f, _viewRadius);
    public float ViewAngle => Mathf.Clamp(_viewAngle, 0f, 360f);
    public float CaptureRadius => Mathf.Max(0f, _captureRadius);
    public float SearchRadius => Mathf.Max(0f, _searchRadius);
    public bool HasVisualContact => _perception != null && _perception.HasVisualContact;
    public bool HasLastKnownPosition => _perception != null && _perception.HasLastKnownPosition;
    public Vector3 LastKnownPosition => _perception != null ? _perception.LastKnownPosition : default;
    public float LastAlertTime { get; private set; } = float.NegativeInfinity;
    public float LastReceivedAlertTime { get; private set; } = float.NegativeInfinity;
    public float LastCaptureTime { get; private set; } = float.NegativeInfinity;
    public int HowlCount { get; private set; }
    public int ReceivedAlertCount { get; private set; }
    public int CaptureCount { get; private set; }
    public float PatrolSpeed => LimitSpeed(_patrolSpeed);
    public float ChaseSpeed => LimitSpeed(_chaseSpeed);
    public float SearchSpeed => LimitSpeed(_searchSpeed);
    public float DeltaTime { get; private set; }
    public bool CanTrackHunter => _hunter != null && _hunter.IsAvailableToWolves && !_hunter.IsInSafeZone;

    private void Awake() => Initialize();
    private void OnEnable() => SubscribeToPack();
    private void Update() => UpdateAgent(Time.deltaTime);

    private void OnDisable()
    {
        UnsubscribeFromPack();
        ForgetHunter();
        Stop();
    }

    public void Initialize()
    {
        Stop();
        _perception = new WolfPerception();
        _perceptionTimer = _howlTimer = 0f;
        _hasReportedPosition = false;
        _pendingSightingAlert = false;
        if (_navigation != null && _navigation.Follower != null)
            _navigation.Follower.Access = NavigationAccess.Wolf;
        InitializeStateMachine();
        if (isActiveAndEnabled) SubscribeToPack();
        UpdateStateFeedback();
    }

    private void InitializeStateMachine()
    {
        _stateMachine = new StateMachine();
        _stateMachine.RegisterState(WolfStates.Patrol, new WolfPatrolState(this, _stateMachine));
        _stateMachine.RegisterState(WolfStates.Chase, new WolfChaseState(this, _stateMachine));
        _stateMachine.RegisterState(WolfStates.Search, new WolfSearchState(this, _stateMachine));
        _stateMachine.ChangeState(WolfStates.Patrol);
    }

    public void UpdateAgent(float deltaTime)
    {
        if (_stateMachine == null || !isActiveAndEnabled) return;
        UpdateTimers(deltaTime);
        UpdatePerception();
        _stateMachine.Update();
        UpdateStateFeedback();
    }

    private void UpdateTimers(float deltaTime)
    {
        DeltaTime = deltaTime > 0f && !float.IsInfinity(deltaTime) ? deltaTime : 0f;
        _perceptionTimer = Mathf.Max(0f, _perceptionTimer - DeltaTime);
        _howlTimer = Mathf.Max(0f, _howlTimer - DeltaTime);
    }

    private void UpdatePerception()
    {
        if (!CanTrackHunter)
        {
            ForgetHunter();
            return;
        }
        // La adquisicion respeta el intervalo; una observacion activa se revalida
        // antes de perseguir para no seguir posiciones nuevas detras de una pared.
        if (!HasVisualContact && _perceptionTimer > 0f) return;
        _perceptionTimer = Mathf.Max(0.01f, _perceptionInterval);
        if (!CanSeeHunter())
        {
            _perception.LoseVisualContact();
            return;
        }
        if (!HasVisualContact) _pendingSightingAlert = true;
        _perception.Observe(_hunter.transform.position);
        TryHowl();
    }

    public bool CanSeeHunter()
    {
        if (!CanTrackHunter) return false;
        NavigationGraph graph = _navigation != null && _navigation.Follower != null
            ? _navigation.Follower.Graph : null;
        LayerMask obstacles = graph != null ? graph.ObstacleMask : _hunter.ObstacleMask;
        return WolfPerception.CanSee(transform.position, transform.forward, _hunter.transform.position,
            ViewRadius, ViewAngle, obstacles);
    }

    private void TryHowl()
    {
        if (_howlTimer > 0f || (_hasReportedPosition && !_pendingSightingAlert
            && PlanarDistanceSquared(_reportedPosition, LastKnownPosition) < AlertDistance * AlertDistance)) return;
        _reportedPosition = LastKnownPosition;
        _hasReportedPosition = true;
        _pendingSightingAlert = false;
        _howlTimer = HowlInterval;
        LastAlertTime = Time.time;
        HowlCount++;
        if (_pack != null) _pack.ReportSighting(this, _reportedPosition);
    }

    private void ReceiveAlert(WolfAgent source, Vector3 position)
    {
        if (source == this || !isActiveAndEnabled || _perception == null
            || !CanTrackHunter || IsSafePosition(position)) return;
        LastReceivedAlertTime = Time.time;
        ReceivedAlertCount++;
        if (!HasVisualContact) _perception.Remember(position);
    }

    public bool TryCaptureHunter()
    {
        if (!HasVisualContact || !CanSeeHunter()
            || PlanarDistanceSquared(transform.position, _hunter.transform.position) >= CaptureRadius * CaptureRadius
            || !_hunter.RequestCapture()) return false;
        LastCaptureTime = Time.time;
        CaptureCount++;
        return true;
    }

    public void NavigateTo(Vector3 position, float speed)
    {
        if (_navigation == null || _navigation.Follower == null) return;
        _navigation.Follower.Access = NavigationAccess.Wolf;
        _navigation.NavigateTo(position, LimitSpeed(speed), DeltaTime);
    }

    public void Stop() => _navigation?.Stop();

    public void ForgetHunter()
    {
        _perception?.Forget();
        _hasReportedPosition = false;
        _pendingSightingAlert = false;
    }

    public bool IsSafePosition(Vector3 position) => _scenario != null && _scenario.ContainsSafeZone(position);

    private float LimitSpeed(float speed) => Mathf.Min(Mathf.Max(0f, speed),
        _hunter != null ? Mathf.Max(0f, _hunter.MovementSpeed) * 0.9f : 0f);

    private void SubscribeToPack()
    {
        if (_subscribedPack == _pack) return;
        UnsubscribeFromPack();
        _subscribedPack = _pack;
        if (_subscribedPack != null) _subscribedPack.AlertRaised += ReceiveAlert;
    }

    private void UnsubscribeFromPack()
    {
        if (_subscribedPack != null) _subscribedPack.AlertRaised -= ReceiveAlert;
        _subscribedPack = null;
    }

    private void UpdateStateFeedback() => _currentStateName = _stateMachine.CurrentState.GetType().Name;

    public static float PlanarDistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }
}

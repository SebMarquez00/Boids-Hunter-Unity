using UnityEngine;

public class BoidAgent : Agent
{
    [Header("Health")]
    [SerializeField] private float _maxHealth = 20f;
    [SerializeField] private float _currentHealth;
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Sickness")]
    [SerializeField] private float _sickDuration = 5f;
    [SerializeField, Range(0.1f, 1f)] private float _sickSpeedMultiplier = 0.35f;
    [SerializeField] private Color _sickColor = new Color(0.15f, 0.7f, 0.55f, 1f);
    [SerializeField] private float _sickTimer;

    [Header("Respawn")]
    [SerializeField] private float _respawnDelay = 5f;

    [Header("Stats")]
    [SerializeField] private float _maxSpeed = 3f;
    [SerializeField] private float _maxSteering = 8f;

    [Header("Perception")]
    [SerializeField] private float _viewRadius = 4f;
    [SerializeField] private int _detectedNeighbors;
    [SerializeField] private float _interestRadius = 3f;
    [SerializeField] private float _escapeMargin = 1.5f;
    [SerializeField, Min(0.01f)] private float _perceptionInterval = 0.1f;
    [SerializeField] private LayerMask _perceptionLayers = ~0;

    [Header("Flocking")]
    [SerializeField] private float _separationRadius = 2f;
    [SerializeField, Range(0f, 3f)] private float _separationWeight = 1f;
    [SerializeField, Range(0f, 3f)] private float _alignmentWeight = 1f;
    [SerializeField, Range(0f, 3f)] private float _cohesionWeight = 1f;

    [Header("Threat")]
    [SerializeField] private bool _threatDetected;
    [SerializeField, Range(0f, 5f)] private float _evadeWeight = 2f;

    [Header("Arrive")]
    [SerializeField] private float _slowingDistance = 2f;
    [SerializeField] private float _stopDistance = .65f;
    [SerializeField] private InterestObject _interestTarget;

    [Header("Interaction")]
    [SerializeField] private float _interactionRadius = 1.1f;
    [SerializeField] private float _interactionDamage = 5f;
    [SerializeField] private float _interactionInterval = 0.25f;
    [SerializeField] private bool _isInteracting;

    private readonly BoidPerception _perception = new BoidPerception();
    private readonly BoidSteering _steering = new BoidSteering();
    private BoidHealth _health;
    private BoidMovement _movement;
    private BoidInteraction _interaction;
    private BoidFeedback _feedback;

    public bool ThreatDetected => _perception.Threat != null;
    public InterestObject InterestTarget => _interaction?.Target;
    public bool IsSick => _health != null && _health.IsSick;
    public bool IsInteracting => _interaction != null && _interaction.IsInteracting;
    public float CurrentMaxSpeed => _health != null ? _health.GetMaxSpeed(_maxSpeed, _sickSpeedMultiplier) : _maxSpeed;
    public bool IsAlive => _health != null && _health.IsAlive;

    private void Awake() => Initialize();
    private void OnEnable() => ResetAgent();
    private void OnDisable() => SuspendAgent();
    private void Update() => Tick(Time.deltaTime);

    private void Initialize()
    {
        if (_health != null) return;
        if (_bodyRenderer == null) _bodyRenderer = GetComponent<Renderer>();
        _health = new BoidHealth();
        _movement = new BoidMovement(transform);
        _interaction = new BoidInteraction(this, _movement);
        _feedback = new BoidFeedback(_bodyRenderer != null ? _bodyRenderer.material : null);
    }

    private void ResetAgent()
    {
        Initialize();
        _interaction.Release();
        _perception.Reset();
        _health.Reset(_maxHealth);
        _movement.InitializeVelocity(CurrentMaxSpeed);
        RefreshPresentation();
    }

    private void SuspendAgent()
    {
        _interaction?.Release();
        _perception.Reset();
        _movement?.Stop();
        RefreshPresentation();
    }

    private void Tick(float deltaTime)
    {
        if (!IsAlive) return;
        _health.Tick(deltaTime);
        UpdatePerception(deltaTime);
        _interaction.UpdateTarget(_perception, _interestRadius, ThreatDetected);
        if (!_interaction.IsInteracting) UpdateMovement(deltaTime);
        if (_interaction.Tick(_interactionRadius, _stopDistance, _interactionDamage, _interactionInterval, deltaTime))
        {
            _health.BecomeSick(_sickDuration);
            _movement.Resume(CurrentMaxSpeed);
        }
        RefreshPresentation();
    }

    private void UpdatePerception(float deltaTime)
    {
        _perception.Update(this, _viewRadius, _separationRadius, _interestRadius,
            _escapeMargin, _perceptionLayers, _perceptionInterval, deltaTime);
    }

    private void UpdateMovement(float deltaTime)
    {
        _steering.UpdateMotion(transform.position, _movement.Velocity, CurrentMaxSpeed, _maxSteering, deltaTime);
        _movement.Move(GetSteering(), CurrentMaxSpeed, deltaTime, WorldBounds.Instance);
    }

    private Vector3 GetSteering()
    {
        if (ThreatDetected)
            return _steering.Evade(_perception.Threat) * _evadeWeight
                + _steering.Separation(_perception.Neighbors, _separationRadius) * _separationWeight;
        if (InterestTarget != null)
            return _steering.Arrive(InterestTarget.transform.position, _slowingDistance, _stopDistance)
                + _steering.Separation(_perception.Neighbors, _separationRadius) * _separationWeight;

        Vector3 weights = new Vector3(_separationWeight, _alignmentWeight, _cohesionWeight);
        return _steering.Flocking(_perception.Neighbors, _separationRadius, _viewRadius, weights);
    }

    private void RefreshPresentation()
    {
        // Espejos para el Inspector y Agent.Velocity; los helpers conservan el estado real.
        _currentHealth = _health?.CurrentHealth ?? 0f;
        _sickTimer = _health?.SickTime ?? 0f;
        _interestTarget = InterestTarget;
        _isInteracting = IsInteracting;
        _threatDetected = ThreatDetected;
        _detectedNeighbors = _perception.VisibleNeighborCount;
        _velocity = _movement?.Velocity ?? Vector3.zero;
        _feedback?.Refresh(IsAlive, IsSick, _sickColor);
    }

    public void TakeDamage(float damage)
    {
        if (_health == null) return;
        if (_health.TakeDamage(damage)) SuspendAgent();
        RefreshPresentation();
    }

    public bool Collect()
    {
        return _health != null && _health.TryCollect(this, WorldBounds.Instance, _respawnDelay);
    }

    [ContextMenu("Debug/Recibir 5 de dano")]
    private void DebugTakeDamage()
    {
        if (Application.isPlaying) TakeDamage(5f);
    }

    private void OnDrawGizmosSelected()
    {
        BoidFeedback.DrawGizmos(transform.position, _velocity, ThreatDetected, _viewRadius, _separationRadius);
    }
}

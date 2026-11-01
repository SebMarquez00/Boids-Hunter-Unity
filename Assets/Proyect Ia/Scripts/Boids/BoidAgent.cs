using UnityEngine;

public class BoidAgent : Agent
{
    [Header("Health")]
    [SerializeField] private float _maxHealth = 20f;
    [SerializeField] private float _currentHealth;
    [SerializeField] private Renderer _bodyRenderer;
    private Color _originalColor;

    [Header("Sickness")]
    [SerializeField] private float _sickDuration = 5f;
    [SerializeField, Range(0.1f, 1f)] private float _sickSpeedMultiplier = 0.35f;
    [SerializeField] private Color _sickColor = new Color(0.15f, 0.7f, 0.55f, 1f);
    [SerializeField] private float _sickTimer;

    [Header("Respawn")]
    [SerializeField] private float _respawnDelay = 5f;
    private bool _collected;

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
    private float _interactionTimer;

    private readonly BoidPerception _perception = new BoidPerception();
    private readonly BoidSteering _steering = new BoidSteering();

    public bool ThreatDetected => _threatDetected;
    public InterestObject InterestTarget => _interestTarget;
    public bool IsSick => _sickTimer > 0f;
    public bool IsInteracting => _isInteracting;
    public float CurrentMaxSpeed => _maxSpeed * (IsSick ? _sickSpeedMultiplier : 1f);
    public bool IsAlive => _currentHealth > 0f;

    private void Awake() => InitializeAppearance();
    private void OnEnable() => ResetAgent();
    private void OnDisable() => ClearPerceptionAndInterest();
    private void Update() => Tick(Time.deltaTime);

    private void InitializeAppearance()
    {
        if (_bodyRenderer == null) _bodyRenderer = GetComponent<Renderer>();
        if (_bodyRenderer != null) _originalColor = _bodyRenderer.material.color;
    }

    private void ResetAgent()
    {
        ClearPerceptionAndInterest();
        _currentHealth = _maxHealth;
        _sickTimer = 0f;
        _collected = false;
        InitializeVelocity();
        SetColor(_originalColor);
    }

    private void ClearPerceptionAndInterest()
    {
        ReleaseInterestTarget();
        _perception.Reset();
        _threatDetected = false;
        _detectedNeighbors = 0;
    }

    private void InitializeVelocity()
    {
        Vector3 direction = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        _velocity = direction.normalized * CurrentMaxSpeed;
    }

    private void Tick(float deltaTime)
    {
        if (!IsAlive) return;
        UpdateSickness();
        UpdatePerception(deltaTime);
        UpdateInterestTarget();
        if (_isInteracting)
        {
            UpdateInteraction();
            return;
        }

        UpdateMovement(deltaTime);
        UpdateInteraction();
    }

    private void UpdatePerception(float deltaTime)
    {
        _perception.Update(this, _viewRadius, _separationRadius, _interestRadius,
            _escapeMargin, _perceptionLayers, _perceptionInterval, deltaTime);
        _detectedNeighbors = _perception.VisibleNeighborCount;
        _threatDetected = _perception.Threat != null;
    }

    private void UpdateInterestTarget()
    {
        if (_threatDetected)
        {
            ReleaseInterestTarget();
            return;
        }

        if (_perception.IsInterestAvailable(_interestTarget, this, _interestRadius)) return;
        ReleaseInterestTarget();
        InterestObject closest = _perception.GetClosestInterest(this, _interestRadius);
        if (closest != null && closest.TryReserve(this)) _interestTarget = closest;
    }

    private void UpdateMovement(float deltaTime)
    {
        _steering.UpdateMotion(transform.position, _velocity, CurrentMaxSpeed, _maxSteering, deltaTime);
        _velocity += GetSteering();
        _velocity.y = 0f;
        _velocity = Vector3.ClampMagnitude(_velocity, CurrentMaxSpeed);
        transform.position += _velocity * deltaTime;
        if (_velocity.sqrMagnitude > 0.001f) transform.forward = _velocity.normalized;
        if (WorldBounds.Instance != null)
            transform.position = WorldBounds.Instance.OutOfBounds(transform.position);
    }

    private Vector3 GetSteering()
    {
        if (_threatDetected)
            return _steering.Evade(_perception.Threat) * _evadeWeight
                + _steering.Separation(_perception.Neighbors, _separationRadius) * _separationWeight;
        if (_interestTarget != null)
            return _steering.Arrive(_interestTarget.transform.position, _slowingDistance, _stopDistance)
                + _steering.Separation(_perception.Neighbors, _separationRadius) * _separationWeight;

        Vector3 weights = new Vector3(_separationWeight, _alignmentWeight, _cohesionWeight);
        return _steering.Flocking(_perception.Neighbors, _separationRadius, _viewRadius, weights);
    }

    private void ReleaseInterestTarget()
    {
        if (_interestTarget != null) _interestTarget.Release(this);
        _interestTarget = null;
        _interactionTimer = 0f;
        _isInteracting = false;
    }

    private void UpdateInteraction()
    {
        _isInteracting = false;
        if (_threatDetected || _interestTarget == null || !_interestTarget.IsAvailableFor(this)
            || _interestTarget.ReservedBy != this)
        {
            _interactionTimer = 0f;
            return;
        }

        Vector3 targetPosition = _interestTarget.transform.position;
        targetPosition.y = transform.position.y;
        float interactionDistance = Mathf.Min(_interactionRadius, _stopDistance + 0.05f);
        if (Vector3.Distance(transform.position, targetPosition) > interactionDistance)
        {
            _interactionTimer = 0f;
            return;
        }

        StayBesideInterest(targetPosition);
        _interactionTimer += Time.deltaTime;
        if (_interactionTimer < _interactionInterval) return;
        bool consumed = _interestTarget.Consume(this, _interactionDamage);
        _interactionTimer = 0f;
        if (!consumed) return;
        ReleaseInterestTarget();
        BecomeSick();
        _velocity = transform.forward * CurrentMaxSpeed;
    }

    private void StayBesideInterest(Vector3 targetPosition)
    {
        Vector3 offset = transform.position - targetPosition;
        if (offset.sqrMagnitude < 0.001f) offset = -transform.forward;
        transform.position = targetPosition + offset.normalized * _stopDistance;
        _velocity = Vector3.zero;
        _isInteracting = true;
    }

    private void SetColor(Color color)
    {
        if (_bodyRenderer != null) _bodyRenderer.material.color = color;
    }

    private void BecomeSick()
    {
        _sickTimer = _sickDuration;
        _velocity = Vector3.ClampMagnitude(_velocity, CurrentMaxSpeed);
        SetColor(_sickColor);
    }

    private void UpdateSickness()
    {
        if (!IsSick) return;
        _sickTimer = Mathf.Max(0f, _sickTimer - Time.deltaTime);
        if (!IsSick) SetColor(_originalColor);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f) return;
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        if (!IsAlive) Die();
    }

    public bool Collect()
    {
        if (IsAlive || _collected || !gameObject.activeInHierarchy || WorldBounds.Instance == null) return false;
        _collected = true;
        WorldBounds.Instance.RespawnAfter(this, _respawnDelay);
        return true;
    }

    private void Die()
    {
        ClearPerceptionAndInterest();
        _sickTimer = 0f;
        _velocity = Vector3.zero;
        SetColor(Color.red);
    }

    [ContextMenu("Debug/Recibir 5 de dano")]
    private void DebugTakeDamage()
    {
        if (Application.isPlaying) TakeDamage(5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Application.isPlaying && _threatDetected ? Color.magenta : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _viewRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _separationRadius);
        if (!Application.isPlaying) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + _velocity);
    }
}

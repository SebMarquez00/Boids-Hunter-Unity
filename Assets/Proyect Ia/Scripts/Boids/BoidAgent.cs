using System.Collections.Generic;
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

    public bool IsSick => _sickTimer > 0f;
    public bool IsInteracting => _isInteracting;
    public float CurrentMaxSpeed => _maxSpeed * (IsSick ? _sickSpeedMultiplier : 1f);

    [Header("Respawn")]
    [SerializeField] private float _respawnDelay = 5f;
    private bool _collected;

    public bool IsAlive => _currentHealth > 0f;

    [Header("Stats")]
    [SerializeField] private float _maxSpeed = 3f;
    [SerializeField] private float _maxSteering = 8f;

    [Header("Perception")]
    [SerializeField] private float _viewRadius = 5f;
    [SerializeField] private int _detectedNeighbors;

    [Header("Flocking")]
    [SerializeField] private float _separationRadius = 2f;

    [Header("Threat")]
    [SerializeField] private Agent _hunter;
    [SerializeField] private bool _threatDetected;
    [SerializeField, Range(0f, 5f)]
    private float _evadeWeight = 2f;

    [Header("Arrive")]
    [SerializeField] private float _slowingDistance = 3f;
    [SerializeField] private float _stopDistance = 1.2f;

    [SerializeField] private InterestObject _interestTarget;

    [Header("Interaction")]
    [SerializeField] private float _interactionRadius = 1.5f;
    [SerializeField] private float _interactionDamage = 5f;
    [SerializeField] private float _interactionInterval = 0.25f;

    [SerializeField] private bool _isInteracting;

    private float _interactionTimer;

    [SerializeField, Range(0f, 3f)]
    private float _separationWeight = 1f;

    [SerializeField, Range(0f, 3f)]
    private float _alignmentWeight = 1f;

    [SerializeField, Range(0f, 3f)]
    private float _cohesionWeight = 1f;

    private static List<BoidAgent> _allAgents =
        new List<BoidAgent>();

    public static IEnumerable<BoidAgent> AllAgents => _allAgents;

    private void OnEnable()
    {
        _currentHealth = _maxHealth;
        _sickTimer = 0f;
        _collected = false;
        _detectedNeighbors = 0;
        InitializeVelocity();

        _interactionTimer = 0f;
        _isInteracting = false;
        _threatDetected = false;
        _interestTarget = null;

        _allAgents.Add(this);

        if (_bodyRenderer != null)
        {
            _bodyRenderer.material.color = _originalColor;
        }
    }

    private void OnDisable()
    {
        ReleaseInterestTarget();
        _allAgents.Remove(this);
    }

    private void Awake()
    {
        if (_bodyRenderer == null)
        {
            _bodyRenderer = GetComponent<Renderer>();
        }

        if (_bodyRenderer != null)
        {
            _originalColor = _bodyRenderer.material.color;
        }
    }

    private void InitializeVelocity()
    {
        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        );

        if (randomDirection.sqrMagnitude < 0.001f)
        {
            randomDirection = Vector3.forward;
        }

        _velocity = randomDirection.normalized * CurrentMaxSpeed;
    }

    private void Update()
    {
        if (!IsAlive)
        {
            return;
        }

        UpdateSickness();
        DetectNeighbors();
        _threatDetected = DetectHunter();

        if (_threatDetected)
        {
            ReleaseInterestTarget();
        }
        else
        {
            DetectInterestObject();
        }

        if (_threatDetected)
        {
            _velocity += Evade(_hunter) * _evadeWeight
                       + CalculateSeparation() * _separationWeight;
        }
        else if (_interestTarget != null)
        {
            _velocity += Arrive(_interestTarget.transform.position)
                       + CalculateSeparation() * _separationWeight;
        }
        else
        {
            _velocity += Flocking();
        }
        _velocity.y = 0f;
        _velocity = Vector3.ClampMagnitude(
            _velocity,
            CurrentMaxSpeed
        );

        transform.position += _velocity * Time.deltaTime;

        if (_velocity.sqrMagnitude > 0.001f)
        {
            transform.forward = _velocity.normalized;
        }

        transform.position =
            WorldBounds.Instance.OutOfBounds(transform.position);

        UpdateInteraction();
    }
    private Vector3 CalculateSteering(Vector3 desired)
    {
        Vector3 steering = desired - _velocity;

        return Vector3.ClampMagnitude(
            steering,
            _maxSteering * Time.deltaTime
        );
    }
    private Vector3 Seek(Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        Vector3 desired =
            direction.normalized * CurrentMaxSpeed;

        return CalculateSteering(desired);
    }
    private Vector3 Flee(Vector3 targetPosition)
    {
        Vector3 direction =
            transform.position - targetPosition;

        direction.y = 0f;

        Vector3 desired =
            direction.normalized * CurrentMaxSpeed;

        return CalculateSteering(desired);
    }
    private Vector3 Arrive(Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= _stopDistance)
        {
            return CalculateSteering(Vector3.zero);
        }

        float targetSpeed = CurrentMaxSpeed
            * (distance - _stopDistance)
            / (_slowingDistance - _stopDistance);

        float desiredSpeed = Mathf.Min(
            targetSpeed,
            CurrentMaxSpeed
        );

        Vector3 desired =
            direction.normalized * desiredSpeed;

        return CalculateSteering(desired);
    }
    private bool InRange(Vector3 position, float radius)
    {
        Vector3 direction = position - transform.position;

        return direction.sqrMagnitude <= radius * radius;
    }
    private void DetectNeighbors()
    {
        _detectedNeighbors = 0;

        foreach (BoidAgent agent in _allAgents)
        {
            if (agent == this || !agent.IsAlive)
            {
                continue;
            }

            if (InRange(agent.transform.position, _viewRadius))
            {
                _detectedNeighbors++;
            }
        }
    }
    private Vector3 CalculateSeparation()
    {
        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (BoidAgent agent in _allAgents)
        {
            if (agent == this || !agent.IsAlive)
            {
                continue;
            }

            if (InRange(agent.transform.position, _separationRadius))
            {
                Vector3 direction =
                    transform.position - agent.transform.position;

                direction.y = 0f;

                float distance = direction.magnitude;

                if (distance > 0.001f)
                {
                    desired += direction.normalized / distance;
                    count++;
                }
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        desired /= count;

        return CalculateSteering(
            desired.normalized * CurrentMaxSpeed
        );
    }
    private Vector3 CalculateAlignment()
    {
        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (BoidAgent agent in _allAgents)
        {
            if (agent == this || !agent.IsAlive)
            {
                continue;
            }

            if (InRange(agent.transform.position, _viewRadius))
            {
                desired += agent.Velocity;
                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        desired /= count;

        return CalculateSteering(
            desired.normalized * CurrentMaxSpeed
        );
    }
    private Vector3 CalculateCohesion()
    {
        Vector3 desiredPosition = Vector3.zero;
        int count = 0;

        foreach (BoidAgent agent in _allAgents)
        {
            if (agent == this || !agent.IsAlive)
            {
                continue;
            }

            if (InRange(agent.transform.position, _viewRadius))
            {
                desiredPosition += agent.transform.position;
                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        desiredPosition /= count;

        return Seek(desiredPosition);
    }
    private Vector3 Flocking()
    {
        return CalculateSeparation() * _separationWeight
             + CalculateAlignment() * _alignmentWeight
             + CalculateCohesion() * _cohesionWeight;
    }
    private Vector3 CalculateFuture(Agent target)
    {
        float distance = Vector3.Distance(
            transform.position,
            target.transform.position
        );

        float combinedSpeed =
            CurrentMaxSpeed + target.Velocity.magnitude;

        if (combinedSpeed <= 0f)
        {
            return target.transform.position;
        }

        float prediction = distance / combinedSpeed;

        return target.transform.position
             + target.Velocity * prediction;
    }
    private Vector3 Evade(Agent target)
    {
        Vector3 futurePosition = CalculateFuture(target);

        return Flee(futurePosition);
    }
    private bool DetectHunter()
    {
        if (_hunter == null)
        {
            return false;
        }

        if (!_hunter.isActiveAndEnabled)
        {
            return false;
        }

        return InRange(
            _hunter.transform.position,
            _viewRadius
        );
    }
    private void ReleaseInterestTarget()
    {
        if (_interestTarget != null) _interestTarget.Release(this);
        _interestTarget = null;
        _interactionTimer = 0f;
        _isInteracting = false;
    }

    private void DetectInterestObject()
    {
        // Conservar la reserva mientras el objeto siga siendo un objetivo valido.
        if (_interestTarget != null && _interestTarget.IsAvailableFor(this)
            && InRange(_interestTarget.transform.position, _viewRadius))
        {
            return;
        }

        ReleaseInterestTarget();
        InterestObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (InterestObject interest in InterestObject.AllObjects)
        {
            if (interest == null || !interest.IsAvailableFor(this)) continue;
            if (!InRange(interest.transform.position, _viewRadius)) continue;

            float distance = (interest.transform.position - transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = interest;
            }
        }

        if (closest != null && closest.TryReserve(this))
        {
            _interestTarget = closest;
        }
    }

    private void UpdateInteraction()
    {
        _isInteracting = false;

        if (_threatDetected)
        {
            _interactionTimer = 0f;
            return;
        }

        if (_interestTarget == null || !_interestTarget.IsAvailableFor(this)
            || _interestTarget.ReservedBy != this)
        {
            _interactionTimer = 0f;
            return;
        }

        if (!InRange(
            _interestTarget.transform.position,
            _interactionRadius
        ))
        {
            _interactionTimer = 0f;
            return;
        }

        _isInteracting = true;

        _interactionTimer += Time.deltaTime;

        if (_interactionTimer >= _interactionInterval)
        {
            bool consumed = _interestTarget.Consume(this, _interactionDamage);
            _interactionTimer = 0f;

            if (consumed)
            {
                ReleaseInterestTarget();
                BecomeSick();
            }
        }
    }
    private void BecomeSick()
    {
        _sickTimer = _sickDuration;
        _velocity = Vector3.ClampMagnitude(_velocity, CurrentMaxSpeed);
        if (_bodyRenderer != null) _bodyRenderer.material.color = _sickColor;
    }

    private void UpdateSickness()
    {
        if (!IsSick) return;

        _sickTimer = Mathf.Max(0f, _sickTimer - Time.deltaTime);
        if (!IsSick && _bodyRenderer != null)
        {
            _bodyRenderer.material.color = _originalColor;
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(
            0f,
            _currentHealth - damage
        );

        if (!IsAlive)
        {
            Die();
        }
    }
    public bool Collect()
    {
        if (IsAlive || _collected || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (WorldBounds.Instance == null)
        {
            return false;
        }

        _collected = true;
        // El temporizador vive en WorldBounds porque este objeto se desactiva.
        WorldBounds.Instance.RespawnAfter(this, _respawnDelay);
        return true;
    }

    private void Die()
    {
        ReleaseInterestTarget();
        _sickTimer = 0f;
        _velocity = Vector3.zero;

        _interactionTimer = 0f;
        _isInteracting = false;

        _threatDetected = false;
        _interestTarget = null;
        _detectedNeighbors = 0;

        if (_bodyRenderer != null)
        {
            _bodyRenderer.material.color = Color.red;
        }
    }
    [ContextMenu("Debug/Recibir 5 de dano")]
    private void DebugTakeDamage()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        TakeDamage(5f);
    }
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && _threatDetected)
        {
            Gizmos.color = Color.magenta;
        }
        else
        {
            Gizmos.color = Color.cyan;
        }

        Gizmos.DrawWireSphere(
            transform.position,
            _viewRadius
        );

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            _separationRadius
        );

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawLine(
                transform.position,
                transform.position + _velocity
            );
        }
    }
}
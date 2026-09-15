using System.Collections.Generic;
using UnityEngine;


public class BoidAgent : Agent
{
    [Header("Stats")]
    [SerializeField] private float _maxSpeed = 4f;
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

    [SerializeField, Range(0f, 3f)]
    private float _separationWeight = 1f;

    [SerializeField, Range(0f, 3f)]
    private float _alignmentWeight = 1f;

    [SerializeField, Range(0f, 3f)]
    private float _cohesionWeight = 1f;

    private static List<BoidAgent> _allAgents =
        new List<BoidAgent>();

    private void OnEnable()
    {
        _allAgents.Add(this);
    }

    private void OnDisable()
    {
        _allAgents.Remove(this);
    }

    private void Start()
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

        _velocity = randomDirection.normalized * _maxSpeed;
    }

    private void Update()
    {
        DetectNeighbors();
        DetectInterestObject();

        _threatDetected = DetectHunter();

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
            _maxSpeed
        );

        transform.position += _velocity * Time.deltaTime;

        if (_velocity.sqrMagnitude > 0.001f)
        {
            transform.forward = _velocity.normalized;
        }

        transform.position =
            WorldBounds.Instance.OutOfBounds(transform.position);
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
            direction.normalized * _maxSpeed;

        return CalculateSteering(desired);
    }
    private Vector3 Flee(Vector3 targetPosition)
    {
        Vector3 direction =
            transform.position - targetPosition;

        Vector3 desired =
            direction.normalized * _maxSpeed;

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

        float targetSpeed = _maxSpeed
            * (distance - _stopDistance)
            / (_slowingDistance - _stopDistance);

        float desiredSpeed = Mathf.Min(
            targetSpeed,
            _maxSpeed
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
            if (agent == this)
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
            if (agent == this)
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
            desired.normalized * _maxSpeed
        );
    }
    private Vector3 CalculateAlignment()
    {
        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (BoidAgent agent in _allAgents)
        {
            if (agent == this)
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
            desired.normalized * _maxSpeed
        );
    }
    private Vector3 CalculateCohesion()
    {
        Vector3 desiredPosition = Vector3.zero;
        int count = 0;

        foreach (BoidAgent agent in _allAgents)
        {
            if (agent == this)
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
            _maxSpeed + target.Velocity.magnitude;

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
    private void DetectInterestObject()
    {
        _interestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (InterestObject interest in InterestObject.AllObjects)
        {
            if (interest == null)
            {
                continue;
            }

            if (!InRange(interest.transform.position, _viewRadius))
            {
                continue;
            }

            float distance = (
                interest.transform.position - transform.position
            ).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                _interestTarget = interest;
            }
        }
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
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
        _velocity += Flocking();

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

        Vector3 desired =
            direction.normalized * _maxSpeed;

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
                desired +=
                    agent.transform.position - transform.position;

                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        desired /= count;

        return CalculateSteering(
            -desired.normalized * _maxSpeed
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
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

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
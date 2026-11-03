using System.Collections.Generic;
using UnityEngine;

public sealed class BoidSteering
{
    private Vector3 _position;
    private Vector3 _velocity;
    private float _maxSpeed;
    private float _steeringLimit;

    public void UpdateMotion(Vector3 position, Vector3 velocity, float maxSpeed, float maxSteering, float deltaTime)
    {
        _position = position;
        _velocity = velocity;
        _maxSpeed = maxSpeed;
        _steeringLimit = maxSteering * deltaTime;
    }

    public Vector3 Flocking(IReadOnlyList<BoidAgent> neighbors, float separationRadius, float viewRadius, Vector3 weights)
    {
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 center = Vector3.zero;
        int separationCount = 0;
        int neighborCount = 0;
        for (int i = 0; i < neighbors.Count; i++)
        {
            BoidAgent neighbor = neighbors[i];
            if (!IsValid(neighbor)) continue;
            Vector3 offset = _position - neighbor.transform.position;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared <= separationRadius * separationRadius)
                AddSeparation(offset, ref separation, ref separationCount);
            if (distanceSquared > viewRadius * viewRadius) continue;
            alignment += neighbor.Velocity;
            center += neighbor.transform.position;
            neighborCount++;
        }

        Vector3 result = SeparationForce(separation, separationCount) * weights.x;
        if (neighborCount == 0) return result;
        result += Calculate((alignment / neighborCount).normalized * _maxSpeed) * weights.y;
        result += Seek(center / neighborCount) * weights.z;
        return result;
    }

    public Vector3 Separation(IReadOnlyList<BoidAgent> neighbors, float radius)
    {
        Vector3 desired = Vector3.zero;
        int count = 0;
        for (int i = 0; i < neighbors.Count; i++)
        {
            BoidAgent neighbor = neighbors[i];
            if (!IsValid(neighbor)) continue;
            Vector3 offset = _position - neighbor.transform.position;
            if (offset.sqrMagnitude <= radius * radius) AddSeparation(offset, ref desired, ref count);
        }

        return SeparationForce(desired, count);
    }

    public Vector3 Arrive(Vector3 target, float slowingDistance, float stopDistance)
    {
        Vector3 direction = target - _position;
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance <= stopDistance) return Calculate(Vector3.zero);
        float slowingRange = Mathf.Max(0.001f, slowingDistance - stopDistance);
        float speed = Mathf.Min(_maxSpeed * (distance - stopDistance) / slowingRange, _maxSpeed);
        return Calculate(direction.normalized * speed);
    }

    public Vector3 Evade(Agent target)
    {
        if (target == null || !target.isActiveAndEnabled) return Vector3.zero;
        float distance = Vector3.Distance(_position, target.transform.position);
        float combinedSpeed = _maxSpeed + target.Velocity.magnitude;
        float prediction = combinedSpeed > 0f ? distance / combinedSpeed : 0f;
        Vector3 direction = _position - (target.transform.position + target.Velocity * prediction);
        direction.y = 0f;
        return Calculate(direction.normalized * _maxSpeed);
    }

    private Vector3 Seek(Vector3 target)
    {
        Vector3 direction = target - _position;
        direction.y = 0f;
        return Calculate(direction.normalized * _maxSpeed);
    }

    private Vector3 Calculate(Vector3 desired)
    {
        return Vector3.ClampMagnitude(desired - _velocity, _steeringLimit);
    }

    private Vector3 SeparationForce(Vector3 desired, int count)
    {
        return count == 0 ? Vector3.zero : Calculate((desired / count).normalized * _maxSpeed);
    }

    private static void AddSeparation(Vector3 offset, ref Vector3 desired, ref int count)
    {
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance <= 0.001f) return;
        desired += offset.normalized / distance;
        count++;
    }

    private static bool IsValid(BoidAgent neighbor)
    {
        return neighbor != null && neighbor.isActiveAndEnabled && neighbor.IsAlive;
    }
}

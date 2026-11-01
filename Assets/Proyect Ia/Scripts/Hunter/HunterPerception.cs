using System.Collections.Generic;
using UnityEngine;

public class HunterPerception
{
    private readonly ProximitySensor _sensor = new ProximitySensor();
    private readonly List<BoidAgent> _boids = new List<BoidAgent>();
    private readonly List<InterestObject> _interestObjects = new List<InterestObject>();
    private float _timeUntilScan;

    public void Update(Vector3 origin, float viewRadius, float interestRadius, LayerMask layers,
        float interval, float deltaTime)
    {
        _timeUntilScan -= deltaTime;
        if (_timeUntilScan > 0f) return;

        _timeUntilScan = Mathf.Max(0.01f, interval);
        _sensor.Scan(origin, Mathf.Max(viewRadius, interestRadius), layers);
        _sensor.GetDetected(_boids);
        _sensor.GetDetected(_interestObjects);
    }

    public bool CanSee(BoidAgent agent, Vector3 origin, float radius)
    {
        return IsInRange(agent, origin, radius) && _boids.Contains(agent);
    }

    public BoidAgent FindClosestBoid(Vector3 origin, float radius, bool alive)
    {
        BoidAgent closest = null;
        float closestDistance = float.MaxValue;

        foreach (BoidAgent boid in _boids)
        {
            if (!IsInRange(boid, origin, radius) || boid.IsAlive != alive) continue;

            float distance = PlanarDistanceSquared(origin, boid.transform.position);
            if (distance >= closestDistance) continue;

            closestDistance = distance;
            closest = boid;
        }

        return closest;
    }

    public bool HasNearbyInterest(Vector3 origin, float radius)
    {
        foreach (InterestObject interest in _interestObjects)
        {
            if (interest == null || !interest.isActiveAndEnabled) continue;
            if (PlanarDistanceSquared(origin, interest.transform.position) < radius * radius) return true;
        }

        return false;
    }

    private static float PlanarDistanceSquared(Vector3 origin, Vector3 target)
    {
        Vector3 offset = target - origin;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private static bool IsInRange(BoidAgent agent, Vector3 origin, float radius)
    {
        if (agent == null || !agent.isActiveAndEnabled || radius < 0f) return false;
        return PlanarDistanceSquared(origin, agent.transform.position) <= radius * radius;
    }
}

using System.Collections.Generic;
using UnityEngine;

public sealed class BoidPerception
{
    private readonly ProximitySensor _sensor = new ProximitySensor();
    private readonly List<BoidAgent> _boidCandidates = new List<BoidAgent>();
    private readonly List<HunterAgent> _hunterCandidates = new List<HunterAgent>();
    private readonly List<InterestObject> _interestCandidates = new List<InterestObject>();
    private readonly List<BoidAgent> _neighbors = new List<BoidAgent>();
    private float _timeUntilScan;

    public IReadOnlyList<BoidAgent> Neighbors => _neighbors;
    public HunterAgent Threat { get; private set; }
    public int VisibleNeighborCount { get; private set; }

    public void Reset()
    {
        _boidCandidates.Clear();
        _hunterCandidates.Clear();
        _interestCandidates.Clear();
        _neighbors.Clear();
        Threat = null;
        VisibleNeighborCount = 0;
        _timeUntilScan = 0f;
    }

    public void Update(BoidAgent owner, float viewRadius, float separationRadius, float interestRadius,
        float escapeMargin, LayerMask layers, float interval, float deltaTime)
    {
        _timeUntilScan -= deltaTime;
        if (_timeUntilScan <= 0f)
        {
            float radius = Mathf.Max(viewRadius + escapeMargin, Mathf.Max(separationRadius, interestRadius));
            Scan(owner.transform.position, radius, layers);
            _timeUntilScan = Mathf.Max(0.01f, interval);
        }

        RefreshNeighbors(owner, viewRadius, separationRadius);
        RefreshThreat(owner.transform.position, viewRadius, escapeMargin);
    }

    public bool IsInterestAvailable(InterestObject interest, BoidAgent owner, float radius)
    {
        return interest != null && _interestCandidates.Contains(interest) && interest.IsAvailableFor(owner)
            && InRange(owner.transform.position, interest.transform.position, radius);
    }

    public InterestObject GetClosestInterest(BoidAgent owner, float radius)
    {
        InterestObject closest = null;
        float closestDistance = float.MaxValue;
        foreach (InterestObject interest in _interestCandidates)
        {
            if (interest == null || !interest.IsAvailableFor(owner)) continue;
            float distance = (interest.transform.position - owner.transform.position).sqrMagnitude;
            if (distance > radius * radius || distance >= closestDistance) continue;
            closest = interest;
            closestDistance = distance;
        }

        return closest;
    }

    private void Scan(Vector3 position, float radius, LayerMask layers)
    {
        _sensor.Scan(position, radius, layers);
        _sensor.GetDetected(_boidCandidates);
        _sensor.GetDetected(_hunterCandidates);
        _sensor.GetDetected(_interestCandidates);
    }

    private void RefreshNeighbors(BoidAgent owner, float viewRadius, float separationRadius)
    {
        _neighbors.Clear();
        VisibleNeighborCount = 0;
        float neighborRadius = Mathf.Max(viewRadius, separationRadius);
        foreach (BoidAgent candidate in _boidCandidates)
        {
            if (candidate == null || candidate == owner || !candidate.isActiveAndEnabled || !candidate.IsAlive) continue;
            if (!InRange(owner.transform.position, candidate.transform.position, neighborRadius)) continue;
            _neighbors.Add(candidate);
            if (InRange(owner.transform.position, candidate.transform.position, viewRadius)) VisibleNeighborCount++;
        }
    }

    private void RefreshThreat(Vector3 position, float viewRadius, float escapeMargin)
    {
        HunterAgent previousThreat = Threat;
        Threat = null;
        float closestDistance = float.MaxValue;
        foreach (HunterAgent candidate in _hunterCandidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            float radius = viewRadius + (candidate == previousThreat ? escapeMargin : 0f);
            Vector3 offset = candidate.transform.position - position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance > radius * radius || distance >= closestDistance) continue;
            Threat = candidate;
            closestDistance = distance;
        }
    }

    private static bool InRange(Vector3 origin, Vector3 target, float radius)
    {
        return (target - origin).sqrMagnitude <= radius * radius;
    }
}

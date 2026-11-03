using UnityEngine;

// Coloca los cebos; la FSM decide cuando se permite esta accion.
public sealed class HunterInterestPlacement
{
    private readonly Transform _owner;

    public HunterInterestPlacement(Transform owner)
    {
        _owner = owner;
    }

    public bool CanPlace(InterestObject prefab, float elapsed, float interval, int limit,
        HunterPerception perception, float spacing)
    {
        if (prefab == null || elapsed < Mathf.Max(0.1f, interval)) return false;
        if (InterestObject.ActiveCount >= limit) return false;
        return !perception.HasNearbyInterest(_owner.position, spacing);
    }

    public void Place(InterestObject prefab, Transform parent, float height)
    {
        Vector3 position = _owner.position;
        position.y = height;
        Object.Instantiate(prefab, position, Quaternion.identity, parent);
    }
}

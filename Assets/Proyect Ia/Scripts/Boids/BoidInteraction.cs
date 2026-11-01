using UnityEngine;

public sealed class BoidInteraction
{
    private readonly BoidAgent _owner;
    private readonly BoidMovement _movement;
    private float _timer;

    public InterestObject Target { get; private set; }
    public bool IsInteracting { get; private set; }

    public BoidInteraction(BoidAgent owner, BoidMovement movement)
    {
        _owner = owner;
        _movement = movement;
    }

    public void UpdateTarget(BoidPerception perception, float radius, bool threatened)
    {
        if (threatened || !_owner.IsAlive || !_owner.isActiveAndEnabled)
        {
            Release();
            return;
        }

        if (perception.IsInterestAvailable(Target, _owner, radius)) return;
        Release();
        InterestObject closest = perception.GetClosestInterest(_owner, radius);
        if (closest != null && closest.TryReserve(_owner)) Target = closest;
    }

    public bool Tick(float interactionRadius, float stopDistance, float damage, float interval, float deltaTime)
    {
        IsInteracting = false;
        if (!_owner.IsAlive || !_owner.isActiveAndEnabled || Target == null
            || !Target.IsAvailableFor(_owner) || Target.ReservedBy != _owner)
        {
            Release();
            return false;
        }

        Vector3 targetPosition = Target.transform.position;
        targetPosition.y = _owner.transform.position.y;
        float interactionDistance = Mathf.Min(interactionRadius, stopDistance + 0.05f);
        if (Vector3.Distance(_owner.transform.position, targetPosition) > interactionDistance)
        {
            _timer = 0f;
            return false;
        }

        _movement.StayBeside(targetPosition, stopDistance);
        IsInteracting = true;
        _timer += deltaTime;
        if (_timer < interval) return false;
        bool consumed = Target.Consume(_owner, damage);
        _timer = 0f;
        if (consumed) Release();
        return consumed;
    }

    public void Release()
    {
        if (Target != null) Target.Release(_owner);
        Target = null;
        _timer = 0f;
        IsInteracting = false;
    }
}

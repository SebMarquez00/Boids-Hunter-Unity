using UnityEngine;

public sealed class BoidMovement
{
    private readonly Transform _transform;

    public Vector3 Velocity { get; private set; }

    public BoidMovement(Transform transform)
    {
        _transform = transform;
    }

    public void InitializeVelocity(float speed)
    {
        Vector3 direction = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        Velocity = direction.normalized * speed;
    }

    public void Move(Vector3 steering, float maxSpeed, float deltaTime, WorldBounds bounds)
    {
        Vector3 velocity = Velocity + steering;
        velocity.y = 0f;
        Velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        _transform.position += Velocity * deltaTime;
        if (Velocity.sqrMagnitude > 0.001f) _transform.forward = Velocity.normalized;
        if (bounds != null) _transform.position = bounds.OutOfBounds(_transform.position);
    }

    public void StayBeside(Vector3 targetPosition, float stopDistance)
    {
        targetPosition.y = _transform.position.y;
        Vector3 offset = _transform.position - targetPosition;
        if (offset.sqrMagnitude < 0.001f) offset = -_transform.forward;
        _transform.position = targetPosition + offset.normalized * stopDistance;
        Stop();
    }

    public void Resume(float speed)
    {
        Velocity = _transform.forward * speed;
    }

    public void Stop()
    {
        Velocity = Vector3.zero;
    }
}

using UnityEngine;

public class HunterMovement
{
    private readonly Transform _transform;

    public HunterMovement(Transform transform)
    {
        _transform = transform;
    }

    public Vector3 MoveTowards(Vector3 targetPosition, float speed, float deltaTime)
    {
        targetPosition.y = _transform.position.y;
        Vector3 previousPosition = _transform.position;
        _transform.position = Vector3.MoveTowards(previousPosition, targetPosition, Mathf.Max(0f, speed) * deltaTime);

        Vector3 velocity = deltaTime > 0f ? (_transform.position - previousPosition) / deltaTime : Vector3.zero;
        if (velocity.sqrMagnitude > 0.001f) _transform.forward = velocity.normalized;
        return velocity;
    }

    public Vector3 PredictPosition(Vector3 targetPosition, Vector3 targetVelocity, float speed, float maxPredictionTime)
    {
        float predictionTime = DistanceTo(targetPosition) / Mathf.Max(0.01f, speed);
        predictionTime = Mathf.Min(predictionTime, Mathf.Max(0f, maxPredictionTime));
        targetVelocity.y = 0f;
        return targetPosition + targetVelocity * predictionTime;
    }

    public float DistanceTo(Vector3 position)
    {
        Vector3 offset = position - _transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }
}

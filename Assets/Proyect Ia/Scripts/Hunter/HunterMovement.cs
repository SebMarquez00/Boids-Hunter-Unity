using UnityEngine;

public class HunterMovement
{
    private readonly Transform _transform;

    public HunterMovement(Transform transform)
    {
        _transform = transform;
    }

    public Vector3 MoveTowards(Vector3 targetPosition, float speed, float deltaTime,
        float turnSpeed = float.PositiveInfinity)
    {
        if (deltaTime <= 0f) return Vector3.zero;
        targetPosition.y = _transform.position.y;
        Vector3 previousPosition = _transform.position;
        _transform.position = Vector3.MoveTowards(previousPosition, targetPosition, Mathf.Max(0f, speed) * deltaTime);

        Vector3 velocity = (_transform.position - previousPosition) / deltaTime;
        if (velocity.sqrMagnitude > 0.001f)
        {
            if (float.IsPositiveInfinity(turnSpeed)) _transform.forward = velocity.normalized;
            else
            {
                Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                _transform.rotation = Quaternion.RotateTowards(_transform.rotation,
                    targetRotation, Mathf.Max(0f, turnSpeed) * deltaTime);
            }
        }
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

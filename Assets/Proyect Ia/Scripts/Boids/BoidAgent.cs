using UnityEngine;

public class BoidAgent : Agent
{
    [Header("Stats")]
    [SerializeField] private float _maxSpeed = 4f;
    [SerializeField] private float _maxSteering = 8f;

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
}
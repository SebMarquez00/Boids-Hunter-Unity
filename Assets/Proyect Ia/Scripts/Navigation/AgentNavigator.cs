using UnityEngine;

public class AgentNavigator : MonoBehaviour
{
    [SerializeField] private PathFollower _follower;
    [SerializeField, Min(0.01f)] private float _repathInterval = 0.5f;
    [SerializeField, Min(0f)] private float _destinationThreshold = 0.25f;
    [SerializeField, Min(0f), Tooltip("Velocidad de giro en grados por segundo.")]
    private float _turnSpeed = 360f;

    private const float ArrivalTolerance = 0.02f;
    private Vector3 _plannedDestination;
    private float _timeUntilRepath;
    private bool _blockedRetryUsed;

    public PathFollower Follower
    {
        get => _follower;
        set
        {
            if (_follower == value) return;
            Stop();
            _follower = value;
        }
    }

    public Vector3 Destination { get; private set; }
    public float TurnSpeed => Mathf.Max(0f, _turnSpeed);
    public bool HasDestination { get; private set; }
    public bool WaitingForPath { get; private set; }
    public int CalculationCount { get; private set; }
    public int Revision { get; private set; }
    public Vector3 Velocity => HasDestination && !WaitingForPath && _follower != null ? _follower.Velocity : Vector3.zero;
    public bool HasArrived => HasDestination && !WaitingForPath && _follower != null && _follower.HasArrived
        && PlanarDistanceSquared(_follower.transform.position, Destination) <= ArrivalTolerance * ArrivalTolerance;

    private void OnDisable() => Stop();

    // La FSM entrega su objetivo actual; este componente no ejecuta un Update propio.
    public void NavigateTo(Vector3 destination, float speed, float deltaTime)
    {
        bool firstRequest = !HasDestination;
        Destination = destination;
        HasDestination = true;
        float step = deltaTime > 0f && !float.IsInfinity(deltaTime) ? deltaTime : 0f;
        _timeUntilRepath = Mathf.Max(0f, _timeUntilRepath - step);
        if (_follower == null)
        {
            WaitingForPath = true;
            return;
        }

        _follower.Speed = speed;
        if (firstRequest || (_timeUntilRepath <= 0f && NeedsNewPath())) CalculatePath();
        if (WaitingForPath) return;

        Vector3 previousPosition = _follower.transform.position;
        _follower.Tick(step);
        FaceMovement(step);
        if (_follower.IsBlocked) HandleBlockedPath();
        else if (PlanarDistanceSquared(previousPosition, _follower.transform.position) > 0f)
            _blockedRetryUsed = false;
    }

    private bool NeedsNewPath()
    {
        if (WaitingForPath || _follower.IsBlocked) return true;
        if (_follower.HasArrived)
            return PlanarDistanceSquared(_follower.transform.position, Destination) > ArrivalTolerance * ArrivalTolerance;
        if (!_follower.IsMoving) return true;

        float threshold = Mathf.Max(0f, _destinationThreshold);
        return PlanarDistanceSquared(_plannedDestination, Destination) > threshold * threshold;
    }

    private void CalculatePath()
    {
        _plannedDestination = Destination;
        _timeUntilRepath = Mathf.Max(0.01f, _repathInterval);
        CalculationCount++;
        Revision++;
        WaitingForPath = !_follower.SetDestination(Destination);
    }

    private void HandleBlockedPath()
    {
        WaitingForPath = true;
        if (_blockedRetryUsed) return;
        _blockedRetryUsed = true;
        // Solo un intento inmediato por bloqueo. Los siguientes respetan el intervalo.
        CalculatePath();
    }

    private void FaceMovement(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        Vector3 direction = _follower.Velocity;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _follower.transform.rotation = Quaternion.RotateTowards(_follower.transform.rotation,
            targetRotation, TurnSpeed * deltaTime);
    }

    public void Stop()
    {
        if (_follower != null) _follower.Stop();
        Destination = default;
        _plannedDestination = default;
        HasDestination = false;
        WaitingForPath = false;
        _timeUntilRepath = 0f;
        _blockedRetryUsed = false;
        Revision++;
    }

    private static float PlanarDistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }
}
